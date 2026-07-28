import { ease, easeOut, clamp, lerp, rnd, fitCanvas, mkCanvas, tone, roundRect } from '../lib.js';

/* gap — 두 크기를 같은 화면에 놓는다. 위에는 뒤진 후보 덩어리, 아래에는 정답 사슬.
   이 회차의 한 문장("4,096번 뒤지고도 네 단계를 못 찾았다")이 곧 이 그림이다.

   정답은 세로로 쌓는다. 9:16 화면에서 네 칸을 가로로 늘어놓으면 칸이 손톱만 해진다 —
   세로면 한 칸이 화면 폭을 다 쓰고 글자도 커진다.

   가이드의 '스켈레톤 + reveal' 패턴을 쓴다: 네 칸의 빈 테두리는 처음부터 제자리에 있고
   내용만 나중에 채워진다. 칸이 나중에 생기면 그때마다 아래 것들이 밀려 레이아웃이 흔들린다. */

const STEP_H = 0.19;   // 정답 한 칸의 높이 (아래 영역 대비)

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, nLines }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const chain = spec.answer || [];
    const gapK = ease(cue(spec.gapCue ?? nLines - 1, 0.2, 0.55));

    /* ── 위: 뒤진 후보 덩어리 ─────────────────────
       처음부터 다 차 있다. 이 샷은 "번지는 과정"이 아니라 "이미 다 태웠다"는 결과다. */
    const massH = h * 0.40;
    const mgx = 32, mgy = 12;
    const mw = w / mgx, mh = massH / mgy;
    const phase = Math.floor(t * 9);
    const fade = 1 - gapK * 0.72;                        // 대비가 붙는 순간 덩어리는 뒤로 물러난다

    for (let j = 0; j < mgy; j++) {
      for (let i = 0; i < mgx; i++) {
        const a = (0.16 + rnd(i * 149 + j * 37 + Math.floor(phase / 2)) * 0.30) * fade;
        ctx.fillStyle = `rgba(255,255,255,${a.toFixed(3)})`;
        ctx.fillRect(i * mw + .5, j * mh + .5, mw - 1, mh - 1);
      }
    }
    ctx.fillStyle = `rgba(255,255,255,${(0.7 * fade).toFixed(3)})`;
    ctx.font = '700 12px Consolas, monospace'; ctx.textAlign = 'left';
    ctx.fillText(`${spec.massLabel || 'AI가 뒤진 후보'}  ${(spec.mass || 4096).toLocaleString()}`,
      0, massH + 20);

    /* ── 아래: 정답 사슬 ──────────────────────────── */
    const top = massH + 36;
    const areaH = h - top;
    const stepH = areaH * STEP_H;
    const gapY = areaH * 0.045;
    const reveal = cue(spec.answerCue ?? 1, 0.2, 0.78);

    ctx.textAlign = 'left';
    chain.forEach((s, i) => {
      const y = top + i * (stepH + gapY);
      // 칸마다 조금씩 늦게 — 한꺼번에 켜지면 "네 단계"가 하나로 뭉쳐 보인다
      const k = ease(clamp(reveal * chain.length - i));
      const sc = lerp(0.94, 1, easeOut(k));             // 가이드 한도(0.8~1.15) 안
      const cwid = w * sc, cx0 = (w - cwid) / 2;

      // 스켈레톤 — 켜지기 전에도 자리는 있다
      ctx.lineWidth = 3;
      ctx.strokeStyle = k > 0.04 ? tone('accent') : tone('track');
      ctx.fillStyle = k > 0.04 ? `rgba(0,255,136,${(0.10 + 0.06 * k).toFixed(3)})` : 'transparent';
      roundRect(ctx, cx0 + 1.5, y + 1.5, cwid - 3, stepH - 3, 4);
      ctx.fill(); ctx.stroke();

      // 단계 번호
      ctx.fillStyle = k > 0.04 ? tone('accent') : tone('track');
      ctx.font = '900 15px Consolas, monospace';
      ctx.fillText(String(i + 1), cx0 + 14, y + stepH / 2 + 6);

      ctx.fillStyle = k > 0.04 ? tone('accent') : 'rgba(255,255,255,0.22)';
      ctx.font = '900 19px Pretendard, "Malgun Gothic", sans-serif';
      ctx.globalAlpha = 0.35 + 0.65 * k;
      ctx.fillText(s, cx0 + 38, y + stepH / 2 + 7);
      ctx.globalAlpha = 1;
    });

    const lastY = top + chain.length * (stepH + gapY);
    ctx.fillStyle = tone('sub'); ctx.font = '700 12px Consolas, monospace';
    ctx.globalAlpha = ease(clamp(reveal * 1.4));
    ctx.fillText(`${spec.answerLabel || '정답'}  ${chain.length}단계`, 0, lastY + 15);
    ctx.globalAlpha = 1;

    /* ── 대비 도장 ─────────────────────────────────
       두 숫자를 한 줄에 붙여 놓는다. 따로 있으면 시청자가 머리로 나눠야 한다. */
    if (gapK > 0.02 && spec.gapNote) {
      ctx.textAlign = 'right';
      ctx.globalAlpha = gapK;
      ctx.fillStyle = tone('accent');
      ctx.font = '900 22px Pretendard, "Malgun Gothic", sans-serif';
      ctx.fillText(spec.gapNote, w, lastY + 17);
      ctx.globalAlpha = 1;
      ctx.textAlign = 'left';
    }
  }
};
