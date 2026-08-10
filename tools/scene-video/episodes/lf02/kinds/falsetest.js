import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* falsetest — 같은 입력을 두 검사에 나란히 넣는다.
   왼쪽(항등식)은 무엇을 넣어도 통과 램프가 켜지고, 오른쪽(실패 가능한 명제)은
   같은 입력 중 하나에서 실제로 걸린다. 「검사가 있다」와 「검사가 일한다」가 다르다는 것을
   램프의 차이 하나로만 말한다.
   연속 모션 = 입력 토큰이 계속 두 검사를 통과해 흘러간다. 검사는 커밋마다 다시 돈다.
   문자열 출처: devlog/sessions/2026-08-02.md 57~60행 · Docs/M17_촌장금고와_재정정책_실행명세서.md 356~359행. */

/* 🔴 v2 — 두 명제를 「순액/총액」이 아니라 **말로 푼 형태**로 적는다
   (`ADR-V25-13` ① — 재료는 원문 M17 명세서 356~359행의 DoD 문장들이다:
   `0 ≤ 순액 ≤ 총액` · `NetWage(20,0)==20`).
   자막이 말하는 문장과 겹치지 않도록 오른쪽 둘째 줄은 **경계 조건** 쪽을 쓴다 —
   자막은 「세율이 오르면 실수령이 절대 늘지 않는다」를 말하고, 판은 그 옆의 경계를 진다. */
const LEFT = '받은 돈 + 뗀 세금 = 원래 임금';
const RIGHT = '0 <= 받은 돈 <= 원래 임금';
const RIGHT2 = '세금 0%면 받은 돈 = 임금';

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    const kThree = ease(cue(0));   // 세 번 썼다
    const kEq = ease(cue(1));      // 이런 걸 검증이라고
    const kAlways = ease(cue(2));  // 무엇을 구현해도 통과
    const kSwap = ease(cue(3));    // 실패할 수 있는 명제로

    const colW = (w - M * 2 - 22) / 2;
    const lx = M, rx = M + colW + 22;
    const cy = 52, ch = h - cy - 54;

    // ── 좌우 검사 칸 ───────────────────────────────
    const COLS = [[0, lx], [1, rx]];
    for (const [i, cx] of COLS) {
      const on = i === 0 ? kEq : kSwap;
      ctx.strokeStyle = on > 0.1 ? (i === 0 ? tone('ink') : tone('accent')) : tone('track');
      ctx.lineWidth = 3;
      roundRect(ctx, cx, cy, colW, ch, 4); ctx.stroke();
    }

    ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('언제나 참', lx, cy - 12);
    ctx.fillText('실패할 수 있다', rx, cy - 12);

    // ── 명제 글자 ─────────────────────────────────
    const fitTxt = (s, maxW, weight, start) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (ctx.measureText(s).width > maxW && fs > 9) { fs -= 1; ctx.font = disp(weight, fs); }
      return fs;
    };
    if (kEq > 0.02) {
      ctx.save(); ctx.globalAlpha = clamp(kEq);
      fitTxt(LEFT, colW - 26, 800, 16);
      ctx.fillStyle = tone('ink');
      const sw = ctx.measureText(LEFT).width;
      ctx.fillText(LEFT, lx + (colW - sw) / 2, cy + 34);
      ctx.restore();
    }
    if (kSwap > 0.02) {
      ctx.save(); ctx.globalAlpha = clamp(kSwap);
      fitTxt(RIGHT, colW - 26, 800, 16);
      ctx.fillStyle = tone('accent');
      let sw = ctx.measureText(RIGHT).width;
      ctx.fillText(RIGHT, rx + (colW - sw) / 2, cy + 30);
      fitTxt(RIGHT2, colW - 26, 700, 13);
      sw = ctx.measureText(RIGHT2).width;
      ctx.fillStyle = tone('sub');
      ctx.fillText(RIGHT2, rx + (colW - sw) / 2, cy + 50);
      ctx.restore();
    }

    // ── 연속 모션 : 입력 토큰이 두 검사를 통과한다 ────
    const trackY = cy + ch - 46;
    const IN_PER = 2.6;
    const kIn = (t % IN_PER) / IN_PER;
    for (const [i, cx] of COLS) {
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(cx + 14, trackY); ctx.lineTo(cx + colW - 14, trackY);
      ctx.stroke();
      const px = lerp(cx + 16, cx + colW - 16, ease(kIn));
      // 이번 회차 입력 번호 (결정적)
      const idx = Math.floor(t / IN_PER) % 3;
      const failing = i === 1 && idx === 2;
      ctx.fillStyle = failing && kSwap > 0.4 ? tone('ink') : tone('sub');
      ctx.beginPath(); ctx.arc(px, trackY, 5, 0, Math.PI * 2); ctx.fill();
      // 램프
      const ly = cy + ch - 22;
      const lit = i === 0 ? (kAlways > 0.2 || kEq > 0.5) : (kSwap > 0.4 && !failing);
      const lw = colW - 28;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, cx + 14, ly, lw, 14, 3); ctx.stroke();
      if (lit) {
        ctx.fillStyle = i === 0 ? tone('ink') : tone('accent');
        ctx.fillRect(cx + 18, ly + 4, lw - 8, 6);
      } else if (i === 1 && kSwap > 0.4) {
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        const mx = cx + 14 + lw / 2;
        ctx.beginPath();
        ctx.moveTo(mx - 8, ly + 2); ctx.lineTo(mx + 8, ly + 12);
        ctx.moveTo(mx + 8, ly + 2); ctx.lineTo(mx - 8, ly + 12);
        ctx.stroke();
      }
      ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText(i === 0 ? '통과' : (failing && kSwap > 0.4 ? '실패' : '통과'), cx + 14, ly - 6);
    }

    // ── 세 번이라는 수 ─────────────────────────────
    if (kThree > 0.05) {
      ctx.save(); ctx.globalAlpha = clamp(kThree);
      ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
      const s = '세 번 썼다';
      const sw = ctx.measureText(s).width;
      ctx.fillText(s, w - M - sw, cy - 12);
      ctx.restore();
    }
  },
};
