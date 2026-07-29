import {
  disp, mono, ease, easeOut, clamp, lerp, frac, depthGrad,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* channels — 하나였던 '멈춘 이유'가 네 갈래로 갈리고, 그중 한 갈래에만 값이 떨어진다.

   원문 93행이 관측 체계를 만들었다고 말하고, 109행이 그 출력을 보여준다.
     ByReason=[Plan=3, Res=0, Path=0, Pre=0]

   🔴 카드 네 장으로 그리지 않았다. ep01s S1B 가 같은 로그를 카드 그리드로 이미 썼고,
   무엇보다 이 편에서 이 로그가 하는 일은 "수치 4개 비교"가 아니라
   **원인이 어느 관으로 흘렀는지 가려내는 것**이다. 그래서 관 넷을 세우고 실제로
   흘려보냈다 — 계획 관에만 세 개가 떨어지고 나머지 셋은 끝까지 비어 있다.
   '경로' 관이 비어 있다는 사실이 이 편의 증명이고, 비어 있음은 카드의 0 보다
   빈 관 쪽이 훨씬 잘 읽힌다.

   위쪽 줄기에서는 신호가 계속 흘러 들어온다 — 관측은 이 순간만 도는 게 아니다. */

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const cx = w / 2;
    const tubes = spec.tubes || [];
    const n = Math.max(1, tubes.length);
    const M = 6, GAP = 10;
    const TW = (w - M * 2 - GAP * (n - 1)) / n;
    const TY = 78, TH = 150;
    const FORK = 58;
    const tubeX = i => M + i * (TW + GAP);

    const splitK = ease(cue(spec.splitCue ?? 0, 0.25, 0.55));
    const dropK = ease(cue(spec.dropCue ?? 1, 0.2, 0.6));
    const focusK = ease(cue(spec.focusCue ?? 2, 0.2, 0.55));

    ctx.textBaseline = 'alphabetic';

    /* ── 들어오는 줄기 ─────────────────────────── */
    ctx.textAlign = 'center';
    ctx.font = disp(900, 13);
    const iw = ctx.measureText(spec.inlet || '멈춘 이유').width + 26;
    ctx.fillStyle = '#000';
    roundRect(ctx, cx - iw / 2, 4, iw, 26, 4); ctx.fill();
    ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
    roundRect(ctx, cx - iw / 2, 4, iw, 26, 4); ctx.stroke();
    ctx.fillStyle = tone('ink');
    ctx.textBaseline = 'middle';
    ctx.fillText(spec.inlet || '멈춘 이유', cx, 18);
    ctx.textBaseline = 'alphabetic';

    ctx.lineWidth = 3; ctx.lineCap = 'round';
    ctx.strokeStyle = 'rgba(255,255,255,0.55)';
    ctx.beginPath(); ctx.moveTo(cx, 30); ctx.lineTo(cx, FORK); ctx.stroke();

    // 줄기 안을 계속 흐르는 것 — 이 샷이 한 번도 멎지 않는 이유
    ctx.fillStyle = tone('ink');
    for (let i = 0; i < 3; i++) {
      const u = frac(t / 1.15 + i / 3);
      ctx.beginPath(); ctx.arc(cx, lerp(30, FORK, u), 5, 0, Math.PI * 2); ctx.fill();
    }

    /* ── 갈라짐 ────────────────────────────────── */
    tubes.forEach((tb, i) => {
      const k = clamp(splitK * 1.3 - i * 0.07);
      if (k <= 0.02) return;
      const x = tubeX(i), mx = x + TW / 2;
      const focus = tb.focus ? focusK : 0;
      const dim = focusK > 0.05 && !tb.focus ? 1 - focusK * 0.55 : 1;

      // 갈래 선
      ctx.globalAlpha = clamp(k * 1.5) * dim;
      ctx.lineWidth = 3; ctx.lineCap = 'round';
      ctx.strokeStyle = 'rgba(255,255,255,0.55)';
      ctx.beginPath();
      ctx.moveTo(cx, FORK);
      ctx.lineTo(lerp(cx, mx, easeOut(k)), lerp(FORK, TY, easeOut(k)));
      ctx.stroke();

      // 관
      ctx.fillStyle = '#000';
      roundRect(ctx, x, TY, TW, TH, 6); ctx.fill();
      ctx.lineWidth = 3;
      if (focus > 0.05) setShadow(ctx, GLOW, 10 + 12 * focus, 0);
      ctx.strokeStyle = focus > 0.4 ? tone('accent') : tone('track');
      roundRect(ctx, x, TY, TW, TH, 6); ctx.stroke();
      clearShadow(ctx);

      /* 떨어지는 값 — 계획 관에만 떨어진다.
         숫자를 먼저 띄우지 않는다. 값이 실제로 도착한 뒤에 숫자가 붙는다. */
      if (tb.fill && dropK > 0.02) {
        const cnt = Math.max(0, Number(tb.value) || 0);
        for (let j = 0; j < cnt; j++) {
          const kk = clamp(dropK * 1.5 - j * 0.22);
          if (kk <= 0) continue;
          const restY = TY + TH - 18 - j * 24;
          const py = lerp(TY + 10, restY, easeOut(kk));
          ctx.fillStyle = tone('ink');
          roundRect(ctx, mx - 13, py - 9, 26, 18, 3); ctx.fill();
        }
      }

      // 숫자
      if (dropK > 0.25) {
        ctx.globalAlpha = clamp(dropK * 1.6 - 0.4) * dim;
        ctx.textAlign = 'center';
        const big = tb.focus && focus > 0.3;
        ctx.font = disp(900, big ? 40 : 30);
        if (big) {
          setShadow(ctx, GLOW, 20, 4);
          ctx.fillStyle = depthGrad(ctx, mx, TY + 44, mx, TY + 92, 'accent');
        } else ctx.fillStyle = tone('ink');
        ctx.fillText(String(tb.value), mx, tb.fill ? TY + 44 : TY + 88);
        clearShadow(ctx);
      }

      // 라벨
      ctx.globalAlpha = clamp(k * 1.5) * dim;
      ctx.textAlign = 'center';
      ctx.font = disp(800, 12.5);
      ctx.fillStyle = focus > 0.4 ? tone('accent') : tone('sub');
      ctx.fillText(tb.label, mx, TY + TH + 20);
      ctx.globalAlpha = 1;
    });

    /* ── 원문 로그 한 줄 ───────────────────────── */
    if (spec.caption && dropK > 0.2) {
      ctx.globalAlpha = clamp(dropK * 1.4 - 0.3) * 0.85;
      ctx.textAlign = 'center';
      let cs = 11;
      ctx.font = mono(700, cs);
      while (cs > 7 && ctx.measureText(spec.caption).width > w - 12) { cs -= 1; ctx.font = mono(700, cs); }
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.caption, cx, 274);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left'; ctx.textBaseline = 'alphabetic';
  }
};
