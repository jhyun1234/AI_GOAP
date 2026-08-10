import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* thirdway — 선택지 세 칸은 AI 색으로 서고, 그 위에 앉는 결정 표식 하나만 사람 색이다.
   S10 이 비워 둔 「사람 몫」 칸이 여기서 채워진다.
   위쪽 진단 칩 둘은 M0 글 65행 원문(문자열 switch 55곳 · 갓 클래스)이고, 결과 줄은
   같은 글 3·71행(24,800 → 5,977, 세션 로그 기록 기준)이다.
   연속 모션 = 세 선택지를 번갈아 비추는 검토 밴드. 결정이 앉은 뒤에도 계속 돈다 —
   고른 다음에도 트레이드오프는 남아 있다. */

/* 🔴 v2 — 진단 칩 둘을 원문이 **풀어 놓은 말**로 바꿨다(`ADR-V25-13` ①).
   `문자열 switch 55곳` → M0 글 13행 *"주민이 새 행동 하나를 배울 때마다 … '이 행동이
   나무 자르기면...' 식의 조건 분기 … 6개 파일, 55곳"*.
   `갓 클래스` → 같은 글 17행 *"모든 것을 혼자 아는 탓에, 손댈 때마다 전체가 흔들리는 파일"*.
   숫자 55 는 그대로 남긴다 — 원문에 있는 수다. */
const OPTS = ['유지', '폐기', '제3의 길'];
/* 🔴 2차 정정 (검수 C4): `조건 분기 55곳` → `갈림길 55곳`.
   자막(S11/2)이 「이 행동이 나무 자르기면, 하는 **갈림길**이 코드 55곳」으로 풀어 놨는데
   화면만 「조건 분기」였다 — 같은 것을 두 이름으로 부르고 **화면 쪽이 더 어려웠다.**
   `ADR-V-10` 은 통과하지만(둘 다 한국어) 이번 작업의 취지(`ADR-V25-13`)에 어긋난다. */
const DIAG = ['갈림길 55곳', '모든 걸 혼자 아는 파일'];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    /* v2 자막 7줄에 물린다: 2 = 진단(갈림길 55곳) · 3 = 혼자 다 아는 파일 ·
       4 = 세 가지를 내놨다 · 5 = 사람이 골랐다 · 6 = 그 결정 뒤에. */
    const kDiag = ease(cue(2));    // AI가 먼저 진단 — 첫 칩
    const kDiag2 = ease(cue(3));   // 둘째 칩
    const kOpt = ease(cue(4));     // 세 가지를 내놨다
    const kPick = ease(cue(5));    // 사람이 골랐다
    const kOut = ease(cue(6));     // 그 결정 뒤에

    // ── 위 : 진단 칩 ───────────────────────────────
    {
      let x = M;
      ctx.font = disp(700, 10);
      ctx.fillStyle = tone('sub');
      ctx.fillText('진단', M, 26);
      const cy = 34;
      for (let i = 0; i < DIAG.length; i++) {
        const k = clamp(i === 0 ? kDiag : kDiag2);
        if (k <= 0.02) break;
        ctx.font = disp(800, 13);
        const cw = ctx.measureText(DIAG[i]).width + 22;
        ctx.save(); ctx.globalAlpha = clamp(k);
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        roundRect(ctx, x, cy, cw, 26, 3); ctx.stroke();
        ctx.fillStyle = tone('accent');
        ctx.fillText(DIAG[i], x + 11, cy + 19);
        ctx.restore();
        x += cw + 10;
      }
    }

    // ── 가운데 : 선택지 세 칸 ──────────────────────
    const gap = 16;
    const cw = (w - M * 2 - gap * 2) / 3;
    const cy = 92, ch = 62;

    // 연속 모션 : 검토 밴드
    {
      const PR = 3.9, k = (t % PR) / PR;
      const idx = Math.floor(k * 3);
      const frac = k * 3 - idx;
      const bx = M + idx * (cw + gap) + lerp(0, cw + gap, ease(Math.min(1, frac * 1.8)));
      ctx.fillStyle = tone('track');
      ctx.fillRect(Math.min(bx, M + 2 * (cw + gap)), cy - 6, cw, ch + 12);
    }

    for (let i = 0; i < 3; i++) {
      const cx = M + i * (cw + gap);
      const k = clamp((kOpt - i * 0.15) / 0.45);
      const chosen = i === 2 && kPick > 0.25;
      ctx.strokeStyle = k > 0.1 ? tone('accent') : tone('track');
      ctx.lineWidth = 3;
      roundRect(ctx, cx, cy, cw, ch, 4); ctx.stroke();

      let fs = 24;
      ctx.font = disp(900, fs);
      while (ctx.measureText(OPTS[i]).width > cw - 24 && fs > 13) {
        fs -= 1; ctx.font = disp(900, fs);
      }
      const sw = ctx.measureText(OPTS[i]).width;
      ctx.save(); ctx.globalAlpha = clamp(0.25 + 0.75 * k);
      ctx.fillStyle = tone('accent');
      ctx.fillText(OPTS[i], cx + (cw - sw) / 2, cy + 40);
      ctx.restore();

      // 트레이드오프 두 줄
      if (k > 0.4) {
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(cx + 14, cy + ch - 14); ctx.lineTo(cx + cw - 40, cy + ch - 14);
        ctx.moveTo(cx + 14, cy + ch - 6); ctx.lineTo(cx + cw - 66, cy + ch - 6);
        ctx.stroke();
      }

      // 사람 표식 — 세 번째 칸 위에 내려앉는다
      if (chosen) {
        const kk = clamp((kPick - 0.25) / 0.5);
        const my = lerp(cy - 44, cy - 12, ease(kk));
        ctx.save(); ctx.globalAlpha = clamp(kk);
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        const bw = 92, bx = cx + (cw - bw) / 2;
        roundRect(ctx, bx, my - 20, bw, 24, 3); ctx.stroke();
        ctx.font = disp(900, 13); ctx.fillStyle = tone('ink');
        const s = '사람 결정';
        const w2 = ctx.measureText(s).width;
        ctx.fillText(s, bx + (bw - w2) / 2, my - 3);
        ctx.beginPath();
        ctx.moveTo(cx + cw / 2, my + 4); ctx.lineTo(cx + cw / 2, cy - 2);
        ctx.stroke();
        ctx.restore();
      }
    }

    // ── 아래 : 결과 한 줄 ──────────────────────────
    if (kOut > 0.05) {
      ctx.save(); ctx.globalAlpha = clamp(kOut);
      const by = h - 40;
      ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText('코드 줄 수 (세션 로그 기준)', M, by - 8);
      ctx.font = disp(900, 24);
      const a = '24,800', b = '5,977';
      const aw = ctx.measureText(a).width;
      ctx.fillStyle = tone('ink');
      ctx.fillText(a, M, by + 20);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      const ax = M + aw + 14;
      ctx.beginPath();
      ctx.moveTo(ax, by + 12); ctx.lineTo(ax + 34, by + 12);
      ctx.moveTo(ax + 26, by + 5); ctx.lineTo(ax + 34, by + 12);
      ctx.lineTo(ax + 26, by + 19);
      ctx.stroke();
      ctx.fillStyle = tone('accent');
      ctx.fillText(b, ax + 46, by + 20);
      ctx.restore();
    }
  },
};
