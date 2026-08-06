import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* spread — 한 점에 뭉쳐 있던 다섯이 레일 양끝까지 벌어지고, 그 아래에서 검사기가 계속 돈다.

   원문: "편차를 만드는 계산을 좀 더 값이 잘 흩어지는 방식(FNV-1a라는 해시 함수)으로
   갈아끼웠더니, 다섯 명의 초기 포만이 62.6에서 82.8까지 시원하게 흩어졌습니다." /
   "실제 주민 이름 다섯 개로 값이 충분히 흩어지는지 자동으로 검사하는 테스트를 걸어뒀습니다."

   🔴 앞 샷(clumped)과 **같은 축**을 쓴다. 거기서 다섯이 겹쳐 있던 자리에서 출발해 그대로
   벌어지므로, 두 샷을 이어 보면 같은 눈금 위에서 벌어진 일이 된다. 이 편의 기본 도형(자리)이
   여기서 처음으로 제 몫을 한다 — 앞의 셋은 "다섯이 한 자리"였고 여기서만 다섯 자리다.

   🔴 값은 양 끝 둘뿐이다(62.6 · 82.8). 원문이 준 것이 구간의 두 끝뿐이라 가운데 셋에는
   값을 안 붙였다 — 그 셋의 x 는 값이 아니라 도형 파라미터다. 평균도 표준편차도 안 그렸다.

   🔴 검사 칸에 「통과」 같은 판정을 안 적었다. 원문이 말한 것은 결과가 아니라 **매번 검사한다**
   는 습관이라, 검사기가 한 방향으로 계속 지나가는 것 자체가 그 뜻이다. 지나갈 때마다 위
   괄호가 매끈하게 밝아졌다 어두워지는데, 이건 문턱이 아니라 사인파라 "언제 켜진다"가 없다.

   계속 도는 것 = 레일 위를 흐르는 바늘(높이 54px), 검사 칸을 지나가는 검사기(46×26),
   그 위상에 물린 괄호의 밝기. 마지막 자막(예고)에 걸린 아래 레일은 아웃트로 카드가
   덮는 구간이라 덤이다 — 정적 판정에서 제외되는 자리이므로 여기에 기대지 않았다. */

