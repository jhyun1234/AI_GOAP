import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* oneasset — 흩어져 있던 네 조각이 에셋 한 장으로 모인다.

   왼쪽에 각기 다른 파일에 흩어져 있던 네 조각(이름 · 비용 · 전제 조건 · 효과)이 있고,
   gatherCue 에서 하나씩 오른쪽 ActionSO 카드의 제 칸으로 건너간다. 조각이 다 앉으면 아래에
   이 단계에서 만들어진 에셋의 내역이 뜬다. blockCue 에서는 '액션 이름으로 분기' 조각이
   위에서 내려오다 카드 앞의 차단선에 부딪혀 튕겨 나간다 — 원문 35행의 '원천 차단'이다.

   회차의 기본 도형 안에서 이 그림의 몫은 '넷이 하나가 되면 다섯 번째가 못 들어온다'.
   계속 도는 것 = 에셋 카드 테두리를 도는 표식. */

const ORBIT = 4.6;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const pieces = spec.pieces || [];
    const np = pieces.length || 4;

    const hk = ease(cue(spec.headCue ?? 0, 0.15, 0.6));
    const gk = ease(cue(spec.gatherCue ?? 1, 0.15, 0.72));
    const bk = ease(cue(spec.blockCue ?? 2, 0.15, 0.6));

    ctx.textBaseline = 'alphabetic';

    const srcX = 14, srcW = 112, srcTop = 74, srcPitch = 40, srcH = 30;
    const cardX = 190, cardW = 148, cardY = 66, cardH = 152;
    const rowTop = cardY + 40, rowPitch = 27;

    /* ── 왼쪽 : 흩어진 조각 ────────────────────────── */
    ctx.globalAlpha = clamp(hk * 2);
    ctx.textAlign = 'left';
    ctx.fillStyle = tone('sub'); ctx.font = disp(800, 10);
    ctx.fillText(spec.srcLabel || '각기 다른 파일', srcX, srcTop - 12);
    ctx.globalAlpha = 1;

    /* ── 오른쪽 : ActionSO 카드 ────────────────────── */
    {
      const s = Math.min(1, spring(clamp(hk * 1.6)));
      ctx.globalAlpha = clamp(hk * 2);
      ctx.lineWidth = 4; ctx.strokeStyle = tone('accent');
      setShadow(ctx, GLOW, 14, 0);
      roundRect(ctx, cardX + (cardW - cardW * s) / 2, cardY + (cardH - cardH * s) / 2,
        cardW * s, cardH * s, 5);
      ctx.stroke();
      clearShadow(ctx);
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('accent'); ctx.font = mono(700, 13);
      ctx.fillText(spec.assetName || '', cardX + cardW / 2, cardY + 24);
      ctx.globalAlpha = 1;

      /* 계속 도는 것 — 카드 테두리를 도는 표식 */
      const per = 2 * (cardW + cardH);
      const u = frac(t / ORBIT) * per;
      let mx, my;
      if (u < cardW) { mx = cardX + u; my = cardY; }
      else if (u < cardW + cardH) { mx = cardX + cardW; my = cardY + (u - cardW); }
      else if (u < 2 * cardW + cardH) { mx = cardX + cardW - (u - cardW - cardH); my = cardY + cardH; }
      else { mx = cardX; my = cardY + cardH - (u - 2 * cardW - cardH); }
      ctx.globalAlpha = clamp(hk * 2) * 0.85;
      ctx.fillStyle = tone('accent');
      ctx.fillRect(mx - 3.5, my - 3.5, 7, 7);
      ctx.globalAlpha = 1;
    }

    /* ── 조각이 건너간다 ───────────────────────────── */
    for (let i = 0; i < np; i++) {
      const sy = srcTop + i * srcPitch;
      const ty = rowTop + i * rowPitch;
      const k = clamp(gk * (np + 1.4) - i * 1.0);

      // 떠난 자리
      ctx.globalAlpha = clamp(hk * 2) * 0.5;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('track');
      ctx.setLineDash([5, 5]);
      roundRect(ctx, srcX, sy, srcW, srcH, 3); ctx.stroke();
      ctx.setLineDash([]);
      ctx.globalAlpha = 1;

      if (hk < 0.05) continue;
      const ek = ease(k);
      const px = lerp(srcX, cardX + 12, ek);
      const py = lerp(sy, ty, ek);
      const pw = lerp(srcW, cardW - 24, ek);
      const ph = srcH - 6 * ek;

      ctx.globalAlpha = clamp(hk * 2);
      ctx.lineWidth = 3;
      ctx.strokeStyle = k > 0.85 ? tone('accent') : tone('ink');
      roundRect(ctx, px, py, pw, ph, 3); ctx.stroke();
      ctx.textAlign = 'left';
      ctx.fillStyle = k > 0.85 ? tone('accent') : tone('ink');
      ctx.font = disp(800, 11);
      ctx.fillText(pieces[i] || '', px + 10, py + ph / 2 + 4);
      ctx.globalAlpha = 1;
    }

    /* ── 이 단계에서 만들어진 에셋 ─────────────────── */
    if (gk > 0.55) {
      const k = clamp((gk - 0.55) / 0.4);
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const txt = spec.assetsLine || '';
      let fs = 11; ctx.font = disp(800, fs);
      while (fs > 8 && ctx.measureText(txt).width > w - 32) { fs -= 0.5; ctx.font = disp(800, fs); }
      ctx.fillStyle = tone('sub');
      ctx.fillText(txt, w / 2, 250);
      ctx.globalAlpha = 1;
    }

    /* ── 못 들어오는 조각 ──────────────────────────── */
    if (bk > 0.02) {
      const barY = cardY - 16;
      ctx.globalAlpha = clamp(bk * 3);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      setShadow(ctx, GLOW, 12, 0);
      ctx.beginPath();
      ctx.moveTo(cardX - 6, barY); ctx.lineTo(cardX + cardW + 6, barY); ctx.stroke();
      clearShadow(ctx);
      ctx.globalAlpha = 1;

      // 위에서 내려오다 튕겨 나간다 — 0~0.45 접근, 0.45~1 되튕김
      const approach = ease(clamp(bk / 0.45));
      const bounce = ease(clamp((bk - 0.45) / 0.55));
      const cy = lerp(4, barY - 28, approach) + bounce * 20;
      const chipW = 118, chipX = cardX + cardW / 2 - chipW / 2;
      ctx.globalAlpha = clamp(bk * 4) * (1 - bounce * 0.85);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      roundRect(ctx, chipX, cy, chipW, 24, 3); ctx.stroke();
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('ink'); ctx.font = disp(800, 10.5);
      ctx.fillText(spec.blocked || '', chipX + chipW / 2, cy + 16);
      ctx.globalAlpha = 1;

      /* 차단 라벨 — 되튕기는 조각(x 205~323)과 겹치지 않게 왼쪽 빈 띠에 둔다. */
      if (bounce > 0.15) {
        ctx.globalAlpha = clamp((bounce - 0.15) / 0.4);
        ctx.textAlign = 'left';
        ctx.fillStyle = tone('accent'); ctx.font = disp(900, 11);
        ctx.fillText(spec.blockLabel || '원천 차단', 132, barY - 6);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
