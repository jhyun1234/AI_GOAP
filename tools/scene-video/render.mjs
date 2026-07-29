/* 회차 → mp4.
   사용: node tools/scene-video/render.mjs ep01s [--fps 30] [--frames 60] [--quick]

   의존성 0 이다. 크로미움을 CDP 로 직접 몰고, PNG 를 ffmpeg 표준입력으로 흘린다.
   Playwright 를 쓰면 브라우저를 130MB 새로 받아야 하는데, 이 PC 에는 크롬도 엣지도
   이미 있고 우리가 쓰는 명령은 세 개뿐이다(화면 크기 지정 · 스크립트 실행 · 캡처).

   해상도: 무대 CSS 폭 392px × deviceScaleFactor 2.7551 = 1080. CSS 폭을 1080 으로
   키우는 게 아니다 — 글자 크기가 px 고정이라 그렇게 하면 화면 대비 글자가 2.75배
   작아진 다른 디자인이 나온다. 고해상도 화면에서 보는 것과 같은 방식으로 올린다.

   소리: build/<ep>.full.wav 를 그대로 쓰지 않는다. 그건 줄과 호흡만 이어 붙인 것이라
   샷 끝 여운(SHOT_TAIL)이 빠져 있어 뒤로 갈수록 밀린다. 자리 시간표는 엔진에게
   물어보고(window.__timeline), 그 자리에 줄별 wav 를 놓아 트랙을 새로 만든다.
*/
import fs from 'fs';
import path from 'path';
import os from 'os';
import { spawn } from 'child_process';
import { fileURLToPath } from 'url';

const ROOT = path.dirname(fileURLToPath(import.meta.url));
const argv = process.argv.slice(2);
const EP = argv.find(a => !a.startsWith('--')) || 'ep01';
const flag = (n, d) => {
  const i = argv.indexOf('--' + n);
  return i >= 0 ? (argv[i + 1] && !argv[i + 1].startsWith('--') ? argv[i + 1] : true) : d;
};
const FPS = Number(flag('fps', 30));
const MAX_FRAMES = flag('frames', 0) ? Number(flag('frames', 0)) : 0;
const QUICK = !!flag('quick', false);        // 확인용 저화질·고속 인코딩

const STAGE_W = 392;                          // 디자인 기준 폭 (style.css .stage 와 같아야 한다)
const OUT_W = 1080, OUT_H = 1920;
const DSF = OUT_W / STAGE_W;                  // 2.7551…
const VIEW_H = Math.round(OUT_H / DSF);       // 697

/* ── 외부 프로그램 찾기 ──────────────────────────── */
function which(names, extra = []) {
  const dirs = (process.env.PATH || '').split(path.delimiter);
  for (const n of names)
    for (const d of [...dirs, ...extra]) {
      const p = path.join(d, n);
      if (p && fs.existsSync(p)) return p;
    }
  return null;
}
const LOCAL = process.env.LOCALAPPDATA || '';
function findFfmpeg() {
  const direct = which(['ffmpeg.exe', 'ffmpeg']);
  if (direct) return direct;
  // winget 설치본은 PATH 에 없다
  const base = path.join(LOCAL, 'Microsoft/WinGet/Packages');
  if (!fs.existsSync(base)) return null;
  for (const pkg of fs.readdirSync(base).filter(d => /ffmpeg/i.test(d))) {
    const stack = [path.join(base, pkg)];
    while (stack.length) {
      const dir = stack.pop();
      for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
        const p = path.join(dir, e.name);
        if (e.isDirectory()) stack.push(p);
        else if (/^ffmpeg(\.exe)?$/i.test(e.name)) return p;
      }
    }
  }
  return null;
}
function findBrowser() {
  const c = [
    'C:/Program Files/Google/Chrome/Application/chrome.exe',
    'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe',
    'C:/Program Files/Microsoft/Edge/Application/msedge.exe',
    'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
    '/usr/bin/google-chrome', '/usr/bin/chromium', '/usr/bin/chromium-browser'
  ];
  return c.find(p => fs.existsSync(p)) || null;
}