const NEEDLE = 3.6;    // 레일 위 바늘
const CHECK = 2.2;     // 검사기 한 바퀴
const TARGET = [0.10, 0.35, 0.52, 0.70, 0.94];

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

    const N = Math.min(spec.people ?? 5, TARGET.length);
    const X0 = 20, X1 = w - 20, SPAN = X1 - X0;
    const RAIL = 100;
    const FROM = X0 + 0.79 * SPAN;              // clumped 에서 다섯이 겹쳐 있던 자리
    const BX0 = X0, BY0 = 198, BY1 = 224;       // 검사 칸

    const sk = ease(cue(spec.spreadCue ?? 0, 0.15, 0.70));
    const vk = ease(cue(spec.verifyCue ?? 1, 0.15, 0.70));
    const nk = ease(cue(spec.nextCue ?? 2, 0.15, 0.80));

    const fly = ease(clamp(sk / 0.8));
    const px = i => lerp(FROM, X0 + TARGET[i] * SPAN, fly);
    const ph = frac(t / CHECK);

    /* ── 축 이름과 갈아 끼운 것 ───────────────────── */
    if (spec.axisLabel) {
      ctx.globalAlpha = clamp(sk * 3);
      ctx.textAlign = 'left';
      ctx.font = disp(800, fit(spec.axisLabel, 800, 12, 150, 9));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.axisLabel, 16, 28);
      ctx.globalAlpha = 1;
    }
    if (spec.methodLabel) {
      ctx.globalAlpha = clamp(sk * 2.6);
      ctx.textAlign = 'right';
      ctx.font = mono(700, 14); ctx.fillStyle = tone('accent');
      ctx.fillText(String(spec.methodLabel), X1, 28);
      ctx.textAlign = 'left';
      ctx.globalAlpha = 1;
    }

    /* ── 값 축 ────────────────────────────────────── */
    const ax = clamp(sk * 4);
    if (ax > 0.02) {
      /* ── 계속 도는 것 ① 레일 위 바늘 ───────────────
         🔴 레일과 표식보다 **먼저** 그린다. 반투명 강조색이라 흰 선·흰 원 위에 얹히면 두 색이
         섞이는데, 먼저 그리면 불투명한 흰 것들이 덮는다.
         🔴 값 두 개(baseline 136)보다 위에서 끝난다 — 흰 숫자를 훑고 지나가지 않게. */
      const hx = X0 + frac(t / NEEDLE) * SPAN;
      ctx.globalAlpha = ax * 0.32;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 5;
      ctx.beginPath(); ctx.moveTo(hx, 66); ctx.lineTo(hx, 120); ctx.stroke();
      ctx.globalAlpha = 1;

      ctx.globalAlpha = ax;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(X0, RAIL); ctx.lineTo(X1, RAIL); ctx.stroke();

      ctx.strokeStyle = tone('track');
      for (let g = 0; g <= 10; g++) {
        const gx = X0 + SPAN * g / 10;
        ctx.beginPath(); ctx.moveTo(gx, RAIL - 7); ctx.lineTo(gx, RAIL); ctx.stroke();
      }
      ctx.globalAlpha = 1;
    }

    /* ── 벌어지는 다섯 ────────────────────────────── */
    for (let i = 0; i < N; i++) {
      const a = clamp(sk * 4);
      if (a <= 0.02) continue;
      ctx.globalAlpha = a;
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(px(i), RAIL, 8, 0, Math.PI * 2); ctx.fill();
      ctx.globalAlpha = 1;
    }

    /* ── 양 끝 값 ─────────────────────────────────
       🔴 가운데 셋에는 값이 없다. 원문이 준 것은 두 끝뿐이다.
       🔴 문턱을 0.70 → **0.60** 으로 당겼다(2026-08-06 검수). 0.70 이면 두 값이 나레이션보다
       **0.53초 늦게** 밝아져, 「육십이 점 육에서 팔십이 점 팔로」를 말하는 순간의 스틸에서
       정작 그 두 숫자가 흐렸다. 한도(±0.67초) 안이라 게이트는 통과했지만 이 편의 페이오프가
       바로 이 두 값이다 — 말과 그림이 같이 도착해야 한다. */
    const vlab = clamp((sk - 0.60) * 3.4);
    if (vlab > 0.02) {
      ctx.globalAlpha = vlab;
      ctx.font = mono(700, 15); ctx.fillStyle = tone('ink');
      ctx.textAlign = 'center';
      const half = ctx.measureText(String(spec.hiLabel ?? '')).width / 2;
      if (spec.loLabel) ctx.fillText(String(spec.loLabel), Math.max(X0 + 18, px(0)), 136);
      if (spec.hiLabel) ctx.fillText(String(spec.hiLabel), Math.min(X1 - half - 2, px(N - 1)), 136);
      ctx.textAlign = 'left';
      ctx.globalAlpha = 1;
    }

    /* ── 벌어진 폭 ───────────────────────────────── */
    const brk = clamp((sk - 0.75) * 4);
    if (brk > 0.02) {
      const pulse = vk > 0.05 ? 0.75 + 0.25 * Math.sin(ph * Math.PI * 2) : 1;
      ctx.globalAlpha = brk * pulse;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(px(0), 148); ctx.lineTo(px(0), 156);
      ctx.lineTo(px(N - 1), 156); ctx.lineTo(px(N - 1), 148);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 매번 도는 검사 ───────────────────────────── */
    if (vk > 0.02) {
      if (spec.testLabel) {
        ctx.globalAlpha = clamp(vk * 2.6);
        ctx.textAlign = 'left';
        ctx.font = disp(800, fit(spec.testLabel, 800, 12, 220, 9));
        ctx.fillStyle = tone('sub');
        ctx.fillText(spec.testLabel, 16, 190);
        ctx.globalAlpha = 1;
      }

      const ba = clamp((vk - 0.15) * 3);
      if (ba > 0.02) {
        ctx.globalAlpha = ba;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        roundRect(ctx, BX0, BY0, SPAN, BY1 - BY0, 5); ctx.stroke();
        ctx.globalAlpha = 1;

        /* ── 계속 도는 것 ② 칸을 지나가는 검사기 ───── */
        ctx.save();
        roundRect(ctx, BX0 + 2, BY0 + 2, SPAN - 4, BY1 - BY0 - 4, 4); ctx.clip();
        ctx.globalAlpha = ba * 0.30;
        ctx.fillStyle = tone('accent');
        ctx.fillRect(BX0 + ph * (SPAN + 46) - 46, BY0, 46, BY1 - BY0);
        ctx.restore();
        ctx.globalAlpha = 1;
      }
    }

    /* ── 다음 편 — 이번엔 값이 아니라 움직임이 한 자리에 ──
       🔴 이 구간은 아웃트로 카드가 픽셀 동일 좌표로 덮는다(2026-08-04 규약).
       그래서 여기에 뜻을 싣지 않았고, 정적 대책으로도 세지 않았다. */
    if (nk > 0.02) {
      const cx = X0 + 0.5 * SPAN;
      ctx.globalAlpha = nk * 0.8;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(X0, 268); ctx.lineTo(X1, 268); ctx.stroke();
      ctx.globalAlpha = 1;

      /* 고리를 표식보다 먼저 그린다 — 반투명 강조색을 흰 표식 위에 얹지 않는다 */
      const gk = clamp((nk - 0.6) * 3);
      if (gk > 0.02) {
        ctx.globalAlpha = gk * 0.9;
        setShadow(ctx, GLOW, 10, 0);
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.arc(cx, 268, 13, 0, Math.PI * 2); ctx.stroke();
        clearShadow(ctx);
        ctx.globalAlpha = 1;
      }

      for (let i = 0; i < N; i++) {
        ctx.globalAlpha = nk * 0.9;
        ctx.fillStyle = tone('ink');
        ctx.beginPath();
        ctx.arc(lerp(px(i), cx, ease(nk)), 268, 5.5, 0, Math.PI * 2);
        ctx.fill();
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
