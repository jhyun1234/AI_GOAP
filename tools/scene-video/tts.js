/* 회차 대본 → 음성.
   사용: node tools/scene-video/tts.js ep01

   두 가지를 만든다.
     1) build/audio/<ep>/NN.mp3  — 줄별. 길이를 재서 타임라인을 확정하는 데 쓴다.
     2) build/<ep>.full.mp3      — 회차 전체를 SSML <break> 로 이어붙인 한 파일.
                                   듣기용이자 최종 mp4 합성용.
   그리고 build/<ep>.timed.json 에 실측 길이를 적는다. 엔진은 이 파일이 있으면
   글자수 추정 대신 이 값을 쓴다.

   길이는 추정하지 않는다 — 48kbps CBR MP3 라 바이트 수가 곧 시간이다(6바이트 = 1ms).
*/
const fs = require('fs');
const path = require('path');
const { MsEdgeTTS, OUTPUT_FORMAT } = require('msedge-tts');

const ROOT = __dirname;
const EP = process.argv[2] || 'ep01';
const FMT = OUTPUT_FORMAT.AUDIO_24KHZ_48KBITRATE_MONO_MP3;
const BYTES_PER_MS = 6;                       // 48000bit/s ÷ 8 ÷ 1000
const msOf = buf => Math.round(buf.length / BYTES_PER_MS);

const esc = s => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
                  .replace(/"/g, '&quot;').replace(/'/g, '&apos;');

const read = p => JSON.parse(fs.readFileSync(path.join(ROOT, p), 'utf8'));

function collect(stream) {
  return new Promise((res, rej) => {
    const chunks = [];
    stream.on('data', c => chunks.push(c));
    stream.on('end', () => res(Buffer.concat(chunks)));
    stream.on('error', rej);
  });
}

(async () => {
  const voice = read('voice.json');
  const scene = read(`scenes/${EP}.json`);
  const { voice: voiceName, rate, pitch } = voice.engine;

  const outDir = path.join(ROOT, 'build', 'audio', EP);
  fs.mkdirSync(outDir, { recursive: true });

  const tts = new MsEdgeTTS();
  await tts.setMetadata(voiceName, FMT);

  // ── 1) 줄별 ────────────────────────────────────────────
  const flat = [];
  scene.shots.forEach((s, si) => s.lines.forEach((l, li) => flat.push({ si, li, l })));

  console.log(`${EP} · ${voiceName} · rate ${rate} · ${flat.length}줄`);
  const timed = { id: EP, voice: voiceName, rate, generatedFrom: 'tts.js', shots: [] };
  scene.shots.forEach(s => timed.shots.push({ id: s.id, lines: s.lines.map(() => ({})) }));

  // 같은 문장을 다시 합성하지 않는다 — 재실행이 잦고, 매번 28번 왕복은 낭비다.
  let prev = {};
  try { prev = read(`build/${EP}.timed.json`); } catch { /* 첫 실행 */ }
  const cached = (si, li, say) => {
    const c = prev.shots?.[si]?.lines?.[li];
    if (!c || c.say !== say) return null;
    const f = path.join(ROOT, c.file);
    return fs.existsSync(f) ? fs.readFileSync(f) : null;
  };

  let spoken = 0, pauses = 0, made = 0, reused = 0;
  for (let i = 0; i < flat.length; i++) {
    const { si, li, l } = flat[i];
    const say = l.say || l.text;
    const file = path.join(outDir, String(i).padStart(2, '0') + '.mp3');

    let buf = cached(si, li, say);
    if (buf) { reused++; }
    else {
      buf = await collect(tts.toStream(say, { rate, pitch }).audioStream);
      fs.writeFileSync(file, buf); made++;
    }

    const dur = msOf(buf);
    const pause = l.pauseAfter ?? 0;
    timed.shots[si].lines[li] = { dur, pause, say, chars: say.length, file: path.relative(ROOT, file).replace(/\\/g, '/') };
    spoken += dur; pauses += pause;
    process.stdout.write(`\r  ${i + 1}/${flat.length}  ${(spoken / 1000).toFixed(1)}s`);
  }
  console.log(`\n  새로 만듦 ${made} · 재사용 ${reused}`);

  // 엔진이 읽는 dur 은 '말하는 시간'만. 침묵은 씬 JSON 의 pauseAfter 가 더한다.
  const total = spoken + pauses;
  timed.summary = { spokenMs: spoken, pauseMs: pauses, totalMs: total };
  tts.close();

  // 타임라인부터 먼저 저장한다. 아래 통짜 합성이 실패해도 여기까지는 남아야 한다.
  const timedPath = path.join(ROOT, 'build', `${EP}.timed.json`);
  fs.writeFileSync(timedPath, JSON.stringify(timed, null, 1));

  // ── 회차 통짜 파일 ─────────────────────────────────────
  // Edge TTS 무료 엔드포인트는 <break> 를 거부한다 — 태그 없는 같은 요청은 되는데
  // break 하나만 넣어도 turn.end 없이 소켓이 닫힌다(실측). 그래서 침묵을 SSML 로는
  // 못 만든다.
  //
  // 대신 volume:silent 로 '소리 없는 진짜 MP3 프레임'을 합성해 잘라 쓴다.
  // 이 포맷(24kHz·48kbps CBR, MPEG-2 Layer III)은 프레임이 144바이트 = 24ms 로
  // 정확히 떨어져서, 프레임 경계로만 자르면 이어붙여도 깨지지 않는다.
  const FRAME = 144, FRAME_MS = 24;
  let fullBuf = null;
  try {
    const t2 = new MsEdgeTTS();
    await t2.setMetadata(voiceName, FMT);
    const sil = await collect(t2.toStream('가나다라마바사아자차카타파하'.repeat(6), { volume: 'silent' }).audioStream);
    try { t2.close(); } catch { }

    const silFrames = Math.floor(sil.length / FRAME);
    const parts = [];
    flat.forEach(({ si, li }, i) => {
      parts.push(fs.readFileSync(path.join(ROOT, timed.shots[si].lines[li].file)));
      const p = timed.shots[si].lines[li].pause;
      if (p > 0 && i < flat.length - 1) {
        const need = Math.min(Math.round(p / FRAME_MS), silFrames);
        parts.push(sil.subarray(0, need * FRAME));
      }
    });
    fullBuf = Buffer.concat(parts);
    fs.writeFileSync(path.join(ROOT, 'build', `${EP}.full.mp3`), fullBuf);
    timed.summary.fullMs = msOf(fullBuf);
    timed.summary.driftMs = msOf(fullBuf) - total;
    fs.writeFileSync(timedPath, JSON.stringify(timed, null, 1));
  } catch (e) {
    console.log(`  통짜 파일 실패(줄별·타임라인은 정상): ${e.message}`);
  }

  const S = timed.summary;
  console.log(`  말하는 시간  ${(S.spokenMs / 1000).toFixed(1)}s`);
  console.log(`  침묵         ${(S.pauseMs / 1000).toFixed(1)}s`);
  console.log(`  합계         ${(S.totalMs / 1000).toFixed(1)}s`);
  if (S.fullMs) console.log(`  통짜 파일     ${(S.fullMs / 1000).toFixed(1)}s   (합계와 차이 ${S.driftMs}ms)`);
  console.log(`\n  ${path.relative(process.cwd(), timedPath)}`);
  console.log(`  미리보기: http://localhost:4173/engine/?ep=${EP}`);
})().catch(e => { console.error('\n실패:', e.message); process.exit(1); });
