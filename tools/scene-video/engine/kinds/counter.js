import {
  disp, mono,
  ease, easeOut, clamp, lerp, rnd, depthGrad, setShadow, clearShadow, GLOW_INK,
  fitCanvas, mkCanvas, tone
} from '../lib.js';

/* counter — 예산 하나가 통째로 타 없어지는 그림.
   후보 칸이 화면 전체로 번지고, 그 넓이가 곧 숫자다.

   ep01 정본의 search 가 이 일을 했지만 격자가 화면의 62% 안에 갇혀 있었고
   숫자는 34px 짜리 곁다리였다. 여기서는 순서를 뒤집는다 —
   숫자가 주인공(62px, 화면 아래 3분의 1)이고 격자가 그 근거다.

   자막 사이가 비지 않게, 번짐 가장자리는 t 로 계속 일렁인다.
   일렁임은 rnd(정수 seed) 라 결정성은 유지된다 — 같은 t 면 같은 그림. */

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, nLines }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const [gx, gy] = spec.grid || [26, 17];
    const budget = spec.budget || 4096;

    const gridH = h * 0.66;
    const cw = w / gx, ch = gridH / gy;
    const cx = w / 2, cy = gridH / 2;
    const maxR = Math.hypot(cx, cy);

    // 번짐 — 숫자를 말하는 자막에 물리되 lead 를 길게 잡아 샷 초반부터 이미 번지고 있게 한다
    const grow = ease(cue(spec.floodCue ?? 1, spec.floodLead ?? 2.6, 0.66));
    const R = grow * maxR * 1.04;
    const done = grow >= 1;

    // 가장자리 일렁임 — 12Hz 로 갱신되는 결정적 잡음. 멈춰 있는 프레임이 없게 한다
    const phase = Math.floor(t * 12);

    let lit = 0;
    for (let j = 0; j < gy; j++) {
      for (let i = 0; i < gx; i++) {
        const x = i * cw, y = j * ch;
        const d = Math.hypot(x + cw / 2 - cx, y + ch / 2 - cy);
        const jitter = rnd(i * 131 + j * 17) * 30;
        const edge = d + jitter;
        const on = edge < R;
        if (on) lit++;
        let a;
        if (!on) a = 0.07;
        else if (edge > R - 26) a = 0.55 + rnd(i * 7 + j * 313 + phase) * 0.45;  // 파면
        else a = 0.20 + rnd(i * 911 + j * 53 + Math.floor(phase / 3)) * 0.12;    // 이미 태운 자리
        ctx.fillStyle = `rgba(255,255,255,${a.toFixed(3)})`;
        ctx.fillRect(x + .6, y + .6, cw - 1.2, ch - 1.2);
      }
    }

    // 다 탄 뒤에는 테두리로 "여기가 끝"을 못박는다. 실패는 색이 아니라 맥락으로 말한다
    if (done) {
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.strokeRect(1.5, 1.5, w - 3, gridH - 3);
    }

    // ── 주인공: 숫자 ────────────────────────────────
    const n = done ? budget : Math.round(clamp(lit / (gx * gy)) * budget);
    const by = gridH + (h - gridH) * 0.62;

    ctx.textAlign = 'left';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 12);
    ctx.fillText(spec.label || '탐색한 후보', 0, gridH + 26);

    // 깊이 — 히어로 숫자는 납작하면 라벨처럼 보인다. 위가 밝고 아래로 가라앉는다
    setShadow(ctx, GLOW_INK, 26, 4);
    ctx.fillStyle = depthGrad(ctx, 0, by - 40, 0, by + 22, 'ink');
    ctx.font = disp(900, 62);
    ctx.fillText(n.toLocaleString(), 0, by + 22);
    clearShadow(ctx);

    // 판정 — 마지막 자막에서 붙는다. 숫자 옆이라 "이만큼 쓰고 이 결과"가 한 눈에 붙는다
    const vk = ease(cue(spec.verdictCue ?? nLines - 1, 0.15, 0.5));
    if (vk > 0.02) {
      const label = spec.verdict || 'NoSolution';
      ctx.font = mono(700, 13);
      const tw = ctx.measureText(label).width;
      ctx.globalAlpha = vk;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.strokeRect(w - tw - 21.5, by - 8.5, tw + 20, 26);
      ctx.fillStyle = tone('ink');
      ctx.fillText(label, w - tw - 11, by + 9);
      ctx.globalAlpha = 1;
    }
  }
};
