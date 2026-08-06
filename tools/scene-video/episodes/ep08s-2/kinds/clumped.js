import {
  disp, mono, ease, clamp, frac,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* clumped — 값 축에서는 한 점인데, 그 한 점을 통째로 키워도 다섯이 다 그 안에 있다.

   원문: "그래서 실제 값을 직접 뽑아봤더니, 다섯 명의 초기 포만이 전부 81.6에서 81.7 사이에
   뭉쳐 있었습니다. 사실상 전원이 똑같았던 거죠."

   🔴 이 그림이 지는 것은 **한 칸의 폭**이다. 위 축에서 다섯은 그냥 겹친 한 점이라 그것만
   보면 "붙어 있네" 이상을 말하지 못한다. 그래서 그 점에 괄호를 물리고, 괄호를 아래 상자로
   벌려서 **상자 전체가 81.6~81.7 이라는 것**을 두 끝에 적었다. 다섯이 그 안에서 아무리
   벌어져 있어도 상자가 0.1 폭이면 벌어진 게 아니다.

   🔴 숫자는 둘뿐이다(81.6 · 81.7). 원문이 준 것이 구간의 두 끝뿐이라 그 사이의 다섯 값은
   **적지 않았다** — 상자 안 표식의 x 는 값이 아니라 도형 파라미터다. 차이(0.1)도 안 적었다.
   그건 원문 문장이 아니라 내가 뺀 값이라, 화면에 올리면 지어낸 수치가 된다.

   🔤 상자 두 끝의 값은 mono(코드·로그에 그대로 있을 법한 ASCII)이고, 축 이름은 disp 다.

   계속 도는 것 = 확대 상자를 훑는 검사선(높이 60px 이 폭 312px 를 왕복 없이 흐른다),
   괄호에서 상자로 벌어지는 부챗살의 점선, 겹친 점을 두른 고리의 맥박. */

const FAN = 1.9;      // 부챗살 점선이 흐르는 주기
const SCAN = 2.6;     // 상자를 훑는 검사선
const PULSE = 1.7;    // 겹친 점의 고리
const NUDGE = [-2.4, -0.9, 0.3, 1.5, 2.7];        // 위 축에서 다섯이 겹치는 어긋남(픽셀)
const INNER = [0.17, 0.34, 0.46, 0.63, 0.82];     // 확대 상자 안의 자리

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

    const N = Math.min(spec.people ?? 5, NUDGE.length);
    const X0 = 20, X1 = w - 20, SPAN = X1 - X0;
    const AY = 88;                                  // 값 축
    const CX = X0 + 0.79 * SPAN;                    // 다섯이 겹쳐 있는 자리
    const ZX0 = 20, ZX1 = w - 20, ZY0 = 154, ZY1 = 222;   // 확대 상자
    const ZW = ZX1 - ZX0;

    const pk = ease(cue(spec.pullCue ?? 0, 0.15, 0.85));

    const a0 = clamp(pk * 4);           // 축
    const a1 = clamp((pk - 0.14) * 4);  // 겹친 점
    const a2 = clamp((pk - 0.34) * 3.4);// 괄호와 부챗살
    const a3 = clamp((pk - 0.48) * 3.2);// 상자
    const a4 = clamp((pk - 0.66) * 3);  // 상자 안 다섯과 두 끝 값

    /* ── 축 이름 ──────────────────────────────────── */
    if (spec.axisLabel) {
      ctx.globalAlpha = a0;
      ctx.textAlign = 'left';
      ctx.font = disp(800, fit(spec.axisLabel, 800, 12, 190, 9));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.axisLabel, 16, 28);
      ctx.globalAlpha = 1;
    }

    /* ── 계속 도는 것 ③ 겹친 점을 두른 고리 ───────────
       🔴 축보다 **먼저** 그린다. 반투명 강조색이라 흰 축선 위에 얹히면 두 색이 섞이는데,
       먼저 그리면 불투명한 흰 축이 교차점을 덮는다. */
    if (a1 > 0.02) {
      const r = 12 + 1.8 * Math.sin(frac(t / PULSE) * Math.PI * 2);
      ctx.globalAlpha = a1 * 0.55;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(CX, AY, r, 0, Math.PI * 2); ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 값 축 ────────────────────────────────────── */
    if (a0 > 0.02) {
      ctx.globalAlpha = a0;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(X0, AY); ctx.lineTo(X1, AY); ctx.stroke();

      ctx.strokeStyle = tone('track');
      for (let g = 0; g <= 10; g++) {
        const gx = X0 + SPAN * g / 10;
        ctx.beginPath(); ctx.moveTo(gx, AY - 7); ctx.lineTo(gx, AY); ctx.stroke();
      }
      ctx.globalAlpha = 1;
    }

    /* ── 겹쳐 있는 다섯 ───────────────────────────── */
    for (let i = 0; i < N; i++) {
      const a = Math.min(a1, clamp(a1 * 3 - i * 0.35));
      if (a <= 0.02) continue;
      ctx.globalAlpha = a;
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(CX + NUDGE[i], AY, 6.5, 0, Math.PI * 2); ctx.fill();
      ctx.globalAlpha = 1;
    }

    /* ── 그 점에 물리는 괄호 ──────────────────────── */
    if (a2 > 0.02) {
      ctx.globalAlpha = a2;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(CX - 8, 100); ctx.lineTo(CX - 8, 108);
      ctx.lineTo(CX + 8, 108); ctx.lineTo(CX + 8, 100);
      ctx.stroke();
      ctx.globalAlpha = 1;

      /* ── 계속 도는 것 ② 부챗살 ─────────────────── */
      ctx.globalAlpha = a2 * 0.85;
      ctx.setLineDash([8, 7]);
      ctx.lineDashOffset = -(t * 16) % 15;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(CX - 8, 109); ctx.lineTo(ZX0, ZY0); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(CX + 8, 109); ctx.lineTo(ZX1, ZY0); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;
    }

    /* ── 확대 상자 ────────────────────────────────── */
    if (a3 > 0.02) {
      ctx.globalAlpha = a3;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, ZX0, ZY0, ZW, ZY1 - ZY0, 6); ctx.stroke();
      ctx.globalAlpha = 1;

      /* ── 계속 도는 것 ① 상자를 훑는 검사선 ────────
         🔴 상자 **안쪽에만** 둔다. 두 끝 값은 상자 밖(아래)에 있어서 이 강조색 선이
         흰 숫자 위를 지나갈 일이 없다 — 같은 상자 안에서는 그리는 순서가 곧 겹침이다. */
      const sx = ZX0 + frac(t / SCAN) * ZW;
      ctx.save();
      roundRect(ctx, ZX0 + 2, ZY0 + 2, ZW - 4, ZY1 - ZY0 - 4, 5); ctx.clip();
      ctx.globalAlpha = a3 * 0.30;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.beginPath(); ctx.moveTo(sx, ZY0); ctx.lineTo(sx, ZY1); ctx.stroke();
      ctx.restore();
      ctx.globalAlpha = 1;

      /* 상자 두 끝을 아래로 내리는 자국 */
      ctx.globalAlpha = a3;
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(ZX0 + 1.5, ZY1); ctx.lineTo(ZX0 + 1.5, ZY1 + 9); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(ZX1 - 1.5, ZY1); ctx.lineTo(ZX1 - 1.5, ZY1 + 9); ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 상자 안의 다섯 ───────────────────────────── */
    for (let i = 0; i < N; i++) {
      const a = Math.min(a4, clamp(a4 * 2.6 - i * 0.3));
      if (a <= 0.02) continue;
      ctx.globalAlpha = a;
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(ZX0 + INNER[i] * ZW, 188, 6.5, 0, Math.PI * 2); ctx.fill();
      ctx.globalAlpha = 1;
    }

    /* ── 이 한 칸의 두 끝 ─────────────────────────
       🔴 상자 **밖**, 상자가 끝나는 자리 바로 아래다. 검사선·표식과 한 픽셀도 안 겹친다. */
    if (a4 > 0.02 && (spec.loLabel || spec.hiLabel)) {
      ctx.globalAlpha = a4;
      ctx.font = mono(700, 15); ctx.fillStyle = tone('ink');
      if (spec.loLabel) { ctx.textAlign = 'left'; ctx.fillText(String(spec.loLabel), ZX0, ZY1 + 28); }
      if (spec.hiLabel) { ctx.textAlign = 'right'; ctx.fillText(String(spec.hiLabel), ZX1, ZY1 + 28); }
      ctx.textAlign = 'left';
      ctx.globalAlpha = 1;
    }
  }
};
