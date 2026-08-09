import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect } from '../../../engine/lib.js';

/* stock — 겨울 필요량 562 위에 비축 1,110 이 얹힌 그림. 두 배가 눈으로 읽혀야 하므로
   같은 축에 겹쳐 그린다(막대 두 개로 나누면 '두 배'가 계산이 되어 버린다).
   아래 계기는 이 마디의 진짜 결론이다 — 비축은 넘치는데 긴장선은 평평하다.
   그 선은 자막과 무관하게 계속 흐른다. 정적을 메우려는 장식이 아니라,
   "아무 일도 안 일어나는 상태가 계속되고 있다"가 이 샷이 말하려는 것이다.
   수치 출처: M6 글 47행. */

const SCALE = 1200;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 18, L = M, R = w - M - 62, AXW = R - L;

    const kNeed = ease(cue(0));
    const kHave = ease(cue(1));
    const kTwice = ease(cue(2));

    const BY = 54, BH = 46;
    const xNeed = L + AXW * (562 / SCALE);
    const xHave = L + AXW * (1110 / SCALE);

    // ── 축 ───────────────────────────────────────
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(L, BY + BH + 8); ctx.lineTo(R + 50, BY + BH + 8); ctx.stroke();

    // ── 비축 막대 ────────────────────────────────
    const bw = (xHave - L) * clamp(kHave);
    if (bw > 3) {
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, L, BY, bw, BH, 3); ctx.stroke();
      ctx.font = disp(900, 24); ctx.fillStyle = tone('ink');
      ctx.fillText('1,110', L + 12, BY + BH - 14);
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText('IN STOCK', L + 12, BY - 8);
    }

    // ── 필요선 ───────────────────────────────────
    if (kNeed > 0.02) {
      ctx.globalAlpha = clamp(kNeed);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(xNeed, BY - 16); ctx.lineTo(xNeed, BY + BH + 16); ctx.stroke();
      ctx.font = disp(900, 17); ctx.fillStyle = tone('accent');
      ctx.fillText('562', xNeed + 8, BY + BH + 30);
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText('NEEDED FOR WINTER', xNeed + 8, BY + BH + 43);
      ctx.globalAlpha = 1;
    }

    // ── 두 배 표시 ───────────────────────────────
    if (kTwice > 0.02) {
      const y = BY - 22;
      const x1 = L, x2 = lerp(L, xHave, clamp(kTwice));
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(x1, y + 7); ctx.lineTo(x1, y); ctx.lineTo(x2, y); ctx.lineTo(x2, y + 7);
      ctx.stroke();
      if (kTwice > 0.6) {
        ctx.font = disp(900, 20); ctx.fillStyle = tone('ink');
        ctx.fillText('x2', xHave + 12, y + 9);
      }
    }

    // ── 긴장 계기 — 자막과 무관하게 계속 흐른다 ──────
    const gy = h - 34;
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('TENSION', L, gy - 12);
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(L + 62, gy); ctx.lineTo(w - M, gy); ctx.stroke();
    const gk = (t % 4.2) / 4.2;
    const gx = lerp(L + 62, w - M, gk);
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(Math.max(L + 62, gx - 46), gy); ctx.lineTo(gx, gy); ctx.stroke();
    ctx.fillStyle = tone('ink');
    ctx.beginPath(); ctx.arc(gx, gy, 8, 0, Math.PI * 2); ctx.fill();
  },
};
