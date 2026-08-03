import { openEngine } from './lib-node.mjs';

const { cdp, close } = await openEngine('ep05s2', { quiet: true });

const out = await cdp.evaluate(`(() => {
  window.seek(12000);
  const el = document.querySelector('.shot.on');
  const cv = el.querySelector('canvas');
  const ctx = cv.getContext('2d');
  const r = cv.getBoundingClientRect();
  const sx = cv.width / r.width;   // 백킹스토어 배율
  // p0gate 의 좌표계는 CSS px (fitCanvas 가 scale 을 걸어 둔다)
  const res = {};
  // 라벨 실측
  const fitW = (txt, w0, start, max, min) => { let fs=start; ctx.font = '700 '+fs+'px Pretendard'; return fs; };
  ctx.font = '800 11.5px Pretendard';
  const m = ctx.measureText('실패 쿨다운');
  res.label = { w: m.width, asc: m.actualBoundingBoxAscent, desc: m.actualBoundingBoxDescent };
  // S10 hook / criteria
  window.seek(63500);
  const el2 = document.querySelector('.shot.on');
  const ctx2 = el2.querySelector('canvas').getContext('2d');
  ctx2.font = '900 16px Pretendard';
  const m2 = ctx2.measureText('집');
  res.jip = { w: m2.width, asc: m2.actualBoundingBoxAscent, desc: m2.actualBoundingBoxDescent };
  ctx2.font = '900 15px Pretendard';
  const m3 = ctx2.measureText('지어 놓은 집이 주민의 길을 막았다');
  res.hook = { w: m3.width, asc: m3.actualBoundingBoxAscent, desc: m3.actualBoundingBoxDescent };
  res.scale = sx;
  res.cssW = r.width;
  return JSON.stringify(res);
})()`);
console.log(out);
await close();
