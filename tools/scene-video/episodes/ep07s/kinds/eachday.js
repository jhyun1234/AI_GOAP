import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* eachday — 첫 샷의 덩어리가 여덟 갈래로 풀린다.

   이 그림은 oneheap 의 정확한 역동작이다. 표식의 개수·반지름·색이 같고 배치만 정반대라,
   마지막 샷이 첫 샷을 되받는다는 것이 설명 없이 읽힌다. 원문 5절의 문장이 그대로 이것이다 —
   "마을 한복판에 뭉쳐 똑같이 움직이던 무리가, 각자의 하루를 사는 사람들로 바뀌는 것."

   🔴 **계속 도는 것이 이 편의 결론을 진다.** oneheap 에서 몰린 여덟은 같은 주기로 돌아
   덩어리 모양이 안 변했다. 여기서는 여덟이 저마다 다른 주기(2.6 ~ 5.4초)로 자기 자리와
   목적지 사이를 왕복한다. 같은 도형 언어가 정반대 뜻으로 두 번 쓰인다.

   🔴 마지막 샷이라 정적 구간이 위험하다. 아웃트로 카드가 마지막 2.6초를 덮고 그 구간은
   판정에서 제외되므로, 문제는 **카드가 뜨기 전**이다. 대책 둘:
     ⓐ 왕복 여덟이 scatterCue 부터 샷 끝까지 한 번도 안 멈춘다(왕복 20~34px).
     ⓑ nextCue 에 면적 큰 변화를 건다 — 판 전체가 18px 위로 미끄러지고 아래에 계절 축이
       새로 그어지며 칩이 튀어 오른다. 칩만 띄우는 것으로는 부족하다는 것이 ep05s-3 검수에서
       실측됐다(반지름 5.5 짜리 표식이 도는 구간이 판정 문턱 아래였다).
   창은 좁게 잡았다(lead 0.20 · span 0.16) — 변화가 카드 앞에서 끝나야 한다. */

const HOME = [
  [0.13, 112], [0.38, 96], [0.63, 110], [0.88, 94],
  [0.11, 182], [0.37, 200], [0.64, 186], [0.89, 202]
];
/* 각자의 하루 — 방향(라디안)·거리(px)·주기(초). 여덟이 전부 다르다. */
const TRIP = [
  [-1.15, 26, 2.6], [2.35, 22, 3.0], [-0.65, 30, 3.4], [2.75, 20, 3.8],
  [1.05, 28, 4.2], [-2.20, 24, 4.6], [0.70, 34, 5.0], [-2.60, 21, 5.4]
];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const fit = (txt, weight, start, max, min = 8) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const N = Math.min(spec.people ?? 8, HOME.length);
    const sk = ease(cue(spec.scatterCue ?? 0, 0.15, 0.7));
    const nk = ease(cue(spec.nextCue ?? 1, 0.20, 0.16));

    const SHIFT = -18 * nk;                    // 판 전체가 위로
    const CX = w / 2, CY = 150;                // 첫 샷에서 물려받은 덩어리 자리
    const homeX = i => 30 + HOME[i][0] * (w - 60);
    const homeY = i => HOME[i][1] + SHIFT;

    /* ── 위쪽 라벨 둘 ──────────────────────────────── */
    ctx.font = disp(800, 12.5);
    ctx.textAlign = 'left';
    ctx.globalAlpha = clamp(1 - sk * 1.6);
    ctx.fillStyle = tone('sub');
    if (spec.beforeLabel) ctx.fillText(spec.beforeLabel, 8, 28);
    ctx.globalAlpha = 1;

    ctx.textAlign = 'right';
    ctx.globalAlpha = clamp(sk * 2 - 0.4);
    ctx.fillStyle = tone('accent');
    if (spec.afterLabel) ctx.fillText(spec.afterLabel, w - 8, 28);
    ctx.globalAlpha = 1;

    /* ── 여덟 ──────────────────────────────────────── */
    for (let i = 0; i < N; i++) {
      const out = ease(clamp(sk * 1.8 - i * 0.09));
      const hx = lerp(CX, homeX(i), out);
      const hy = lerp(CY + SHIFT, homeY(i), out);

      const [ang, dist, per] = TRIP[i];
      const dx = hx + Math.cos(ang) * dist;
      const dy = hy + Math.sin(ang) * dist;

      // 목적지 표식 — 흩어진 뒤에만
      if (out > 0.35) {
        ctx.globalAlpha = (out - 0.35) / 0.65 * 0.8;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.arc(dx, dy, 6, 0, Math.PI * 2); ctx.stroke();
        ctx.globalAlpha = 1;
      }

      // 각자의 하루 — 자기 자리와 목적지 사이를 오간다
      const k = out * (0.5 - 0.5 * Math.cos(frac(t / per) * Math.PI * 2));
      const px = lerp(hx, dx, k), py = lerp(hy, dy, k);

      setShadow(ctx, GLOW, 7, 0);
      ctx.fillStyle = tone('accent');
      ctx.beginPath(); ctx.arc(px, py, 5.5, 0, Math.PI * 2); ctx.fill();
      clearShadow(ctx);
    }

    /* 덩어리 테두리는 흩어지면서 사라진다 */
    const heap = clamp(1 - sk * 2.2);
    if (heap > 0.02) {
      ctx.globalAlpha = heap * 0.55;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.ellipse(CX, CY + SHIFT, 21, 17, 0, 0, Math.PI * 2);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 남는 한 줄 ────────────────────────────────── */
    if (spec.meaning) {
      const k = clamp((sk - 0.62) / 0.38);
      if (k > 0.02) {
        ctx.globalAlpha = k;
        ctx.textAlign = 'center';
        const fs = fit(spec.meaning, 900, 14, w - 30, 10);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('ink');
        ctx.fillText(spec.meaning, w / 2, 248 + SHIFT);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 예고 — 계절 축 하나가 새로 그어지고 칩이 뜬다 ── */
    if (nk > 0.02) {
      const AXW = Math.min(300, w - 40);
      ctx.globalAlpha = nk;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.moveTo(w / 2 - AXW / 2, 268);
      ctx.lineTo(w / 2 - AXW / 2 + AXW * nk, 268);
      ctx.stroke();
      ctx.globalAlpha = 1;

      if (spec.nextLabel) {
        const rise = (1 - nk) * 10;
        ctx.globalAlpha = nk;
        ctx.textAlign = 'center';
        const fs = fit(spec.nextLabel, 900, 14, w - 34, 10);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
        ctx.fillText(spec.nextLabel, w / 2, Math.min(h - 12, 288) + rise);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