/* ── wav 읽기/쓰기 ───────────────────────────────── */
function readWav(file) {
  const b = fs.readFileSync(file);
  // fmt/ data 청크를 찾아 간다 — 헤더 44바이트를 가정하면 청크가 하나만 끼어도 깨진다
  let off = 12, fmt = null, data = null;
  while (off + 8 <= b.length) {
    const id = b.toString('ascii', off, off + 4);
    const size = b.readUInt32LE(off + 4);
    if (id === 'fmt ') fmt = { format: b.readUInt16LE(off + 8), ch: b.readUInt16LE(off + 10), rate: b.readUInt32LE(off + 12), bits: b.readUInt16LE(off + 22) };
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

/* ── CDP ─────────────────────────────────────────── */
class CDP {
  constructor(ws) { this.ws = ws; this.id = 0; this.waiting = new Map(); this.session = null; }
  static async connect(url) {
    const ws = new WebSocket(url);
    await new Promise((res, rej) => { ws.onopen = res; ws.onerror = e => rej(new Error('CDP 연결 실패')); });
    const c = new CDP(ws);
    ws.onmessage = ev => {
      const m = JSON.parse(ev.data);
      if (m.id && c.waiting.has(m.id)) {
        const { res, rej } = c.waiting.get(m.id); c.waiting.delete(m.id);
        m.error ? rej(new Error(m.error.message)) : res(m.result);
      }
    };
    return c;
  }
  send(method, params = {}, sessionId = this.session) {
    const id = ++this.id;
    return new Promise((res, rej) => {
      this.waiting.set(id, { res, rej });
      this.ws.send(JSON.stringify({ id, method, params, ...(sessionId ? { sessionId } : {}) }));
    });
  }
  async evaluate(expr) {
    const r = await this.send('Runtime.evaluate', { expression: expr, awaitPromise: true, returnByValue: true });
    if (r.exceptionDetails) throw new Error(r.exceptionDetails.text + ' :: ' + expr.slice(0, 80));
    return r.result?.value;
  }
  close() { try { this.ws.close(); } catch { } }
}

const sleep = ms => new Promise(r => setTimeout(r, ms));

/* ── 본체 ────────────────────────────────────────── */
const ffmpeg = findFfmpeg();
const browser = findBrowser();
if (!ffmpeg) { console.error('ffmpeg 를 못 찾았다. winget install yt-dlp.FFmpeg'); process.exit(1); }
if (!browser) { console.error('크롬/엣지를 못 찾았다.'); process.exit(1); }
if (!fs.existsSync(path.join(ROOT, 'scenes', `${EP}.json`))) {
  console.error(`scenes/${EP}.json 이 없다`); process.exit(1);
}
console.log(`ffmpeg   ${ffmpeg}`);
console.log(`browser  ${browser}`);

const PORT = 4200 + (EP.length % 50);
const server = spawn(process.execPath, [path.join(ROOT, 'serve.js')],
  { env: { ...process.env, PORT: String(PORT) }, stdio: 'ignore' });

const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'scenevid-'));
const chrome = spawn(browser, [
  '--headless=new', '--remote-debugging-port=0', `--user-data-dir=${profile}`,
  '--no-first-run', '--no-default-browser-check', '--disable-extensions',
  '--hide-scrollbars', '--mute-audio', '--force-device-scale-factor=1',
  '--disable-lcd-text',                    // 서브픽셀 렌더링을 끈다 — 머신마다 다른 색 테두리가 생긴다
  '--font-render-hinting=none',
  'about:blank'
], { stdio: ['ignore', 'ignore', 'pipe'] });

let cdp = null, ff = null;
const cleanup = () => {
  try { cdp?.close(); } catch { }
  try { chrome.kill(); } catch { }
  try { server.kill(); } catch { }
  try { fs.rmSync(profile, { recursive: true, force: true }); } catch { }
};
process.on('exit', cleanup);
process.on('SIGINT', () => { cleanup(); process.exit(1); });

