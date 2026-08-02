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
const scene = read(`episodes/${EP}/scene.json`);
const { voice: voiceName, speed, steps, assets } = voice.engine;
if (voice.engine.provider !== 'supertonic')
  throw new Error(`voice.json provider 가 supertonic 이 아니다: ${voice.engine.provider}`);

const A = path.join(ROOT, assets);
const outDir = path.join(ROOT, 'episodes', EP, 'build', 'audio');
fs.mkdirSync(outDir, { recursive: true });

const tts = await loadTextToSpeech(path.join(A, 'onnx'), false);

/* 목소리는 우리 것(voice_styles/)을 먼저 찾고, 없으면 벤더 프리셋(M1~M5, F1~F5)으로 간다.
   🔴 우리가 만든 목소리를 vendor/ 안에 두면 안 된다 — 통째로 gitignore 대상이라 리포에 안 남고,
   녹음 원본은 공개 리포에 못 올리니 날리면 GPU 30분을 다시 태워야 한다. */
const stylePath = ['voice_styles', path.join(assets, 'voice_styles')]
  .map(d => path.join(ROOT, d, `${voiceName}.json`))
  .find(p => fs.existsSync(p));
if (!stylePath) throw new Error(`목소리 스타일을 찾을 수 없다: ${voiceName}.json`);
const style = loadVoiceStyle([stylePath], false);
const SR = tts.sampleRate;

const flat = [];
scene.shots.forEach((s, si) => s.lines.forEach((l, li) => flat.push({ si, li, l })));
console.log(`${EP} · Supertonic ${voiceName} · speed ${speed} · ${SR}Hz · ${flat.length}줄`);

/* 같은 문장은 다시 합성하지 않는다 — 파일 이름을 내용으로 짓는다.
   🔴 인덱스(00.wav, 01.wav…)로 이름을 지으면 안 된다. 자막을 하나 합치거나
   나누는 순간 그 뒤 인덱스가 전부 밀려서, 캐시는 "같은 문장"이라고 판단하는데
   그 자리의 파일은 다른 문장의 음성이 된다. 실제로 그렇게 어긋났다
   (통짜 113.8s vs 합계 116.9s). 내용 해시로 이름을 지으면 구조가 바뀌어도 안전하다. */
const keyOf = say => {
  let h = 2166136261;
  const s = `${voiceName}|${speed}|${steps}|${say}`;
  for (let i = 0; i < s.length; i++) { h ^= s.charCodeAt(i); h = Math.imul(h, 16777619); }
  return (h >>> 0).toString(36).padStart(7, '0');
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
  const file = path.join(outDir, `${String(i).padStart(2, '0')}-${keyOf(say)}.wav`);
  const rel = path.relative(ROOT, file).replace(/\\/g, '/');

  // 같은 내용의 파일이 어느 인덱스에 있든 찾아서 재사용한다
  const twin = fs.readdirSync(outDir).find(f => f.endsWith(`-${keyOf(say)}.wav`));
  if (twin && path.join(outDir, twin) !== file) fs.renameSync(path.join(outDir, twin), file);

  if (!fs.existsSync(file)) {
    const r = await tts.call(say, 'ko', style, steps, speed);
    writeWavFile(file, r.wav.slice(0, Math.floor(SR * r.duration[0])), SR);
    made++;
  } else reused++;

  // 길이도 통짜 파일도 항상 '방금 확정된 그 파일'에서 읽는다.
  // 합성 결과를 따로 들고 있으면 캐시 경로와 신규 경로가 갈려 어긋날 수 있다.
  const buf = fs.readFileSync(file);
  const bits = buf.readUInt16LE(34), ch = buf.readUInt16LE(22);
  const samples = (buf.length - 44) / (bits / 8) / ch;
  const dur = Math.round(samples / SR * 1000);
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
writeWavFile(path.join(ROOT, 'episodes', EP, 'build', 'full.wav'), all, SR);

timed.summary = {
  spokenMs: spoken, pauseMs: pauses, totalMs: spoken + pauses,
  fullMs: Math.round(total / SR * 1000),
  chars: flat.reduce((a, { l }) => a + (l.say || l.text).replace(/[.,?!…]/g, '').length, 0)
};
timed.summary.charsPerSec = +(timed.summary.chars / (timed.summary.totalMs / 1000)).toFixed(2);
fs.writeFileSync(path.join(ROOT, 'episodes', EP, 'build', 'timed.json'), JSON.stringify(timed, null, 1));

const S = timed.summary;
console.log(`  말하는 시간  ${(S.spokenMs / 1000).toFixed(1)}s`);
console.log(`  침묵         ${(S.pauseMs / 1000).toFixed(1)}s  (${(S.pauseMs / S.totalMs * 100).toFixed(1)}%)`);
console.log(`  합계         ${(S.totalMs / 1000).toFixed(1)}s   통짜 ${(S.fullMs / 1000).toFixed(1)}s`);
console.log(`  말 속도      ${S.charsPerSec}자/초   (참고 영상 6.93)`);
