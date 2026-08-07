import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* brushpast — 둘이 서로 반대 방향으로 계속 흘러가고, 스치는 찰나에만 말이 튄다.

   원문: "둘째, 대화 연출이 어색했습니다. 두 주민이 스치듯 지나가며 아무 데서나 떠들면
   그게 대화로 안 보입니다."

   🔴 이 회차의 기본 도형은 **대화가 성립하는 자리 — 두 표식 사이의 거리와 멈춤**이다.
   이 샷은 그 자리가 **정해져 있지 않은 상태**를 그린다. 둘은 한 번도 멈추지 않고
   레인 위를 등속으로 오가며, 말풍선은 두 x 가 가까워지는 순간에만 떴다가 꺼진다.
   그 순간의 x 가 매번 다르다는 것이 「아무 데서나」다 — 바닥에 남는 자국이 그 증거다.

   🔴 **말풍선이 어디서 터지는지는 문턱이 아니라 거리의 연속 함수다.**
   near = clamp(1 - |xa − xb| / 54) 라서 `frac(t/N) > x` 같은 시각 조건이 없다.
   등장 자체(레인·표식·말풍선·바닥)는 전부 cue 에 물려 있다.

   🔴 표식은 둘뿐이다. 원문이 이 대목에서 "두 주민"이라고만 쓰고 인원을 세지 않는다 —
   숫자를 화면에 적지 않았고, 이름도 안 붙였다(원문이 여기서 이름을 안 부른다).

   🔴 다른 회차와 겹치지 않게 본 자리: ep07s-1/oneheap 은 흩어진 여덟이 **한 곳으로
   수렴**하고, ep08s-3/onecall 은 제 칸에서 **방향만** 바뀐다. 여기는 수렴도 정렬도 없다 —
   둘이 서로를 지나쳐 갈 뿐이고, 사건은 **겹치는 시간이 너무 짧다**는 것이다. */

const LA = 92, LB = 158;      // 레인 둘
const BUB = 125;              // 살아 있는 말풍선이 뜨는 띠
const MARK = 210, BASE = 240; // 지나간 자리에 남은 자국과 그 바닥선
const PA = 5.3, PB = 3.7;     // 둘의 왕복 주기 — 서로 나눠떨어지지 않아 만나는 x 가 매번 다르다
const PAST = [0.13, 0.31, 0.48, 0.68, 0.87];

