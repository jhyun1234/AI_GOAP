/* 회차 → mp4.
   사용: node tools/scene-video/render.mjs ep01s [--fps 30] [--frames 60] [--quick]
                                                 [--still 12.5,30]

   의존성 0 이다. 크로미움을 CDP 로 직접 몰고, PNG 를 ffmpeg 표준입력으로 흘린다.
   Playwright 를 쓰면 브라우저를 130MB 새로 받아야 하는데, 이 PC 에는 크롬도 엣지도
   이미 있고 우리가 쓰는 명령은 세 개뿐이다(화면 크기 지정 · 스크립트 실행 · 캡처).

   해상도: 무대 CSS 폭 392px × deviceScaleFactor 2.7551 = 1080. CSS 폭을 1080 으로
   키우는 게 아니다 — 글자 크기가 px 고정이라 그렇게 하면 화면 대비 글자가 2.75배
   작아진 다른 디자인이 나온다. 고해상도 화면에서 보는 것과 같은 방식으로 올린다.

   소리: build/<ep>.full.wav 를 그대로 쓰지 않는다. 그건 줄과 호흡만 이어 붙인 것이라
   샷 끝 여운(SHOT_TAIL)이 빠져 있어 뒤로 갈수록 밀린다. 자리 시간표는 엔진에게
   물어보고(window.__timeline), 그 자리에 줄별 wav 를 놓아 트랙을 새로 만든다.

   브라우저를 띄우고 엔진을 로드하는 부분은 check.mjs 와 같아서 lib-node.mjs 에 모았다.
   같은 코드를 두 벌 두면 한쪽만 고쳐지는 날이 온다.
*/
import fs from 'fs';
import path from 'path';
import { spawn } from 'child_process';
import { ROOT, OUT_W, OUT_H, findFfmpeg, openEngine, epScene, epBuild } from './lib-node.mjs';

const argv = process.argv.slice(2);
const EP = argv.find(a => !a.startsWith('--')) || 'ep01';
const flag = (n, d) => {
  const i = argv.indexOf('--' + n);
  return i >= 0 ? (argv[i + 1] && !argv[i + 1].startsWith('--') ? argv[i + 1] : true) : d;
};
const FPS = Number(flag('fps', 30));
const MAX_FRAMES = flag('frames', 0) ? Number(flag('frames', 0)) : 0;
const QUICK = !!flag('quick', false);        // 확인용 저화질·고속 인코딩
const STILL = flag('still', null);

/* ── wav 읽기/쓰기 ───────────────────────────────── */
function readWav(file) {
  const b = fs.readFileSync(file);
  // fmt/data 청크를 찾아 간다 — 헤더 44바이트를 가정하면 청크가 하나만 끼어도 깨진다
  let off = 12, fmt = null, data = null;
  while (off + 8 <= b.length) {
    const id = b.toString('ascii', off, off + 4);
    const size = b.readUInt32LE(off + 4);
    if (id === 'fmt ') fmt = { ch: b.readUInt16LE(off + 10), rate: b.readUInt32LE(off + 12), bits: b.readUInt16LE(off + 22) };
    if (id === 'data') { data = b.subarray(off + 8, off + 8 + size); break; }
    off += 8 + size + (size & 1);
  }
  if (!fmt || !data) throw new Error(`wav 를 못 읽었다: ${file}`);
  const n = Math.floor(data.length / (fmt.bits / 8) / fmt.ch);
  const out = new Float32Array(n);
  for (let i = 0; i < n; i++) {
    const o = i * fmt.ch * (fmt.bits / 8);                 // 다채널이면 첫 채널만
    out[i] = fmt.bits === 16 ? data.readInt16LE(o) / 32768 : data.readFloatLE(o);
  }
  return { rate: fmt.rate, pcm: out };
}

