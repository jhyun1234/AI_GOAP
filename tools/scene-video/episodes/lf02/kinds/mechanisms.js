import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* mechanisms — 여덟 칸이 스무 칸으로 자란다. 각 칸 아래에 실증 표식이 하나씩 매달리고,
   표식이 안 달린 칸은 색이 안 찬다 — 「근거 없는 원칙은 다음 세션이 안 믿는다」를
   글자 대신 빈 칸으로 말한다.
   수는 원문에 있는 둘뿐이다 — 처음 여덟(번외 글 11행) · 지금 스물(Docs/개발_방법론_명세서.md
   §2 의 M1~M20 표제를 직접 셈).
   🔴 2차 개정 (검수 R1): 1차본은 카운터(`8→20`)와 `M1 - M20` 을 `x = w − M` 기준으로
   **캔버스 우상단**에 그렸는데, 그 자리가 엔진 HUD 챕터 칩(「기억」)이 앉는 자리라
   글자가 칩 아래로 들어갔다(증거 `build/stills/600s.png`). 21개 kind 중 우측 정렬을
   쓴 것이 이 하나뿐이었다 → **머리글 옆 왼쪽 정렬로 옮겼다.**

   🔴 2차 개정 (검수 C6): 연속 모션이 12×6px 실증 표식의 점멸뿐이라 문턱 아래였다 →
   **두 행을 훑는 52px 폭 띠**를 세웠다(칸보다 먼저 그린다).

   연속 모션 = 칸을 훑는 띠 + 실증 표식이 왼쪽부터 차례로 켜지며 도는 것.
   실증은 커밋이 쌓일 때마다 다시 붙으므로 멈추지 않는다. */

const N0 = 8, N1 = 20, COLS = 10;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    const kGap = ease(cue(0));     // 무엇을 지켜라만 있었다
    const kBack = ease(cue(1));    // 커밋에서 거꾸로 뽑았다
    const kGrow = ease(cue(2));    // 여덟 → 스물
    const kProof = ease(cue(3));   // 실증이 붙는다
    const kTrust = ease(cue(4));   // 안 믿을 테니까

    const gap = 7;
    const cw = (w - M * 2 - gap * (COLS - 1)) / COLS;
    const chh = 34;
    const rowY = [64, 64 + chh + 30];

    // 연속 모션 ① : 두 행을 훑는 띠 (칸보다 먼저 — 강조색 위에 흰색을 얹지 않는다)
    {
      const BR = 3.4, bk = (t % BR) / BR;
      const bx0 = lerp(M - 34, w - M + 34, bk);
      const top = rowY[0] - 12, bot = rowY[1] + chh + 18;
      ctx.fillStyle = tone('track');
      ctx.fillRect(bx0 - 26, top, 52, bot - top);
    }

    ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
    const HEAD = '어떻게 판단하는가';
    ctx.fillText(HEAD, M, 32);
    const headW = ctx.measureText(HEAD).width;

    /* 수 표시 — 8 → 20. 🔴 우측 정렬 금지: 캔버스 우상단은 엔진 HUD 챕터 칩 자리다. */
    {
      const cur = Math.round(lerp(N0, N1, ease(kGrow)));
      const nx = M + headW + 26;
      ctx.font = disp(900, 22);
      const s = String(cur);
      const sw = ctx.measureText(s).width;
      ctx.fillStyle = kGrow > 0.5 ? tone('accent') : tone('ink');
      ctx.fillText(s, nx, 36);
      /* `M1 - M20` 은 방법론 문서의 절 번호를 그대로 인용한 것이다(`ADR-V-10` 예외 ①).
         🧊 개수는 기준 시각 `03d0c17d` 에서 다시 셌다 — `grep -c "^### M[0-9]"` = 20. */
      ctx.font = mono(700, 9); ctx.fillStyle = tone('sub');
      ctx.fillText('M1 - M20', nx + sw + 12, 34);
    }

    // 연속 모션 ② : 실증 표식이 도는 위상
    const PR = 2.8;
    const wave = (t % PR) / PR;

    const total = Math.round(lerp(N0, N1, ease(kGrow)));
    for (let i = 0; i < N1; i++) {
      const r = Math.floor(i / COLS), c = i % COLS;
      const cx = M + c * (cw + gap), cy = rowY[r];
      const born = i < N0
        ? clamp((kBack - c * 0.05) / 0.4)
        : clamp((kGrow - (i - N0) * 0.05) / 0.45);
      if (i >= total && born < 0.02) continue;

      ctx.save(); ctx.globalAlpha = clamp(0.2 + 0.8 * born);
      ctx.strokeStyle = i < N0 ? tone('ink') : tone('accent');
      ctx.lineWidth = 3;
      roundRect(ctx, cx, cy, cw, chh, 3); ctx.stroke();
      ctx.restore();

      // 실증 표식 — 아래에 매달린다
      const ph = ((i / N1) + wave) % 1;
      const lit = kProof > 0.05 && ph < 0.22;
      const kp = clamp((kProof - (i / N1) * 0.35) / 0.5);
      if (kp > 0.03) {
        ctx.save(); ctx.globalAlpha = clamp(kp) * (lit ? 1 : 0.45);
        ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(cx + cw / 2, cy + chh); ctx.lineTo(cx + cw / 2, cy + chh + 9);
        ctx.stroke();
        ctx.fillStyle = lit ? tone('accent') : tone('sub');
        ctx.fillRect(cx + cw / 2 - 6, cy + chh + 9, 12, 6);
        ctx.restore();
      }
    }

    // 규칙만 있고 사고법이 없던 상태 — 왼쪽 위에 빈 층 하나
    if (kGap > 0.03 && kBack < 0.6) {
      ctx.save(); ctx.globalAlpha = clamp(kGap) * (1 - clamp(kBack / 0.6));
      ctx.setLineDash([7, 6]);
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, M, rowY[0], w - M * 2, chh, 3); ctx.stroke();
      ctx.setLineDash([]);
      ctx.font = disp(700, 11); ctx.fillStyle = tone('sub');
      ctx.fillText('어떻게 판단하라 — 비어 있었다', M + 10, rowY[0] + 22);
      ctx.restore();
    }

    // 실증 라벨
    if (kProof > 0.05) {
      ctx.save(); ctx.globalAlpha = clamp(kProof);
      ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText('근거 = 실제 커밋', M, h - 12);
      ctx.restore();
    }
    if (kTrust > 0.05) {
      ctx.save(); ctx.globalAlpha = clamp(kTrust);
      ctx.font = disp(900, 15); ctx.fillStyle = tone('ink');
      const s = '근거 없으면 안 믿는다';
      const sw = ctx.measureText(s).width;
      ctx.fillText(s, w - M - sw, h - 10);
      ctx.restore();
    }
  },
};
