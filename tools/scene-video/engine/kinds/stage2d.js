import { clamp, lerp, ease, easeOut, frac, span, fitCanvas, mkCanvas, tone } from '../lib.js';

/* stage2d — 2D 무대 위의 동그라미.
   위치는 전부 t 의 함수다. 궤적(trail)도 과거 시각을 다시 계산해서 그린다.
   그래서 어느 시각으로 뛰어도 같은 그림이 나온다. */

const PAD = 14;
const toPx = (a, w, h) => [PAD + a[0] / 100 * (w - PAD * 2), PAD + a[1] / 100 * (h - PAD * 2)];

/** 배우의 시각 t(초)·진행 p 에서의 무대 좌표(0~100) */
function pos(a, t, p) {
  if (a.oscillate) {
    const o = a.oscillate, ph = o.phase || 0;
    const u = frac(t / o.period + ph);
    const out = 0.30, hold = out + (o.stall ?? 0.4) * 0.35;
    let k;
    if (u < out) k = ease(u / out);
    else if (u < hold) k = 1 + Math.sin(t * 16 + ph * 9) * 0.014;   // 멈칫거림
    else k = 1 - ease((u - hold) / (1 - hold));
    return [lerp(a.at[0], o.to[0], k), lerp(a.at[1], o.to[1], k)];
  }
  if (a.walk) {
    const pts = [a.at, ...a.walk], seg = pts.length - 1;
    const q = easeOut(span(p, 0.12, 0.86)) * seg;
    const i = Math.min(seg - 1, Math.floor(q)), f = q - i;
    return [lerp(pts[i][0], pts[i + 1][0], f), lerp(pts[i][1], pts[i + 1][1], f)];
  }
  return a.at;
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, p, t }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const actors = spec.actors || [];

    // 안개 걷기 — 지나간 자리가 밝아진다
    if (spec.fog?.reveal) {
      ctx.save();
      for (const a of actors) {
        for (let s = 0; s <= 24; s++) {
          const tt = t * (s / 24);
          const [x, y] = toPx(pos(a, tt, p * (s / 24)), w, h);
          const g = ctx.createRadialGradient(x, y, 0, x, y, 46);
          g.addColorStop(0, 'rgba(79,182,168,.055)');
          g.addColorStop(1, 'rgba(79,182,168,0)');
          ctx.fillStyle = g; ctx.beginPath(); ctx.arc(x, y, 46, 0, 7); ctx.fill();
        }
      }
      ctx.restore();
    }

    // 기지 등 소품
    for (const pr of spec.props || []) {
      const [x, y] = toPx(pr.at, w, h);
      ctx.strokeStyle = '#2C3242'; ctx.lineWidth = 1.5;
      ctx.strokeRect(x - 15, y - 12, 30, 24);
      ctx.fillStyle = '#15181F'; ctx.fillRect(x - 15, y - 12, 30, 24);
      if (pr.label) {
        ctx.fillStyle = tone('dim'); ctx.font = '600 9px Consolas, monospace';
        ctx.textAlign = 'center'; ctx.fillText(pr.label, x, y + 26);
      }
    }

    // 궤적 — 과거 1.1초를 되짚어 그린다
    const TRAIL = 1.1, N = 16;
    actors.forEach((a, ai) => {
      ctx.lineWidth = 2; ctx.lineCap = 'round';
      for (let s = N; s > 0; s--) {
        const t0 = Math.max(0, t - TRAIL * (s / N));
        const t1 = Math.max(0, t - TRAIL * ((s - 1) / N));
        const p0 = clamp(p - (p * TRAIL * (s / N)) / Math.max(t, .001), 0, 1);
        const [x0, y0] = toPx(pos(a, t0, p0), w, h);
        const [x1, y1] = toPx(pos(a, t1, p0), w, h);
        ctx.strokeStyle = tone(a.color) + Math.round(6 + (1 - s / N) * 40).toString(16).padStart(2, '0');
        ctx.beginPath(); ctx.moveTo(x0, y0); ctx.lineTo(x1, y1); ctx.stroke();
      }
    });

    // 배우
    actors.forEach(a => {
      const [x, y] = toPx(pos(a, t, p), w, h);
      const c = tone(a.color);
      ctx.fillStyle = c + '26';
      ctx.beginPath(); ctx.arc(x, y, 17, 0, 7); ctx.fill();
      ctx.fillStyle = c;
      ctx.beginPath(); ctx.arc(x, y, 8.5, 0, 7); ctx.fill();
    });
  }
};