/** 왕복(핑퐁) — 0→1→0. frac 만 쓰면 끝에서 순간이동한다. */
const pp = (t, p) => { const u = frac(t / p); return u < 0.5 ? u * 2 : 2 - u * 2; };
/** 진행 방향 — 왕복의 앞 절반이면 +1 */
const dir = (t, p) => (frac(t / p) < 0.5 ? 1 : -1);

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const fit = (txt, weight, start, max, min = 8) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    /** 말풍선 하나. tail=false 면 꼬리를 안 단다(작은 자국용). */
    const bubble = (cx, cy, bw, bh, a, col, tail = true, dots = false) => {
      if (a <= 0.02) return;
      ctx.globalAlpha = a;
      ctx.strokeStyle = col; ctx.lineWidth = 3;
      roundRect(ctx, cx - bw / 2, cy - bh / 2, bw, bh, 6); ctx.stroke();
      if (tail) {
        ctx.beginPath();
        ctx.moveTo(cx - 6, cy + bh / 2);
        ctx.lineTo(cx + 1, cy + bh / 2 + 9);
        ctx.lineTo(cx + 7, cy + bh / 2);
        ctx.stroke();
      }
      if (dots) {
        ctx.fillStyle = col;
        for (let i = -1; i <= 1; i++) {
          ctx.beginPath(); ctx.arc(cx + i * 9, cy, 2.6, 0, Math.PI * 2); ctx.fill();
        }
      }
      ctx.globalAlpha = 1;
    };

    const X0 = 20, X1 = w - 20;
    const k0 = ease(cue(spec.talkCue ?? 0, 0.15, 0.7));   // 둘이 말을 주고받는다
    const k1 = ease(cue(spec.passCue ?? 1, 0.15, 0.85));  // 스쳐 지나간다 · 자국이 남는다

    /* ── 위 라벨 ──────────────────────────────────── */
    if (spec.label) {
      const a = clamp(k0 * 4);
      if (a > 0.02) {
        ctx.globalAlpha = a;
        ctx.textAlign = 'left';
        ctx.font = disp(800, fit(spec.label, 800, 12.5, 150, 9));
        ctx.fillStyle = tone('sub');
        ctx.fillText(spec.label, X0, 34);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 레인 둘 — 점선이 서로 반대로 흐른다 ────────── */
    if (k0 > 0.02) {
      ctx.globalAlpha = clamp(k0 * 2.4) * 0.95;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.setLineDash([11, 8]);
      ctx.lineDashOffset = -(t * 16) % 19;
      ctx.beginPath(); ctx.moveTo(X0, LA); ctx.lineTo(X1, LA); ctx.stroke();
      ctx.lineDashOffset = (t * 16) % 19;
      ctx.beginPath(); ctx.moveTo(X0, LB); ctx.lineTo(X1, LB); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;
    }

    /* ── 표식 둘 ──────────────────────────────────── */
    const xa = lerp(X0 + 16, X1 - 16, pp(t, PA));
    const xb = lerp(X1 - 16, X0 + 16, pp(t + 1.35, PB));
    const da = dir(t, PA), db = -dir(t + 1.35, PB);

    const drawOne = (x, y, d, a) => {
      if (a <= 0.02) return;
      /* 꼬리 — 멈추지 않는다는 표시. 지나온 쪽으로 뻗는다. */
      if (k1 > 0.05) {
        ctx.globalAlpha = a * k1 * 0.8;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.moveTo(x - d * 9, y); ctx.lineTo(x - d * (9 + 26 * k1), y); ctx.stroke();
        ctx.globalAlpha = 1;
      }
      ctx.globalAlpha = a;
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(x, y, 6.5, 0, Math.PI * 2); ctx.fill();
      ctx.globalAlpha = 1;
    };

    drawOne(xa, LA, da, clamp(k0 * 3));
    drawOne(xb, LB, db, clamp(k0 * 3 - 0.35));

    /* ── 스치는 찰나에만 뜨는 말풍선 ────────────────── */
    const near = clamp(1 - Math.abs(xa - xb) / 54);
    const mid = (xa + xb) / 2;
    bubble(mid, BUB, 42, 26, clamp(k0 * 3) * near, tone('accent'), true, true);

    /* ── 지나간 자리 — 바닥선과 자국 ────────────────── */
    if (k1 > 0.02) {
      const ab = clamp(k1 * 2.4);
      ctx.globalAlpha = ab * 0.95;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.setLineDash([8, 7]);
      ctx.lineDashOffset = -(t * 12) % 15;
      ctx.beginPath(); ctx.moveTo(X0, BASE); ctx.lineTo(X1, BASE); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;

      for (let i = 0; i < PAST.length; i++) {
        const a = clamp(k1 * 2.8 - i * 0.32);
        if (a <= 0.02) continue;
        const px = lerp(X0 + 18, X1 - 18, PAST[i]);
        bubble(px, MARK, 24, 16, a * 0.7, tone('sub'), false, false);
        ctx.globalAlpha = a * 0.7;
        ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.moveTo(px, MARK + 11); ctx.lineTo(px, BASE - 4); ctx.stroke();
        ctx.globalAlpha = 1;
      }

      /* 지금 터진 자리 — 바닥의 그 x 에 표식이 선다 */
      const a = ab * near;
      if (a > 0.02) {
        ctx.globalAlpha = a;
        ctx.fillStyle = tone('accent');
        ctx.beginPath();
        ctx.moveTo(mid, BASE - 11); ctx.lineTo(mid - 7, BASE + 1); ctx.lineTo(mid + 7, BASE + 1);
        ctx.closePath(); ctx.fill();
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
