import fs from 'fs';
import { openEngine } from './lib-node.mjs';

const EP = process.argv[2] || 'ep05s2';
const { cdp, close } = await openEngine(EP, { quiet: true });

// 1) 타임라인 덤프
const tl = await cdp.evaluate(`(() => {
  const out = [];
  const els = [...document.querySelectorAll('.shot')];
  return JSON.stringify({ TOTAL: window.TOTAL });
})()`);
console.log('TOTAL', tl);

// engine 내부 shots 는 모듈 스코프라 접근 불가 → seek 으로 경계를 찾는다
const bounds = await cdp.evaluate(`(() => {
  const N = Math.ceil(window.TOTAL);
  let prev = null; const marks = [];
  for (let ms = 0; ms <= N; ms += 1) {
    window.seek(ms);
    const id = document.querySelector('.shot.on')?.dataset.id;
    if (id !== prev) { marks.push([id, ms]); prev = id; }
  }
  return JSON.stringify(marks);
})()`);
console.log('SHOT_STARTS', bounds);

// 자막 경계
const caps = await cdp.evaluate(`(() => {
  const N = Math.ceil(window.TOTAL);
  let prev = null; const marks = [];
  for (let ms = 0; ms <= N; ms += 1) {
    window.seek(ms);
    const txt = document.getElementById('cap').textContent;
    if (txt !== prev) { marks.push([txt.replace(/\\n/g,' / '), ms]); prev = txt; }
  }
  return JSON.stringify(marks);
})()`);
console.log('CAP_STARTS', caps);

// 정적 구간 m 덤프 (check.mjs 216~ 와 같은 식, 5fps)
const stat = await cdp.evaluate(`(async () => {
  const FPS = 5, N = Math.max(2, Math.floor(window.TOTAL/1000*FPS));
  const rows = [];
  let prev = null, prevId = null;
  for (let i = 0; i < N; i++) {
    window.seek(i / FPS * 1000);
    const el = document.querySelector('.shot.on');
    const id = el?.dataset.id; if (!id) continue;
    const cv = el.querySelector('canvas');
    if (!cv || !cv.width) { prev = null; prevId = id; continue; }
    const g = cv.getContext('2d');
    const d = g.getImageData(0,0,cv.width,cv.height).data;
    const L = new Uint8Array((d.length>>2));
    for (let p = 0; p < d.length; p += 4) {
      L[p>>2] = (((d[p]*77+d[p+1]*151+d[p+2]*28)>>8) * d[p+3] / 255) | 0;
    }
    let m = null;
    if (prev && prevId === id && prev.length === L.length) {
      let s = 0; for (let k = 0; k < L.length; k++) s += Math.abs(L[k]-prev[k]);
      m = s / L.length / 255;
    }
    rows.push([id, +(i/FPS).toFixed(2), m === null ? null : +m.toFixed(6)]);
    prev = L; prevId = id;
  }
  return JSON.stringify(rows);
})()`);
fs.writeFileSync('/tmp/claude-0/-home-user-AI-GOAP/21ce8092-076c-517f-a526-bcc57158c98e/scratchpad/static.json', stat);
console.log('static rows written');

await close();