function writeWav16(file, pcm, rate) {
  const b = Buffer.alloc(44 + pcm.length * 2);
  b.write('RIFF', 0); b.writeUInt32LE(36 + pcm.length * 2, 4); b.write('WAVE', 8);
  b.write('fmt ', 12); b.writeUInt32LE(16, 16); b.writeUInt16LE(1, 20); b.writeUInt16LE(1, 22);
  b.writeUInt32LE(rate, 24); b.writeUInt32LE(rate * 2, 28); b.writeUInt16LE(2, 32); b.writeUInt16LE(16, 34);
  b.write('data', 36); b.writeUInt32LE(pcm.length * 2, 40);
  for (let i = 0; i < pcm.length; i++) {
    const v = Math.max(-1, Math.min(1, pcm[i]));
    b.writeInt16LE(Math.round(v * 32767), 44 + i * 2);
  }
  fs.writeFileSync(file, b);
}

/** 엔진이 준 시간표대로 줄별 wav 를 제자리에 놓아 트랙을 만든다 */
function buildTrack(timeline, outFile) {
  const files = timeline.lines.filter(l => l.file);
  if (!files.length) throw new Error('줄별 음성이 없다 — 먼저 tts.mjs 를 돌려라');
  const rate = readWav(path.join(ROOT, files[0].file)).rate;
  const total = Math.ceil(timeline.totalMs / 1000 * rate);
  const track = new Float32Array(total);
  let placed = 0, clipped = 0;
  for (const l of timeline.lines) {
    if (!l.file) continue;
    const { pcm, rate: r } = readWav(path.join(ROOT, l.file));
    if (r !== rate) throw new Error(`샘플레이트가 섞였다: ${l.file}`);
    const at = Math.round(l.t / 1000 * rate);
    for (let i = 0; i < pcm.length; i++) {
      const j = at + i;
      if (j >= total) { clipped++; break; }
      track[j] += pcm[i];                       // 겹칠 일은 없지만 더해 둔다
    }
    placed++;
  }
  writeWav16(outFile, track, rate);
  return { placed, clipped, seconds: total / rate };
}

/* ── 본체 ────────────────────────────────────────── */
const ffmpeg = findFfmpeg();
if (!ffmpeg) { console.error('ffmpeg 를 못 찾았다. winget install yt-dlp.FFmpeg'); process.exit(1); }
if (!fs.existsSync(epScene(EP))) {
  console.error(`episodes/${EP}/scene.json 이 없다`); process.exit(1);
}
console.log(`ffmpeg   ${ffmpeg}`);