try {
  // DevTools 포트는 프로필 폴더에 적힌다
  const portFile = path.join(profile, 'DevToolsActivePort');
  let wsPath = null;
  for (let i = 0; i < 100 && !wsPath; i++) {
    await sleep(100);
    if (fs.existsSync(portFile)) {
      const [p, suffix] = fs.readFileSync(portFile, 'utf8').split('\n');
      if (p && suffix) wsPath = `ws://127.0.0.1:${p.trim()}${suffix.trim()}`;
    }
  }
  if (!wsPath) throw new Error('브라우저 디버그 포트를 못 찾았다');

  const b = await CDP.connect(wsPath);
  const { targetId } = await b.send('Target.createTarget', { url: 'about:blank' });
  const { sessionId } = await b.send('Target.attachToTarget', { targetId, flatten: true });
  b.session = sessionId;
  cdp = b;

  await b.send('Page.enable');
  await b.send('Runtime.enable');
  await b.send('Emulation.setDeviceMetricsOverride', {
    width: STAGE_W, height: VIEW_H, deviceScaleFactor: DSF, mobile: false
  });

  const url = `http://127.0.0.1:${PORT}/engine/?ep=${EP}&render=1`;
  await b.send('Page.navigate', { url });

  process.stdout.write('준비 중');
  let ready = false;
  for (let i = 0; i < 600 && !ready; i++) {
    await sleep(100);
    try { ready = await b.evaluate('!!window.__ready'); } catch { }
    if (i % 10 === 0) process.stdout.write('.');
  }
  if (!ready) throw new Error('엔진이 준비되지 않았다 (window.__ready)');
  console.log(' 완료');

  // 폰트가 폴백으로 떨어졌는지 — 여기서 걸러야 3천 프레임을 헛돌지 않는다
  const fontWarn = await b.evaluate(`(()=>{
    const c=document.createElement('canvas').getContext('2d');
    const p='4,096 무해 ABC';
    c.font='900 62px "__none__", sans-serif'; const base=c.measureText(p).width;
    c.font='900 62px Pretendard, "__none__", sans-serif';
    return Math.abs(c.measureText(p).width-base)>0.5 ? null : 'Pretendard 가 적용되지 않았다';
  })()`);
  if (fontWarn) throw new Error(`폰트: ${fontWarn}`);

  const timeline = await b.evaluate('window.__timeline()');
  const totalMs = timeline.totalMs;
  const nFrames = MAX_FRAMES || Math.floor(totalMs / 1000 * FPS);

  /* --still 12.5,30,50  → 그 시각의 프레임만 PNG 로. 4분짜리 전체 렌더 없이
     고친 샷 하나를 눈으로 확인할 때 쓴다. 여기가 실제 추출과 같은 경로라 믿을 수 있다. */
  const still = flag('still', null);
  if (still && still !== true) {
    const dir = path.join(ROOT, 'build', 'stills');
    fs.mkdirSync(dir, { recursive: true });
    for (const sec of String(still).split(',').map(Number)) {
      await b.evaluate(`window.seek(${sec * 1000})`);
      const s = await b.send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false });
      const f = path.join(dir, `${EP}-${sec}s.png`);
      fs.writeFileSync(f, Buffer.from(s.data, 'base64'));
      console.log(`스틸     ${path.relative(process.cwd(), f)}`);
    }
    process.exit(0);
  }

  // 소리 트랙
  const trackFile = path.join(ROOT, 'build', `${EP}.track.wav`);
  const tr = buildTrack(timeline, trackFile);
  console.log(`소리     ${tr.placed}줄 배치 · ${tr.seconds.toFixed(1)}s` + (tr.clipped ? ` · 🔴 잘림 ${tr.clipped}` : ''));

  // 첫 프레임으로 실제 크기 확인 — 어긋나면 스케일 필터로 맞춘다
  await b.evaluate('window.seek(0)');
  const probe = await b.send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false });
  const png = Buffer.from(probe.data, 'base64');
  const pw = png.readUInt32BE(16), ph = png.readUInt32BE(20);
  console.log(`프레임   ${pw}×${ph}` + (pw === OUT_W && ph === OUT_H ? '' : ` → ${OUT_W}×${OUT_H} 로 보정`));

  const outFile = path.join(ROOT, 'build', `${EP}${QUICK ? '.quick' : ''}.mp4`);
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
    await b.evaluate(`window.seek(${(i / FPS * 1000).toFixed(3)})`);
    const shot = await b.send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false });
    await write(Buffer.from(shot.data, 'base64'));
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
} finally {
  cleanup();
}
