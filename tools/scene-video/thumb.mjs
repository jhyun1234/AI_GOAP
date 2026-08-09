/* 롱폼 썸네일 합성 (2026-08-09 사용자 확정 — 게임 스틸 + 큰 카피).
   사용: node thumb.mjs <ep> --bg <배경 PNG 경로> --title "큰 줄|둘째 줄"
                        [--kicker "위 작은 줄"] [--out thumb-a.png]

   방식 ①의 구현: 촬영 수확물의 실제 프레임 위에 3색 팔레트 카피를 얹는다.
   렌더러와 같은 크로미움 경로(의존성 0)·같은 폰트(Pretendard 임베드)를 쓴다.
   유튜브 규격 1280×720 · 2MB 미만. 업로드는 사람 몫 — 산출물은 build/ 에.
   카피는 대본 산출물이다(문법 문서 §1 챕터 제목과 같은 원칙) — 본편이 이행하지
   않는 약속을 쓰면 검수 반려 대상. */
import fs from 'fs';
import path from 'path';
import os from 'os';
import { spawn } from 'child_process';
import { ROOT, CDP, findBrowser, sleep } from './lib-node.mjs';

const argv = process.argv.slice(2);
const EP = argv.find(a => !a.startsWith('--'));
const flag = (n, d) => {
  const i = argv.indexOf('--' + n);
  return i >= 0 && argv[i + 1] && !argv[i + 1].startsWith('--') ? argv[i + 1] : d;
};
const BG = flag('bg', null);
const TITLE = flag('title', null);
const KICKER = flag('kicker', '');
const OUT = flag('out', 'thumbnail.png');
if (!EP || !BG || !TITLE) {
  console.log('사용: node thumb.mjs <ep> --bg <png> --title "큰 줄|둘째 줄" [--kicker "..."] [--out f.png]');
  process.exit(1);
}

const buildDir = path.join(ROOT, 'episodes', EP, 'build');
fs.mkdirSync(buildDir, { recursive: true });
// 배경을 build 로 복사해 서버 경로로 쓴다 (D: 는 서버 루트 밖이다)
fs.copyFileSync(BG, path.join(buildDir, '__thumb_bg.png'));

const [t1, t2 = ''] = TITLE.split('|');
const html = `<!doctype html><meta charset="utf-8">
<style>
@font-face{font-family:"Pretendard";src:url("/engine/fonts/PretendardVariable.woff2") format("woff2-variations");font-weight:45 920;font-display:block}
*{margin:0;padding:0;box-sizing:border-box}
body{width:1280px;height:720px;overflow:hidden;position:relative;background:#000;font-family:"Pretendard",sans-serif}
img.bg{position:absolute;inset:0;width:100%;height:100%;object-fit:cover}
/* 카피가 앉는 왼쪽 아래를 어둡게 — 게임 화면(밝은 초록) 위 흰 글자는 스크림 없이 안 읽힌다 */
.scrim{position:absolute;inset:0;background:
  linear-gradient(75deg, rgba(0,0,0,.82) 0%, rgba(0,0,0,.55) 34%, rgba(0,0,0,0) 62%)}
.edge{position:absolute;inset:0;border:10px solid #00FF88}
.box{position:absolute;left:56px;bottom:52px;right:420px}
.kicker{display:inline-block;font-weight:800;font-size:34px;letter-spacing:.04em;
  color:#000;background:#00FF88;padding:8px 18px 10px;border-radius:6px;margin-bottom:22px}
h1{font-weight:900;font-size:96px;line-height:1.08;letter-spacing:-.035em;color:#FFF;
  text-shadow:0 6px 26px rgba(0,0,0,.9)}
h1 em{font-style:normal;color:#00FF88}
h1 .l2{display:block}
</style>
<img class="bg" src="__thumb_bg.png">
<div class="scrim"></div>
<div class="edge"></div>
<div class="box">
  ${KICKER ? `<div class="kicker">${KICKER}</div>` : ''}
  <h1>${t1}${t2 ? `<span class="l2">${t2}</span>` : ''}</h1>
</div>
<script>
(async () => {
  await Promise.all(['800 34px Pretendard','900 96px Pretendard'].map(f => document.fonts.load(f)));
  await document.fonts.ready;
  const img = document.querySelector('img.bg');
  if (!img.complete) await new Promise(r => img.onload = r);
  window.__ready = true;
})();
</script>`;
fs.writeFileSync(path.join(buildDir, '__thumb.html'), html);

const browser = findBrowser();
if (!browser) { console.error('🔴 크롬/엣지를 못 찾았다'); process.exit(1); }
const PORT = 4600 + (process.pid % 90);
const server = spawn(process.execPath, [path.join(ROOT, 'serve.js')],
  { env: { ...process.env, PORT: String(PORT) }, stdio: 'ignore' });
const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'scenethumb-'));
const chrome = spawn(browser, [
  '--headless=new', '--remote-debugging-port=0', `--user-data-dir=${profile}`,
  '--no-first-run', '--no-default-browser-check', '--disable-extensions',
  '--hide-scrollbars', '--mute-audio', '--force-device-scale-factor=1',
  '--disable-lcd-text', '--font-render-hinting=none', 'about:blank',
], { stdio: ['ignore', 'ignore', 'pipe'] });
const close = () => {
  try { chrome.kill(); } catch { }
  try { server.kill(); } catch { }
  try { fs.rmSync(profile, { recursive: true, force: true }); } catch { }
};

try {
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
  await b.send('Page.enable');
  await b.send('Runtime.enable');
  await b.send('Emulation.setDeviceMetricsOverride',
    { width: 1280, height: 720, deviceScaleFactor: 1, mobile: false });
  await b.send('Page.navigate',
    { url: `http://127.0.0.1:${PORT}/episodes/${EP}/build/__thumb.html` });
  let ready = false;
  for (let i = 0; i < 100 && !ready; i++) {
    await sleep(100);
    try { ready = await b.evaluate('!!window.__ready'); } catch { }
  }
  if (!ready) throw new Error('썸네일 페이지가 준비되지 않았다');
  const s = await b.send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false });
  const out = path.join(buildDir, OUT);
  fs.writeFileSync(out, Buffer.from(s.data, 'base64'));
  const kb = (fs.statSync(out).size / 1024).toFixed(0);
  console.log(`썸네일  ${path.relative(process.cwd(), out)}  (1280×720 · ${kb}KB)`);
  b.close();
} finally {
  close();
  try { fs.unlinkSync(path.join(buildDir, '__thumb.html')); } catch { }
  try { fs.unlinkSync(path.join(buildDir, '__thumb_bg.png')); } catch { }
}
