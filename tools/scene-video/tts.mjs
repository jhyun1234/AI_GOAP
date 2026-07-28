/* 회차 대본 → 음성 (Supertonic / 로컬 ONNX)
   사용: node tools/scene-video/tts.mjs ep01

   만드는 것
     build/audio/<ep>/NN.wav   줄별. 엔진이 타임라인에 맞춰 재생한다.
     build/<ep>.full.wav       회차 전체(호흡 포함). 듣기·최종 합성용.
     build/<ep>.timed.json     실측 길이. 엔진은 이게 있으면 글자수 추정 대신 이 값을 쓴다.

   길이는 추정하지 않는다 — 샘플 수 ÷ 샘플레이트가 곧 시간이다.
   Edge TTS 백엔드는 tts.js 에 남겨 뒀다(참고용). 어느 쪽을 쓰는지는 voice.json 이 정한다.
*/
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { loadTextToSpeech, loadVoiceStyle, writeWavFile } from './vendor/repo/nodejs/helper.js';

const ROOT = path.dirname(fileURLToPath(import.meta.url));
const EP = process.argv[2] || 'ep01';
const read = p => JSON.parse(fs.readFileSync(path.join(ROOT, p), 'utf8'));

const voice = read('voice.json');
const scene = read(`scenes/${EP}.json`);
const { voice: voiceName, speed, steps, assets } = voice.engine;
if (voice.engine.provider !== 'supertonic')
  throw new Error(`voice.json provider 가 supertonic 이 아니다: ${voice.engine.provider}`);

const A = path.join(ROOT, assets);
const outDir = path.join(ROOT, 'build', 'audio', EP);
fs.mkdirSync(outDir, { recursive: true });

const tts = await loadTextToSpeech(path.join(A, 'onnx'), false);
const style = loadVoiceStyle([path.join(A, 'voice_styles', `${voiceName}.json`)], false);
const SR = tts.sampleRate;

const flat = [];
scene.shots.forEach((s, si) => s.lines.forEach((l, li) => flat.push({ si, li, l })));
console.log(`${EP} · Supertonic ${voiceName} · speed ${speed} · ${SR}Hz · ${flat.length}줄`);

// 같은 문장은 다시 합성하지 않는다
let prev = {};
try { prev = read(`build/${EP}.timed.json`); } catch { }
const cachedOk = (si, li, say) => {
  const c = prev.shots?.[si]?.lines?.[li];
  return c && c.say === say && c.voice === voiceName && c.speed === speed
    && fs.existsSync(path.join(ROOT, c.file)) ? c : null;
};

const timed = {
  id: EP, engine: 'supertonic', voice: voiceName, speed, sampleRate: SR,
  shots: scene.shots.map(s => ({ id: s.id, lines: s.lines.map(() => ({})) }))
};

const pcm = [];                       // 통짜 파일용
let spoken = 0, pauses = 0, made = 0, reused = 0;

for (let i = 0; i < flat.length; i++) {
  const { si, li, l } = flat[i];
  const say = l.say || l.text;
  const file = path.join(outDir, String(i).padStart(2, '0') + '.wav');
  const rel = path.relative(ROOT, file).replace(/\\/g, '/');

  let dur;
  const hit = cachedOk(si, li, say);
  if (hit) { dur = hit.dur; reused++; }
  else {
    const r = await tts.call(say, 'ko', style, steps, speed);
    const wav = r.wav.slice(0, Math.floor(SR * r.duration[0]));
    writeWavFile(file, wav, SR);
    dur = Math.round(r.duration[0] * 1000);
    made++;
  }

  // 통짜 파일은 항상 방금 쓴 파일에서 다시 읽는다(캐시든 신규든 같은 경로)
  const buf = fs.readFileSync(file);
  const n = (buf.length - 44) / 4;                    // Float32 mono 라고 가정하지 않고 헤더로 확인
  const bits = buf.readUInt16LE(34), ch = buf.readUInt16LE(22);
  const samples = (buf.length - 44) / (bits / 8) / ch;
  const f32 = new Float32Array(samples);
  for (let s = 0; s < samples; s++)
    f32[s] = bits === 16 ? buf.readInt16LE(44 + s * 2) / 32768 : buf.readFloatLE(44 + s * 4);
  pcm.push(f32);

  const pause = l.pauseAfter ?? 0;
  if (pause > 0 && i < flat.length - 1) pcm.push(new Float32Array(Math.round(SR * pause / 1000)));

  timed.shots[si].lines[li] = { dur, pause, say, chars: say.length, voice: voiceName, speed, file: rel };
  spoken += dur; pauses += pause;
  process.stdout.write(`\r  ${i + 1}/${flat.length}  ${(spoken / 1000).toFixed(1)}s`);
}
console.log(`\n  새로 만듦 ${made} · 재사용 ${reused}`);

let total = 0; for (const p of pcm) total += p.length;
const all = new Float32Array(total);
let o = 0; for (const p of pcm) { all.set(p, o); o += p.length; }
writeWavFile(path.join(ROOT, 'build', `${EP}.full.wav`), all, SR);

timed.summary = {
  spokenMs: spoken, pauseMs: pauses, totalMs: spoken + pauses,
  fullMs: Math.round(total / SR * 1000),
  chars: flat.reduce((a, { l }) => a + (l.say || l.text).replace(/[.,?!…]/g, '').length, 0)
};
timed.summary.charsPerSec = +(timed.summary.chars / (timed.summary.totalMs / 1000)).toFixed(2);
fs.writeFileSync(path.join(ROOT, 'build', `${EP}.timed.json`), JSON.stringify(timed, null, 1));

const S = timed.summary;
console.log(`  말하는 시간  ${(S.spokenMs / 1000).toFixed(1)}s`);
console.log(`  침묵         ${(S.pauseMs / 1000).toFixed(1)}s  (${(S.pauseMs / S.totalMs * 100).toFixed(1)}%)`);
console.log(`  합계         ${(S.totalMs / 1000).toFixed(1)}s   통짜 ${(S.fullMs / 1000).toFixed(1)}s`);
console.log(`  말 속도      ${S.charsPerSec}자/초   (참고 영상 6.93)`);
