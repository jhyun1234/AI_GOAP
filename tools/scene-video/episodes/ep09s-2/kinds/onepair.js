import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* onepair — 여기저기서 뜨던 말이 한 주기에 한 쌍만 남는다.

   원문: "실제로 이 명세서에는 그런 경계선이 여섯 갈래(ADR)로 정리돼 있었는데, 그중
   하나가 한 주기에 최대 한 쌍만 대화가 성립하도록 못박은 규칙이었습니다.
   대화가 흔해지면 그건 장면이 아니라 소음이 되니까요."

   🔴 이 회차의 기본 도형(대화가 성립하는 자리)이 여기서 **개수**로 나온다.
   위쪽에 말풍선이 빽빽하게 떠 있는 것이 소음이고, 그것이 작은 점으로 주저앉으면서
   아래에 **반경 2 원 하나와 그 안의 한 쌍**만 남는다.
   🔑 남는 원의 반지름 44 는 앞 샷(faceoff)이 88 에서 줄여 놓은 바로 그 값이다 —
   네 그림을 관통하는 원이 여기서 한 번 더, 이번에는 「한 개」라는 뜻으로 선다.

   🔴 **후보는 사라지지 않고 점으로 남는다.** 원문이 말하는 것은 '대화를 없앴다'가 아니라
   '성립을 한 쌍으로 제한했다'이므로, 기회는 계속 있고 성립만 하나여야 맞다.

   🔴 화면에 적는 수는 라벨의 **1** 하나뿐이고 원문 문장("한 주기에 최대 한 쌍")에서 왔다.
   흩어진 후보의 개수는 도형 파라미터다 — '많다'로만 읽히게 배치했고 숫자로 적지 않았다.

   🔴 마지막 샷이라 아웃트로 카드가 끝의 2.6초를 덮는다. 그래서 **페이오프(솎아냄과
   남는 한 쌍)를 전부 cue 0 안에서 끝낸다**(c0 0.74 · 0.92 에 완성). cue 1(예고 자막)에는
   두 표식을 잇는 선만 걸었고, lead 0.40 · span 0.06 으로 조여 카드보다 먼저 앉힌다. */

const FIELD_Y0 = 58, FIELD_Y1 = 146;
const RING_Y = 198, RING_R = 44;
const PAIR_DX = 32, PAIR_Y = 214, BUB_Y = 178;

