import { span, ease, clamp, rnd, fitCanvas, mkCanvas, tone, roundRect } from '../lib.js';

/* search — 탐색이 격자 위로 번지다 예산에 막히는 그림.
   핵심은 "많이 뒤졌다"가 아니라 "그 옆에 짧은 정답이 그대로 있다"는 대비다.
   그래서 정답 사슬은 처음부터 화면 아래에 놓여 있고, 끝에 밝아진다. */

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, p }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const [gx, gy] = spec.grid || [24, 30];
    const budget = spec.budget || 4096;

    const gridH = h * 0.62;
    const cw = w / gx, ch = gridH / gy;
    const cx = w / 2, cy = gridH / 2;
    const maxR = Math.hypot(cx, cy);

    // 파면 — 중심에서 바깥으로 번진다
    const grow = ease(span(p, 0.05, 0.62));
    const R = grow * maxR * 1.02;

    let lit = 0;
    for (let j = 0; j < gy; j++) {
      for (let i = 0; i < gx; i++) {
        const x = i * cw, y = j * ch;
        const d = Math.hypot(x + cw / 2 - cx, y + ch / 2 - cy);
        const jitter = rnd(i * 131 + j * 17) * 26;
        const on = d + jitter < R;
        if (on) lit++;
        ctx.fillStyle = on
          ? (d + jitter > R - 22 ? '#7A2338' : '#2A1620')
          : '#16191F';
        ctx.fillRect(x + .6, y + .6, cw - 1.2, ch - 1.2);
      }
    }

    // 예산 카운터
    const n = Math.round(clamp(lit / (gx * gy)) * budget);
    const done = p > 0.62;
    ctx.textAlign = 'left';
    ctx.fillStyle = tone('dim'); ctx.font = '600 9.5px Consolas, monospace';
    ctx.fillText('탐색한 후보', 0, gridH + 20);
    ctx.fillStyle = done ? tone('hot') : tone('ink');
    ctx.font = '800 30px Pretendard, "Malgun Gothic", sans-serif';
    ctx.fillText((done ? budget : n).toLocaleString(), 0, gridH + 50);

    if (done) {
      const flash = 0.55 + 0.45 * Math.abs(Math.sin(p * 22));
      ctx.strokeStyle = tone('hot'); ctx.globalAlpha = flash;
      ctx.lineWidth = 2; ctx.strokeRect(1, 1, w - 2, gridH - 2);
      ctx.globalAlpha = 1;
      ctx.textAlign = 'right';
      ctx.fillStyle = tone('hot'); ctx.font = '700 11px Consolas, monospace';
      ctx.fillText(spec.verdict || 'NoSolution', w, gridH + 50);
      ctx.textAlign = 'left';
    }

    // 정답 사슬 — 내내 거기 있었다
    const chain = spec.answer || [];
    const reveal = ease(span(p, 0.66, 0.92));
    const by = h - 20;
    ctx.font = '600 9.5px Consolas, monospace';
    ctx.fillStyle = reveal > 0 ? tone('cool') : tone('dim');
    ctx.fillText('정답', 0, by - 26);

    let x = 0;
    chain.forEach((s, i) => {
      const bw = ctx.measureText(s).width + 16;
      const hot = reveal > i / chain.length;
      ctx.strokeStyle = hot ? tone('cool') : '#262B36';
      ctx.fillStyle = hot ? 'rgba(79,182,168,.13)' : 'transparent';
      roundRect(ctx, x, by - 18, bw, 20, 3); ctx.fill(); ctx.stroke();
      ctx.fillStyle = hot ? tone('cool') : tone('dim');
      ctx.fillText(s, x + 8, by - 4);
      x += bw + 4;
      if (i < chain.length - 1) {
        ctx.fillStyle = '#3A404E'; ctx.fillText('›', x - 2, by - 4); x += 8;
      }
    });
  }
};
