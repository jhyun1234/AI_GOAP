/* render.mjs 와 check.mjs 가 함께 쓰는 조각.
   둘 다 "헤드리스 크로미움을 띄우고 엔진을 로드해 seek 하며 프레임을 읽는다"가 필요하다.
   같은 코드를 두 벌 두면 한쪽만 고쳐지는 날이 온다. */
import fs from 'fs';
import path from 'path';
import os from 'os';
import { spawn } from 'child_process';
import { fileURLToPath } from 'url';

export const ROOT = path.dirname(fileURLToPath(import.meta.url));

/* ── 회차 폴더 ──────────────────────────────────────
   한 회차에 필요한 것이 한 자리에 모인다: 대본 · **그 회차 전용 그림** · 산출물 · 검수 기록.

   🔴 kinds 가 회차 안에 있는 것이 핵심이다. 전에는 engine/kinds/ 공용 라이브러리였고,
   그래서 회차마다 같은 그림이 돌아왔다 — 구조가 돌려막기를 기본값으로 만들고 있었다.
   지금은 회차가 자기 그림을 소유한다. 다른 회차 것을 쓰려면 파일을 복사해 와야 하고,
   그 복사가 곧 "이걸 또 쓴다"는 의식적인 결정이 된다.
   공유하는 것은 도구뿐이다(lib.js 의 팔레트·이징·캔버스 헬퍼) — 그림이 아니라 붓이다. */
export const epDir = ep => path.join(ROOT, 'episodes', ep);
export const epScene = ep => path.join(epDir(ep), 'scene.json');
export const epBuild = (ep, ...rest) => path.join(epDir(ep), 'build', ...rest);

export const STAGE_W = 392;                     // 디자인 기준 폭 (style.css .stage 와 같아야 한다)
export const OUT_W = 1080, OUT_H = 1920;
export const DSF = OUT_W / STAGE_W;             // 2.7551…
export const VIEW_H = Math.round(OUT_H / DSF);  // 697

export const sleep = ms => new Promise(r => setTimeout(r, ms));

function which(names) {
  const dirs = (process.env.PATH || '').split(path.delimiter);
  for (const n of names) for (const d of dirs) {
    const p = path.join(d, n);
    if (d && fs.existsSync(p)) return p;
  }
  return null;
}

