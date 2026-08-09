import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect } from '../../../engine/lib.js';

/* threshold — 숫자 하나가 두 번 내려가야 늑대가 비로소 문다.
   시도는 자막과 무관하게 계속 날아간다. 임계값이 높은 동안에는 매번 튕기고,
   8 로 내려간 뒤에야 통과한다 — '착탄이 원천적으로 불가능했다'가 이 반복의 뜻이다.
   아래 두 막대에는 눈금을 넣지 않는다. 원문이 말한 것은 어느 쪽이 길었나뿐이고
   비율은 원문에 없다.
   출처: M10 글 115·117·132행. */

const STEPS = ['12', '10', '8'];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 18;

    const kFail = ease(cue(0));
    const kStep = ease(cue(1));
    const kCode = ease(cue(2));
    const kTime = ease(cue(3));

    const stage = kStep < 0.34 ? 0 : kStep < 0.72 ? 1 : 2;

    // ── 임계값 계단 ───────────────────────────────
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('FLEE THRESHOLD', M, 30);
    let x = M;
    for (let i = 0; i < STEPS.length; i++) {
      const on = i <= stage;
      const cur = i === stage;
      ctx.globalAlpha = on ? 1 : 0.35;
      ctx.strokeStyle = cur ? tone('accent') : tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, x, 40, 62, 42, 4); ctx.stroke();
      ctx.font = disp(900, 24);
      ctx.fillStyle = cur ? tone('accent') : tone('ink');
      const s = STEPS[i];
      ctx.fillText(s, x + 31 - ctx.measureText(s).width / 2, 71);
      ctx.globalAlpha = 1;
      if (i < STEPS.length - 1) {
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.moveTo(x + 68, 61); ctx.lineTo(x + 86, 61); ctx.stroke();
      }
      x += 92;
    }

    // ── 착탄 시도 — 계속 날아간다 ───────────────────
    const tx0 = 300, tx1 = w - M - 74, ty = 61;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(tx0, ty); ctx.lineTo(tx1, ty); ctx.stroke();
    // 표적
    ctx.strokeStyle = stage === 2 ? tone('accent') : tone('ink'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.arc(tx1 + 26, ty, 20, 0, Math.PI * 2); ctx.stroke();
    ctx.font = mono(700, 9); ctx.fillStyle = tone('sub');
    ctx.fillText('HIT', tx1 + 14, ty + 40);

    if (kFail > 0.1) {
      const P = 1.7, k = (t % P) / P;
      const guard = tx1 - 34;
      let ax;
      if (stage === 2) ax = lerp(tx0, tx1 + 26, ease(k));
      else if (k < 0.55) ax = lerp(tx0, guard, ease(k / 0.55));
      else ax = lerp(guard, tx0, ease((k - 0.55) / 0.45));
      ctx.fillStyle = stage === 2 ? tone('accent') : tone('ink');
      ctx.beginPath(); ctx.arc(ax, ty, 9, 0, Math.PI * 2); ctx.fill();
      if (stage < 2) {
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.moveTo(guard + 8, ty - 24); ctx.lineTo(guard + 8, ty + 24); ctx.stroke();
        ctx.font = mono(700, 9); ctx.fillStyle = tone('sub');
        ctx.fillText('FLEE WINS', guard - 26, ty - 32);
      }
    }

    // ── 코드 변경 ─────────────────────────────────
    if (kCode > 0.02) {
      ctx.globalAlpha = clamp(kCode);
      ctx.font = mono(700, 11); ctx.fillStyle = tone('sub');
      ctx.fillText('ASSET VALUE ONLY', M, 122);
      ctx.font = disp(900, 20); ctx.fillStyle = tone('accent');
      ctx.fillText('code diff  0 lines', M, 150);
      ctx.globalAlpha = 1;
    }

    // ── 두 시간 (눈금 없음) ─────────────────────────
    if (kTime > 0.02) {
      const by = 176, bh = 22;
      const L = M, FW = w - M * 2;
      const rows = [
        { s: 'BUILD THE WOLF', f: 0.34, accent: false },
        { s: 'MAKE IT ACTUALLY BITE', f: 0.86, accent: true },
      ];
      rows.forEach((r, i) => {
        const k = clamp((kTime - i * 0.3) / 0.6);
        const y = by + i * 34;
        ctx.strokeStyle = r.accent ? tone('accent') : tone('ink'); ctx.lineWidth = 3;
        const bw = FW * r.f * ease(k);
        if (bw > 3) roundRect(ctx, L, y, bw, bh, 3), ctx.stroke();
        ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
        ctx.fillText(r.s, L + 10, y + 15);
      });
    }
  },
};
