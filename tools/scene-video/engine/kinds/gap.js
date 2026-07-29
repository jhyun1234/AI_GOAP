import {
  disp,
  ease, easeOut, clamp, lerp, rnd, spring, depthGrad, setShadow, clearShadow, GLOW,
  fitCanvas, mkCanvas, tone, roundRect
} from '../lib.js';

/* gap — 두 크기를 같은 화면에 놓는다. 위에는 뒤진 후보 덩어리, 아래에는 정답 사슬.
   이 회차의 한 문장("4,096번 뒤지고도 네 단계를 못 찾았다")이 곧 이 그림이다.

   정답은 세로로 쌓는다. 9:16 화면에서 네 칸을 가로로 늘어놓으면 칸이 손톱만 해진다 —
   세로면 한 칸이 화면 폭을 다 쓰고 글자도 커진다.

   가이드의 '스켈레톤 + reveal' 패턴을 쓴다: 네 칸의 빈 테두리는 처음부터 제자리에 있고
   내용만 나중에 채워진다. 칸이 나중에 생기면 그때마다 아래 것들이 밀려 레이아웃이 흔들린다. */

/* 🔴 칸 높이를 '남은 영역의 몇 할'로 잡으면 안 된다. 처음엔 STEP_H=0.19 로 잡았는데
   네 칸 + 사이 간격이 남은 영역의 94%를 먹어서, 그 아래 붙는 '정답 4단계 / 4,096 대 4'
   줄이 캔버스 밖으로 밀려 잘렸다. 비율은 칸 수가 바뀌면 같이 안 따라온다.
   지금은 반대로 간다 — 머리(덩어리)와 발(비교 줄) 자리를 먼저 떼어놓고,
   남은 것을 칸 수로 나눈다. 칸이 3개든 6개든 밖으로 안 나간다. */
const MASS_H = 0.36;   // 위 덩어리가 쓰는 높이 비율
const MASS_GAP = 30;   // 덩어리 라벨 아래 여백 (px)
const FOOT_H = 30;     // 아래 비교 줄이 쓰는 높이 (px)
const FOOT_PAD = 8;    // 비교 줄 글자 밑선과 캔버스 바닥 사이 (22px 글자의 아랫부분 몫)
const STEP_GAP = 7;    // 정답 칸 사이 간격 (px)

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, nLines }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const chain = spec.answer || [];
    const gapK = ease(cue(spec.gapCue ?? nLines - 1, 0.2, 0.55));

    /* ── 위: 뒤진 후보 덩어리 ─────────────────────
       처음부터 다 차 있다. 이 샷은 "번지는 과정"이 아니라 "이미 다 태웠다"는 결과다. */
    const massH = h * MASS_H;
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
    ctx.font = disp(700, 12); ctx.textAlign = 'left';
    ctx.fillText(`${spec.massLabel || 'AI가 뒤진 후보'}  ${(spec.mass || 4096).toLocaleString()}`,
      0, massH + 20);

    /* ── 아래: 정답 사슬 ────────────────────────────
       발(FOOT_H)을 먼저 떼어놓고 남은 높이를 칸 수로 나눈다 — 밖으로 나갈 수가 없다. */
    const top = massH + MASS_GAP;
    const n = Math.max(1, chain.length);
    const areaH = h - top - FOOT_H;
    const stepH = (areaH - STEP_GAP * (n - 1)) / n;
    const gapY = STEP_GAP;
    const reveal = cue(spec.answerCue ?? 1, 0.2, 0.78);

    ctx.textAlign = 'left';
    chain.forEach((s, i) => {
      const y = top + i * (stepH + gapY);
      // 칸마다 조금씩 늦게 — 한꺼번에 켜지면 "네 단계"가 하나로 뭉쳐 보인다
      const k = ease(clamp(reveal * chain.length - i));
      // 스프링 등장. 기준 폭을 0.95 로 줄여 두었으므로 최대 1.05 배가 곧 원래 폭이다
      const sc = spring(k) * 0.95;
      const cwid = w * sc, cx0 = (w - cwid) / 2;
      const on = k > 0.04;

      // 스켈레톤 — 켜지기 전에도 자리는 있다
      if (on) setShadow(ctx, GLOW, 18 * k, 3);
      ctx.lineWidth = 3;
      ctx.strokeStyle = on ? tone('accent') : tone('track');
      ctx.fillStyle = on ? `rgba(0,255,136,${(0.10 + 0.06 * k).toFixed(3)})` : 'transparent';
      roundRect(ctx, cx0 + 1.5, y + 1.5, cwid - 3, stepH - 3, 4);
      ctx.fill(); ctx.stroke();
      clearShadow(ctx);

      // 단계 번호
      ctx.fillStyle = on ? tone('accent') : tone('track');
      ctx.font = disp(900, 15);
      ctx.fillText(String(i + 1), cx0 + 14, y + stepH / 2 + 6);

      ctx.font = disp(900, 19);
      ctx.fillStyle = on
        ? depthGrad(ctx, cx0, y, cx0, y + stepH, 'accent')
        : 'rgba(255,255,255,0.22)';
      ctx.globalAlpha = 0.35 + 0.65 * k;
      ctx.fillText(s, cx0 + 38, y + stepH / 2 + 7);
      ctx.globalAlpha = 1;
    });

    // 발 — 두 글자가 같은 밑선에 앉는다. 밑선은 캔버스 바닥에서 역산한다
    const footBase = h - FOOT_PAD;
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 12);
    ctx.globalAlpha = ease(clamp(reveal * 1.4));
    ctx.fillText(`${spec.answerLabel || '정답'}  ${chain.length}단계`, 0, footBase);
    ctx.globalAlpha = 1;

    /* ── 대비 도장 ─────────────────────────────────
       두 숫자를 한 줄에 붙여 놓는다. 따로 있으면 시청자가 머리로 나눠야 한다. */
    if (gapK > 0.02 && spec.gapNote) {
      ctx.textAlign = 'right';
      ctx.globalAlpha = gapK;
      ctx.fillStyle = tone('accent');
      ctx.font = disp(900, 22);
      ctx.fillText(spec.gapNote, w, footBase);
      ctx.globalAlpha = 1;
      ctx.textAlign = 'left';
    }
  }
};
