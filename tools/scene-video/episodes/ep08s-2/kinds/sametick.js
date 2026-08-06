import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* sametick — 제 순간을 하나씩 가지고 있던 다섯이 한 눈금 위로 미끄러져 거기 쌓인다.

   원문: "주민들의 초기 배고픔이 제각각이어야 자연스러운데, 다섯 명이 마치 짜기라도 한 듯
   똑같은 타이밍에 동시에 배고파졌던 겁니다." / 제보 원문: "동시에 허기 플랜이 실행됨"

   🔴 이 편의 기본 도형은 **자리**다 — 다섯 값이 축 위 어디에 앉는가. 네 그림이 전부
   그것 하나를 축만 바꿔 그린다(여기는 시간 축, 다음은 값 축, 그다음은 이름 → 값).

   🔴 축이 **하나뿐인** 것이 이 그림의 전부다. 트랙을 다섯 줄 깔면 "다섯이 나란히 간다"는
   그림이 되는데, 이 편의 사건은 나란히 가는 게 아니라 **다섯 개의 서로 다른 순간이 하나로
   붕괴한 것**이다. 그래서 축은 하나만 긋고, 다섯이 같은 x 로 모이면서 위로 쌓이게 했다.
   쌓인 기둥의 높이가 "몇 개가 여기 겹쳐 있는지"를 세게 해 준다 — 한 점에 포개 놓으면
   다섯인지 하나인지 셀 수가 없다.

   🔴 숫자를 한 자도 안 적었다. 원문에 "다섯 명"과 "동시에"는 있지만 그 순간이 몇 틱인지,
   각자 원래 몇 틱이었는지는 없다. 그래서 눈금에 값을 안 붙였고, 흩어진 다섯의 자리는
   값이 아니라 **도형 파라미터**다.

   계속 도는 것 = 축을 왼쪽에서 오른쪽으로 훑는 재생 머리(높이 180px)와 줄기 다섯의
   점선 흐름. 둘 다 t 의 함수이고 문턱이 없다. */

const SWEEP = 3.4;     // 재생 머리 한 바퀴(초)
const SPREAD = [0.13, 0.30, 0.46, 0.65, 0.86];   // 있어야 했던 다섯 순간
const MEET = 0.58;                                // 실제로 다섯이 모인 한 순간

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

    const N = Math.min(spec.people ?? 5, SPREAD.length);
    const X0 = 20, X1 = w - 20, SPAN = X1 - X0;
    const AXIS = 236, BASE = 198, STEP = 29;

    const ek = ease(cue(spec.spreadCue ?? 0, 0.15, 0.70));   // 있어야 할 모습
    const sk = ease(cue(spec.syncCue ?? 1, 0.15, 0.75));     // 실제로 벌어진 일

    const alive = i => clamp(ek * 4.2 - i * 0.62);
    const mx = i => lerp(X0 + SPREAD[i] * SPAN, X0 + MEET * SPAN, sk);
    const my = i => BASE - STEP * i * sk;
    const MX = X0 + MEET * SPAN;
    const ax = clamp(ek * 3.2);

    /* ── 무엇을 재는 화면인지 ─────────────────────── */
    if (spec.planLabel) {
      ctx.globalAlpha = ax;
      ctx.textAlign = 'left';
      ctx.font = disp(800, fit(spec.planLabel, 800, 12, 190, 9));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.planLabel, 16, 28);
      ctx.globalAlpha = 1;
    }

    /* ── 계속 도는 것 ① 재생 머리 ────────────────────
       🔴 **가장 먼저** 그린다. 반투명 강조색이라 흰 축·줄기·표식 위에 얹히면 두 색이 섞이는데,
       먼저 그리면 그 위에 불투명한 흰 선들이 덮어 준다. 같은 상자 안에서는 z 순서가 곧 겹침이다. */
    if (ax > 0.02) {
      const hx = X0 + frac(t / SWEEP) * SPAN;
      ctx.globalAlpha = ax * 0.30;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(hx, 60); ctx.lineTo(hx, AXIS + 6); ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 시간 축 하나 ─────────────────────────────── */
    if (ax > 0.02) {
      ctx.globalAlpha = ax;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(X0, AXIS); ctx.lineTo(X1, AXIS); ctx.stroke();

      ctx.strokeStyle = tone('track');
      for (let g = 0; g <= 12; g++) {
        const gx = X0 + SPAN * g / 12;
        ctx.beginPath(); ctx.moveTo(gx, AXIS); ctx.lineTo(gx, AXIS + 9); ctx.stroke();
      }
      ctx.globalAlpha = 1;

      if (spec.axisLabel) {
        ctx.globalAlpha = ax;
        ctx.textAlign = 'right';
        ctx.font = disp(800, fit(spec.axisLabel, 800, 12, 120, 9));
        ctx.fillStyle = tone('sub');
        ctx.fillText(spec.axisLabel, X1, 264);
        ctx.textAlign = 'left';
        ctx.globalAlpha = 1;
      }
    }

    /* ── 계속 도는 것 ② 줄기 다섯의 점선 ─────────────
       표식이 어느 눈금에 서 있는지를 잇는 선.
       🔴 다섯이 한 자리로 모이는 만큼 이 선들은 사그라든다. 두 가지 이유다 —
       ①뜻: 서로 다른 줄기 다섯이 **하나의 기둥으로 대체되는** 것이 이 샷의 사건이다.
       ②규약: 안 사그라들면 그 자리에서 강조색 기둥이 흰 점선 위에 겹쳐 두 색이 섞인다. */
    const merge = clamp((sk - 0.45) * 2.2);
    for (let i = 0; i < N; i++) {
      const a = alive(i);
      if (a <= 0.02) continue;
      ctx.globalAlpha = a * 0.75 * (1 - 0.88 * merge);
      ctx.setLineDash([7, 6]);
      ctx.lineDashOffset = -(t * 13) % 13;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(mx(i), my(i) + 11); ctx.lineTo(mx(i), AXIS - 3);
      ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;
    }

    /* ── 다섯이 한 자리에 겹쳤다는 증거 — 기둥을 꿰는 선 ── */
    const ck = clamp((sk - 0.55) * 3);
    if (ck > 0.02) {
      ctx.globalAlpha = ck;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.moveTo(MX, my(N - 1) - 13); ctx.lineTo(MX, AXIS - 3);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 축에 남는 자국 ───────────────────────────── */
    for (let i = 0; i < N; i++) {
      const a = alive(i);
      if (a <= 0.02) continue;
      ctx.globalAlpha = a;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(mx(i), AXIS - 8); ctx.lineTo(mx(i), AXIS + 8);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* 다섯 자국이 하나로 겹친 그 눈금 */
    const lk = clamp((sk - 0.72) * 4);
    if (lk > 0.02) {
      ctx.globalAlpha = lk;
      setShadow(ctx, GLOW, 10, 0);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 5;
      ctx.beginPath();
      ctx.moveTo(MX, AXIS - 12); ctx.lineTo(MX, AXIS + 14);
      ctx.stroke();
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    /* ── 표식 다섯 ────────────────────────────────── */
    for (let i = 0; i < N; i++) {
      const a = alive(i);
      if (a <= 0.02) continue;
      ctx.globalAlpha = a;
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(mx(i), my(i), 7.5, 0, Math.PI * 2); ctx.fill();
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