/* fx, fy, 떠다니는 주기(초), 위상 — 값이 아니라 배치다 */
const CAND = [
  [0.05, 0.10, 2.6, 0.0], [0.24, 0.02, 3.4, 1.3], [0.44, 0.14, 2.1, 2.6],
  [0.65, 0.04, 3.9, 4.0], [0.86, 0.17, 2.9, 5.2], [0.02, 0.48, 3.2, 0.7],
  [0.22, 0.58, 2.4, 2.0], [0.43, 0.44, 3.7, 3.3], [0.63, 0.60, 2.2, 4.6],
  [0.84, 0.47, 2.8, 5.9], [0.13, 0.94, 3.5, 1.6], [0.74, 0.92, 2.5, 3.9]
];

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

    const X0 = 20, X1 = w - 20, cx = w * 0.5;

    const c0 = cue(spec.thinCue ?? 0, 0.15, 0.85);
    const app = clamp(c0 * 5);                                // 후보가 다 켜져 있다
    const tk = ease(clamp((c0 - 0.34) / 0.40));               // 솎아낸다
    const pk = ease(clamp((c0 - 0.52) / 0.40));               // 한 쌍이 남는다
    const ck = ease(cue(spec.keepCue ?? 1, 0.40, 0.06));      // 둘이 이어진다

    /* ── 후보들 — 말풍선이 점으로 주저앉는다 ─────────── */
    for (let i = 0; i < CAND.length; i++) {
      const [fx, fy, per, ph] = CAND[i];
      const a = clamp(app - i * 0.03);
      if (a <= 0.02) continue;
      const px = lerp(X0 + 18, X1 - 18, fx);
      const py = lerp(FIELD_Y0, FIELD_Y1, fy) + Math.sin(t * (Math.PI * 2 / per) + ph) * 7;

      const ba = a * (1 - tk);
      if (ba > 0.02) {
        ctx.globalAlpha = ba;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        roundRect(ctx, px - 13, py - 9, 26, 18, 5); ctx.stroke();
        ctx.beginPath();
        ctx.moveTo(px - 4, py + 9); ctx.lineTo(px + 1, py + 16); ctx.lineTo(px + 6, py + 9);
        ctx.stroke();
        ctx.globalAlpha = 1;
      }
      const da = a * tk;
      if (da > 0.02) {
        ctx.globalAlpha = da * 0.9;
        ctx.fillStyle = tone('sub');
        ctx.beginPath(); ctx.arc(px, py, 3.4, 0, Math.PI * 2); ctx.fill();
        ctx.globalAlpha = 1;
      }
    }

    /* ── 라벨 ─────────────────────────────────────── */
    if (spec.ruleLabel) {
      const a = clamp((c0 - 0.28) / 0.18);
      if (a > 0.02) {
        ctx.globalAlpha = a;
        ctx.textAlign = 'left';
        ctx.font = disp(900, fit(spec.ruleLabel, 900, 13, w - 44, 9));
        ctx.fillStyle = tone('accent');
        ctx.fillText(spec.ruleLabel, X0, 34);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 남는 자리 — 반경 2 원 ─────────────────────── */
    if (pk > 0.02) {
      ctx.globalAlpha = clamp(pk * 2.2) * 0.85;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.setLineDash([12, 9]);
      ctx.lineDashOffset = -(t * 15) % 21;
      ctx.beginPath(); ctx.arc(cx, RING_Y, RING_R * ease(clamp(pk * 1.3)), 0, Math.PI * 2); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;
    }

    /* ── 남는 한 쌍 ───────────────────────────────── */
    const pa = clamp(pk * 2.4 - 0.4);
    if (pa > 0.02) {
      /* 이어지는 선 — 예고 자막에서 그어진다 */
      if (ck > 0.02) {
        ctx.globalAlpha = pa * clamp(ck * 1.6);
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 5;
        const half = PAIR_DX * ease(ck);
        ctx.beginPath();
        ctx.moveTo(cx - half, PAIR_Y); ctx.lineTo(cx + half, PAIR_Y);
        ctx.stroke();
        ctx.globalAlpha = 1;
      }

      for (const s of [-1, 1]) {
        const px = cx + s * PAIR_DX;
        ctx.globalAlpha = pa;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        ctx.beginPath();                       // 가운데(상대)를 향해 뻗는다
        ctx.moveTo(px - s * 9, PAIR_Y); ctx.lineTo(px - s * 23, PAIR_Y);
        ctx.stroke();
        ctx.fillStyle = tone('ink');
        ctx.beginPath(); ctx.arc(px, PAIR_Y, 6.5, 0, Math.PI * 2); ctx.fill();
        ctx.globalAlpha = 1;
      }

      /* 말풍선 하나 — 점 셋의 밝기가 이어서 오간다(문턱 없는 연속 함수) */
      ctx.globalAlpha = pa;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, cx - 23, BUB_Y - 13, 46, 26, 7); ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(cx - 6, BUB_Y + 13); ctx.lineTo(cx + 1, BUB_Y + 22); ctx.lineTo(cx + 7, BUB_Y + 13);
      ctx.stroke();
      for (let i = -1; i <= 1; i++) {
        ctx.globalAlpha = pa * (0.4 + 0.6 * (0.5 + 0.5 * Math.sin(t * 3.4 - i * 2.1)));
        ctx.fillStyle = tone('accent');
        ctx.beginPath(); ctx.arc(cx + i * 10, BUB_Y, 2.8, 0, Math.PI * 2); ctx.fill();
      }
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