const { cdp, close } = await openEngine(EP);
let ff = null;
try {
  // 폰트가 폴백으로 떨어졌는지 — 여기서 걸러야 3천 프레임을 헛돌지 않는다
  const fontWarn = await cdp.evaluate(`(()=>{
    const c=document.createElement('canvas').getContext('2d');
    const p='4,096 무해 ABC';
    c.font='900 62px "__none__", sans-serif'; const base=c.measureText(p).width;
    c.font='900 62px Pretendard, "__none__", sans-serif';
    return Math.abs(c.measureText(p).width-base)>0.5 ? null : 'Pretendard 가 적용되지 않았다';
  })()`);
  if (fontWarn) throw new Error(`폰트: ${fontWarn}`);

  const timeline = await cdp.evaluate('window.__timeline()');
  const nFrames = MAX_FRAMES || Math.floor(timeline.totalMs / 1000 * FPS);

  /* --still 12.5,30  → 그 시각의 프레임만 PNG 로. 4분짜리 전체 렌더 없이 고친 샷 하나를
     눈으로 확인할 때 쓴다. 여기가 실제 추출과 같은 경로라 믿을 수 있다. */
  if (STILL && STILL !== true) {
    const dir = epBuild(EP, 'stills');
    fs.mkdirSync(dir, { recursive: true });
    for (const sec of String(STILL).split(',').map(Number)) {
      await cdp.evaluate(`window.seek(${sec * 1000})`);
      const s = await cdp.send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false });
      const f = path.join(dir, `${sec}s.png`);
      fs.writeFileSync(f, Buffer.from(s.data, 'base64'));
      console.log(`스틸     ${path.relative(process.cwd(), f)}`);
    }
    close(); process.exit(0);
  }

  const trackFile = epBuild(EP, 'track.wav');
  const tr = buildTrack(timeline, trackFile);
  console.log(`소리     ${tr.placed}줄 배치 · ${tr.seconds.toFixed(1)}s` + (tr.clipped ? ` · 🔴 잘림 ${tr.clipped}` : ''));

  // 첫 프레임으로 실제 크기 확인 — 어긋나면 스케일 필터가 맞춘다
  await cdp.evaluate('window.seek(0)');
  const probe = await cdp.send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false });
  const png = Buffer.from(probe.data, 'base64');
  const pw = png.readUInt32BE(16), ph = png.readUInt32BE(20);
  console.log(`프레임   ${pw}×${ph}` + (pw === OUT_W && ph === OUT_H ? '' : ` → ${OUT_W}×${OUT_H} 로 보정`));

  const outFile = epBuild(EP, QUICK ? 'quick.mp4' : 'video.mp4');
  const DUR = (nFrames / FPS).toFixed(3);
  /* 영상·소리를 한 filter_complex 에서 처리한다. -vf 와 -filter_complex 를 섞으면
     같은 스트림을 두 번 잡으려 해서 거절당한다.
     apad = 소리가 영상보다 짧을 때 무음으로 채운다. 마지막 샷 여운은 소리가 없다. */
  ff = spawn(ffmpeg, [
    '-y', '-loglevel', 'error',
    '-f', 'image2pipe', '-framerate', String(FPS), '-i', 'pipe:0',
    '-i', trackFile,
    '-filter_complex',
    `[0:v]scale=${OUT_W}:${OUT_H}:flags=lanczos,format=yuv420p[v];[1:a]apad[a]`,
    '-map', '[v]', '-map', '[a]',
    // medium 이상으로 올리면 인코딩이 캡처보다 느려져 파이프가 막힌다. crf 18 이면 차이도 안 보인다
    '-c:v', 'libx264', '-preset', QUICK ? 'ultrafast' : 'medium',
    '-crf', QUICK ? '30' : '18',
    '-c:a', 'aac', '-b:a', '192k',
    '-t', DUR,
    '-movflags', '+faststart',
    outFile
  ], { stdio: ['pipe', 'inherit', 'inherit'] });

  const write = buf => new Promise(res => ff.stdin.write(buf) ? res() : ff.stdin.once('drain', res));

  const t0 = Date.now();
  for (let i = 0; i < nFrames; i++) {
    await cdp.evaluate(`window.seek(${(i / FPS * 1000).toFixed(3)})`);
    const s = await cdp.send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false });
    await write(Buffer.from(s.data, 'base64'));
    if (i % 30 === 0 || i === nFrames - 1) {
      const el = (Date.now() - t0) / 1000;
      const eta = i ? (el / i * (nFrames - i)) : 0;
      process.stdout.write(`\r  ${i + 1}/${nFrames}  ${(i / nFrames * 100).toFixed(1)}%  경과 ${el.toFixed(0)}s  남음 ${eta.toFixed(0)}s   `);
    }
  }
  ff.stdin.end();
  await new Promise((res, rej) => ff.on('close', c => c === 0 ? res() : rej(new Error(`ffmpeg 종료 ${c}`))));

  const size = fs.statSync(outFile).size;
  console.log(`\n\n완료  ${path.relative(process.cwd(), outFile)}`);
  console.log(`      ${(nFrames / FPS).toFixed(1)}초 · ${OUT_W}×${OUT_H} · ${FPS}fps · ${(size / 1048576).toFixed(1)}MB`);
} catch (e) {
  console.error('\n🔴 ' + e.message);
  process.exitCode = 1;
} finally { close(); }
