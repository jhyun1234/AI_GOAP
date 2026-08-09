import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect } from '../../../engine/lib.js';

/* twoq — 코드를 안 건드린 그 하루의 질문 두 개. 카드 안을 검토 밴드가 왕복하는데,
   답이 난 질문은 밴드가 멈추고 답이 안 난 질문은 계속 훑는다.
   그래서 Q2 가 다음 샷으로 넘어가는 열린 고리가 화면에도 남는다.
   문구 출처: M13 글 29·31행. */

const Q1 = ['이 게임이 GOAP의 원래 의도에', '맞게 설계되어 있는가'];
const Q2 = ['RimWorld는', '무엇이 다른가'];

function card(ctx, x, y, cw, ch, tag, body, k) {
  ctx.globalAlpha = clamp(k);
  ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
  roundRect(ctx, x, y, cw, ch, 4); ctx.stroke();
  ctx.font = mono(700, 11); ctx.fillStyle = tone('sub');
  ctx.fillText(tag, x + 14, y + 22);
  ctx.font = disp(800, 15); ctx.fillStyle = tone('ink');
  body.forEach((s, i) => ctx.fillText(s, x + 14, y + 50 + i * 22));
  ctx.globalAlpha = 1;
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    const kDay = ease(cue(0));
    const kCards = ease(cue(1));
    const kAns = ease(cue(2));

    // ── 머리말 : 코드를 안 건드린 날 ────────────────
    ctx.font = mono(700, 11); ctx.fillStyle = tone('sub');
    ctx.fillText('ANALYSIS DAY - 0 COMMITS', M, M + 10);
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(M, M + 20); ctx.lineTo(M + (w - M * 2) * clamp(kDay), M + 20);
    ctx.stroke();

    const cw = (w - M * 2 - 22) / 2, ch = 112, cy = 54;
    const x1 = M, x2 = M + cw + 22;

    /* 🔴 카드는 첫 프레임부터 옅게 서 있고 검토 밴드는 언제나 돈다.
       1차본은 둘 다 cue(1) 뒤에만 나와서 첫 자막 구간이 멈췄다(검수 실측 6.6초).
       뜻으로도 이쪽이 맞다 — 질문 두 개를 놓고 하루를 통째로 보낸 세션이라,
       질문은 첫 줄부터 이미 책상 위에 있었다. */
    card(ctx, x1, cy, cw, ch, 'Q1', Q1, Math.max(0.22, kCards));
    card(ctx, x2, cy, cw, ch, 'Q2', Q2, Math.max(0.22, clamp((kCards - 0.25) / 0.6)));

    // ── 검토 밴드 — 답이 안 난 질문만 계속 훑는다 ────
    const band = (x, live, phase) => {
      if (!live) return;
      const P = 2.8, k = ((t + phase) % P) / P;
      const tri = k < 0.5 ? k * 2 : 2 - k * 2;
      const bx = lerp(x + 4, x + cw - 46, ease(tri));
      ctx.fillStyle = tone('track');
      ctx.fillRect(bx, cy + 4, 42, ch - 8);
    };
    band(x1, kAns < 0.5, 0);
    band(x2, true, 1.3);

    // ── Q1 의 답 ──────────────────────────────────
    if (kAns > 0.02) {
      ctx.globalAlpha = clamp(kAns);
      const ay = cy + ch + 14;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, x1, ay, cw, h - ay - 12, 4); ctx.stroke();
      ctx.font = disp(900, 20); ctx.fillStyle = tone('accent');
      ctx.fillText('부합한다', x1 + 14, ay + 26);
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText('EVIDENCE: DISASTER/THREAT = 0 LINES', x1 + 14, ay + 44);
      ctx.globalAlpha = 1;
    }
  },
};
