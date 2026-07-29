import {
  disp, mono, span, ease, clamp, rnd, fitCanvas, mkCanvas, tone, roundRect } from '../lib.js';

/* search — 탐색이 격자 위로 번지다 예산에 막히는 그림.
   핵심은 "많이 뒤졌다"가 아니라 "그 옆에 짧은 정답이 그대로 있다"는 대비다.
   그래서 정답 사슬은 처음부터 화면 아래에 놓여 있고, 끝에 밝아진다. */

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, cue, nLines }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const [gx, gy] = spec.grid || [24, 30];
    const budget = spec.budget || 4096;
    // 자막에 물린다 — 숫자를 말하는 순간 숫자가 차고, 정답을 말할 때 정답이 켜진다
    const growC = spec.growCue ?? 1;      // "4,096번을 뒤지고도 못 찾았어요"
    const ansC = spec.answerCue ?? nLines - 1;  // "근데 정답은 딱 네 단계였어요"

    const gridH = h * 0.62;
    const cw = w / gx, ch = gridH / gy;
    const cx = w / 2, cy = gridH / 2;
    const maxR = Math.hypot(cx, cy);

    // 파면 — 중심에서 바깥으로 번진다. 앞 자막부터 미리 번지기 시작한다(lead 1.8초)
    const grow = ease(cue(growC, 1.8, 0.62));
    const R = grow * maxR * 1.02;

    let lit = 0;
    for (let j = 0; j < gy; j++) {
      for (let i = 0; i < gx; i++) {
        const x = i * cw, y = j * ch;
        const d = Math.hypot(x + cw / 2 - cx, y + ch / 2 - cy);
        const jitter = rnd(i * 131 + j * 17) * 26;
        const on = d + jitter < R;
        if (on) lit++;
        // 3색 — 탐색이 닿은 칸은 흰색(대조 대상), 파면 가장자리만 밝게. 어두운 fill 금지
        ctx.fillStyle = on
          ? (d + jitter > R - 22 ? 'rgba(255,255,255,.85)' : 'rgba(255,255,255,.28)')
          : 'rgba(255,255,255,.07)';
        ctx.fillRect(x + .6, y + .6, cw - 1.2, ch - 1.2);
      }
    }

    // 예산 카운터
    const n = Math.round(clamp(lit / (gx * gy)) * budget);
    const done = grow >= 1;
    ctx.textAlign = 'left';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
    ctx.fillText('탐색한 후보', 0, gridH + 20);
    ctx.fillStyle = tone('ink');
    ctx.font = disp(900, 34);
    ctx.fillText((done ? budget : n).toLocaleString(), 0, gridH + 52);

    if (done) {
      // 실패는 색이 아니라 맥락으로 — 흰 테두리 + NoSolution 라벨. 깜빡임은 안 쓴다
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.strokeRect(1.5, 1.5, w - 3, gridH - 3);
      ctx.textAlign = 'right';
      ctx.fillStyle = tone('ink'); ctx.font = mono(700, 12);
      ctx.fillText(spec.verdict || 'NoSolution', w, gridH + 50);
      ctx.textAlign = 'left';
    }

    // 정답 사슬 — 내내 거기 있었다
    const chain = spec.answer || [];
    const reveal = ease(cue(ansC, 0.15, 0.72));
    const by = h - 20;
    ctx.font = disp(700, 11);
    ctx.fillStyle = reveal > 0 ? tone('accent') : tone('sub');
    ctx.fillText('정답', 0, by - 28);

    let x = 0;
    chain.forEach((s, i) => {
      const bw = ctx.measureText(s).width + 18;
      const on = reveal > i / chain.length;
      ctx.lineWidth = 3;
      ctx.strokeStyle = on ? tone('accent') : tone('track');
      ctx.fillStyle = on ? 'rgba(0,255,136,.14)' : 'transparent';
      roundRect(ctx, x + 1.5, by - 19, bw, 22, 3); ctx.fill(); ctx.stroke();
      ctx.fillStyle = on ? tone('accent') : tone('sub');
      ctx.fillText(s, x + 10, by - 4);
      x += bw + 5;
      if (i < chain.length - 1) {
        ctx.fillStyle = tone('track'); ctx.fillText('›', x - 2, by - 4); x += 9;
      }
    });
  }
};
