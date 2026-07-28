import { span, ease, lerp, tone, rnd, fitCanvas } from '../lib.js';

/* title — 훅 문장, 그리고 마지막 예고(mood:"outro") */
export default {
  build(root, { spec, scene }) {
    if (spec.mood === 'outro') {
      root.innerHTML = `<canvas></canvas>`;
      return;
    }
    root.innerHTML = `<div class="hookbig"></div>`;
    root.querySelector('.hookbig').textContent = scene.hook;
  },

  draw(root, { spec, p, t, scene }) {
    if (spec.mood !== 'outro') {
      // 한 글자씩 밝아진다 (시간의 함수 — 애니메이션 아님)
      const el = root.querySelector('.hookbig');
      const k = span(p, 0.02, 0.42);
      el.style.opacity = 0.15 + 0.85 * ease(k);
      el.style.letterSpacing = lerp(-0.06, -0.035, ease(k)) + 'em';
      return;
    }

    // outro — 마을이 그려지고, 그 위로 취소선이 그어진다
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const cx = w / 2, cy = h / 2;

    const appear = ease(span(p, 0.05, 0.45));
    for (let i = 0; i < 14; i++) {
      const a = rnd(i * 7 + 3) * Math.PI * 2;
      const r = (0.25 + rnd(i * 13 + 5) * 0.62) * Math.min(w, h) * 0.44;
      const x = cx + Math.cos(a) * r, y = cy + Math.sin(a) * r * 0.72;
      const on = i / 14 < appear;
      if (!on) continue;
      ctx.fillStyle = i % 3 === 0 ? tone('warm') : tone('grid');
      ctx.beginPath(); ctx.arc(x, y, i % 3 === 0 ? 7 : 9, 0, 7); ctx.fill();
    }

    const cut = ease(span(p, 0.5, 0.9));
    if (cut > 0) {
      ctx.strokeStyle = tone('hot'); ctx.lineWidth = 3; ctx.lineCap = 'round';
      ctx.beginPath();
      ctx.moveTo(w * 0.06, h * 0.78);
      ctx.lineTo(lerp(w * 0.06, w * 0.94, cut), lerp(h * 0.78, h * 0.2, cut));
      ctx.stroke();
    }
  }
};
