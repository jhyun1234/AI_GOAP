import {
  disp, span, ease, easeOut, lerp, tone, rnd, fitCanvas, mkCanvas, roundRect } from '../../../engine/lib.js';

/* title — 훅, 그리고 마지막 예고(mood:"outro")
   가이드(youtube-editor/SKILL.md '인트로 세그먼트 규칙'): 인트로가 정적 텍스트면 FAIL.
   다만 쇼츠는 첫 1초의 문구가 스크롤을 멈추게 하므로 훅 문구는 남긴다.
   대신 그 아래에 '의미 있는 모션'을 깐다 — 게이지가 4,096까지 치솟다가
   끝내 목표선에 못 닿고 멈춘다. 이 편의 내용 자체가 그 그림이다. */

export default {
  build(root, { spec, scene }) {
    if (spec.mood === 'outro') { root.innerHTML = ''; mkCanvas(root); return; }
    root.innerHTML = `<div class="hookwrap"><div class="hookbig"></div><canvas></canvas></div>`;
    const w = root.querySelector('.hookwrap');
    w.style.cssText = 'height:100%;display:flex;flex-direction:column;justify-content:center;gap:14px';
    root.querySelector('.hookbig').textContent = scene.hook;
    root.querySelector('.hookbig').style.height = 'auto';
    root.querySelector('canvas').style.cssText = 'width:100%;height:34%;display:block';
  },

  draw(root, { spec, p, cue, scene }) {
    if (spec.mood !== 'outro') {
      const el = root.querySelector('.hookbig');
      const k = ease(cue(0, 0.3, 0.3));
      el.style.opacity = 0.2 + 0.8 * k;
      el.style.letterSpacing = lerp(-0.06, -0.035, k) + 'em';

      // 게이지 — 빠르게 차오르다 목표선 앞에서 멈춘다
      const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
      const y = h * 0.56, barH = 14, goalX = w * 0.93;
      const q = easeOut(cue(0, 0.05, 0.92));
      const reach = q * goalX * 0.88;               // 끝내 못 닿는다

      ctx.fillStyle = tone('track');
      roundRect(ctx, 0, y, w, barH, barH / 2); ctx.fill();
      ctx.fillStyle = tone('ink');
      roundRect(ctx, 0, y, Math.max(barH, reach), barH, barH / 2); ctx.fill();

      // 목표선 — 닿지 못한 자리
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(goalX, y - 12); ctx.lineTo(goalX, y + barH + 12); ctx.stroke();
      ctx.fillStyle = tone('accent'); ctx.font = disp(700, 11);
      ctx.textAlign = 'right'; ctx.fillText('정답', goalX - 8, y - 18);

      // 카운터
      ctx.textAlign = 'left';
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
      ctx.fillText('탐색한 후보', 0, y - 18);
      ctx.fillStyle = tone('ink');
      ctx.font = disp(900, 30);
      ctx.fillText(Math.round(q * 4096).toLocaleString(), 0, y + barH + 40);
      return;
    }

    // outro — 마을이 그려지고, 그 위로 취소선이 그어진다
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const cx = w / 2, cy = h / 2;
    const appear = ease(cue(0, 0.35, 0.42));

    for (let i = 0; i < 14; i++) {
      if (i / 14 >= appear) continue;
      const a = rnd(i * 7 + 3) * Math.PI * 2;
      const r = (0.25 + rnd(i * 13 + 5) * 0.62) * Math.min(w, h) * 0.44;
      const x = cx + Math.cos(a) * r, y = cy + Math.sin(a) * r * 0.72;
      ctx.lineWidth = 3;
      if (i % 3 === 0) { ctx.fillStyle = tone('ink'); ctx.beginPath(); ctx.arc(x, y, 9, 0, 7); ctx.fill(); }
      else { ctx.strokeStyle = tone('track'); ctx.beginPath(); ctx.arc(x, y, 10, 0, 7); ctx.stroke(); }
    }

    const cut = ease(cue(0, -0.55, 0.95));   // 문장 후반, "지워집니다" 에 맞춰 그어진다
    if (cut > 0) {
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 5; ctx.lineCap = 'round';
      ctx.beginPath();
      ctx.moveTo(w * 0.06, h * 0.78);
      ctx.lineTo(lerp(w * 0.06, w * 0.94, cut), lerp(h * 0.78, h * 0.2, cut));
      ctx.stroke();
    }
  }
};