/** winget 으로 깔린 ffmpeg 은 PATH 에 없다 — 패키지 폴더를 훑어 찾는다 */
export function findFfmpeg() {
  const direct = which(['ffmpeg.exe', 'ffmpeg']);
  if (direct) return direct;
  const base = path.join(process.env.LOCALAPPDATA || '', 'Microsoft/WinGet/Packages');
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

export function findBrowser() {
  /* 🔴 클라우드 루틴(scriptBy=cloud)은 리눅스 컨테이너에서 돈다. 거기엔 크롬이 /usr/bin 에
     없고 Playwright 가 받아 둔 것만 있어서, 이 목록이 윈도우 경로 넷 + /usr/bin 셋뿐이던
     동안은 검수팀이 check.mjs 를 아예 못 돌렸다. env 로 직접 지정하는 길을 먼저 두고,
     그다음에 Playwright 가 쓰는 자리를 본다. glob 대신 디렉터리를 훑는 것은 버전 번호가
     붙기 때문이다(chromium-1194). */
  const envPath = process.env.SCENE_VIDEO_CHROME;
  if (envPath && fs.existsSync(envPath)) return envPath;

  const pwRoot = process.env.PLAYWRIGHT_BROWSERS_PATH || '/opt/pw-browsers';
  const pwCandidates = [];
  try {
    for (const dir of fs.readdirSync(pwRoot)) {
      if (!/^chromium/.test(dir)) continue;
      pwCandidates.push(
        path.join(pwRoot, dir, 'chrome-linux', 'chrome'),
        path.join(pwRoot, dir, 'chrome-linux', 'headless_shell'));
    }
  } catch { /* 디렉터리가 없으면 후보가 없을 뿐이다 */ }

  return [
    'C:/Program Files/Google/Chrome/Application/chrome.exe',
    'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe',
    'C:/Program Files/Microsoft/Edge/Application/msedge.exe',
    'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
    '/usr/bin/google-chrome', '/usr/bin/chromium', '/usr/bin/chromium-browser',
    ...pwCandidates
  ].find(p => fs.existsSync(p)) || null;
}

export class CDP {
  constructor(ws) { this.ws = ws; this.id = 0; this.waiting = new Map(); this.session = null; }
  static async connect(url) {
    const ws = new WebSocket(url);
    await new Promise((res, rej) => { ws.onopen = res; ws.onerror = () => rej(new Error('CDP 연결 실패')); });
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

/** 정적 서버 + 헤드리스 브라우저를 띄우고 엔진이 준비될 때까지 기다린다.
 *  반환한 close() 를 반드시 부를 것 — 안 부르면 크롬과 서버가 남는다. */
export async function openEngine(EP, { quiet = false } = {}) {
  const browser = findBrowser();
  if (!browser) throw new Error('크롬/엣지를 못 찾았다');

  const PORT = 4200 + (EP.length % 50) + (process.pid % 30);
  const server = spawn(process.execPath, [path.join(ROOT, 'serve.js')],
    { env: { ...process.env, PORT: String(PORT) }, stdio: 'ignore' });
  const profile = fs.mkdtempSync(path.join(os.tmpdir(), 'scenevid-'));
  /* 🔴 클라우드 컨테이너는 root 로 돈다. 크롬은 root 에서 --no-sandbox 없이는 아예 안 뜨고
     (zygote_host_impl_linux.cc: "Running as root without --no-sandbox is not supported"),
     DevToolsActivePort 가 안 생겨 '디버그 포트를 못 찾았다'로만 보인다. getuid 는 윈도우에
     없으므로 로컬 실행에는 이 플래그가 붙지 않는다 — 샌드박스를 끄는 범위를 딱 그만큼으로
     묶는다. */
  const rootless = typeof process.getuid === 'function' && process.getuid() === 0
    ? ['--no-sandbox', '--disable-dev-shm-usage'] : [];
  const chrome = spawn(browser, [
    '--headless=new', '--remote-debugging-port=0', `--user-data-dir=${profile}`,
    ...rootless,
    '--no-first-run', '--no-default-browser-check', '--disable-extensions',
    '--hide-scrollbars', '--mute-audio', '--force-device-scale-factor=1',
    // 서브픽셀 렌더링을 끈다 — 머신마다 다른 색 테두리가 생기고 3색 점검에도 걸린다
    '--disable-lcd-text', '--font-render-hinting=none',
    'about:blank'
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
      { width: STAGE_W, height: VIEW_H, deviceScaleFactor: DSF, mobile: false });
    await b.send('Page.navigate', { url: `http://127.0.0.1:${PORT}/engine/?ep=${EP}&render=1` });

    if (!quiet) process.stdout.write('준비 중');
    let ready = false;
    for (let i = 0; i < 600 && !ready; i++) {
      await sleep(100);
      try { ready = await b.evaluate('!!window.__ready'); } catch { }
      if (!quiet && i % 10 === 0) process.stdout.write('.');
    }
    if (!ready) throw new Error('엔진이 준비되지 않았다 (window.__ready)');
    if (!quiet) console.log(' 완료');

    return { cdp: b, close: () => { b.close(); close(); } };
  } catch (e) { close(); throw e; }
}

/** PNG 한 장을 Buffer 로 */
export async function shot(cdp, ms) {
  await cdp.evaluate(`window.seek(${Number(ms).toFixed(3)})`);
  const s = await cdp.send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false });
  return Buffer.from(s.data, 'base64');
}
