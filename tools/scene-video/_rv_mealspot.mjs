import { openEngine } from './lib-node.mjs';
const { cdp, close } = await openEngine('ep04s2', { quiet: true });
const r = await cdp.evaluate(`(async ()=>{
  const lib = await import('/engine/lib.js');
  const {ease, clamp, lerp} = lib;
  // 라벨 실측 폭
  const cv = document.querySelector('.shot[data-id="S6"] canvas');
  const ctx = cv.getContext('2d');
  ctx.font = lib.disp(700, 10);
  const m = ctx.measureText('모닥불');
  const halfW = m.width/2;
  const asc = m.actualBoundingBoxAscent, desc = m.actualBoundingBoxDescent;
  // 샷 rel 타임라인
  const shots = [...document.querySelectorAll('.shot')].map(e=>e.dataset.id);
  const out = {label:{w:m.width, halfW, asc, desc}, samples:[]};
  const hx=262, hy=158, midX=172, nV=4;
  const L = {x0: hx-halfW, x1: hx+halfW, y0: hy+30-asc, y1: hy+30+desc};
  out.labelBox = L;
  // rel 타이밍은 6.7cps 산정치로 따로 계산한다(여기선 mk 를 0..1 로 훑는다)
  for (let step=0; step<=200; step++){
    const mk = step/200;
    const p1 = ease(clamp(mk/0.55)), p2 = ease(clamp((mk-0.55)/0.45));
    let worst = null;
    for (let i=0;i<nV;i++){
      const a = -Math.PI/2 + (i/nV)*Math.PI*2 + Math.PI/4;
      const sx = midX+16+(i%2)*18, sy = 210+Math.floor(i/2)*26;
      const tx = hx+2, ty = hy+34;
      const rx = hx+Math.cos(a)*42, ry = hy+Math.sin(a)*42*0.86;
      const ax = lerp(lerp(sx,tx,p1), rx, p2);
      const ay = lerp(lerp(sy,ty,p1), ry, p2);
      // 링 = 반지름 9, lineWidth 3 → 바깥 10.5 / 안쪽 7.5
      // 라벨 박스와 링 획(도넛)의 겹침 = 박스 최근접점 거리가 [7.5,10.5] 에 들거나 박스가 링을 포함
      const cxp = Math.max(L.x0, Math.min(ax, L.x1));
      const cyp = Math.max(L.y0, Math.min(ay, L.y1));
      const dNear = Math.hypot(ax-cxp, ay-cyp);
      // 박스에서 가장 먼 점
      const fx = (Math.abs(ax-L.x0) > Math.abs(ax-L.x1)) ? L.x0 : L.x1;
      const fy = (Math.abs(ay-L.y0) > Math.abs(ay-L.y1)) ? L.y0 : L.y1;
      const dFar = Math.hypot(ax-fx, ay-fy);
      const hit = (dNear <= 10.5 && dFar >= 7.5);
      const rec = {i, ax:+ax.toFixed(1), ay:+ay.toFixed(1), dNear:+dNear.toFixed(2), hit};
      if (hit && (!worst || rec.dNear < worst.dNear)) worst = rec;
    }
    out.samples.push({mk:+mk.toFixed(3), p1:+p1.toFixed(3), p2:+p2.toFixed(3), hit: !!worst, worst});
  }
  return JSON.stringify(out);
})()`);
console.log(r);
await close();
