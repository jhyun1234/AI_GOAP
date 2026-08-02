import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* weight — 조각 수는 그대로인데, 진 사람이 달라진다.

   55개의 작은 조각이 처음에는 한 상자 안에 빽빽하게 들어 있다. splitCue 에서 조각들이
   다섯 상자로 흩어져 각자 자리를 잡는다 — 개수는 하나도 줄지 않는다. 그게 원문 73행의
   요점이고, 이 편의 표제 숫자를 뒤집는 자리다. 조각이 사라지는 연출을 일부러 넣지 않았다.
   사라지는 것처럼 보이면 문장과 그림이 정반대가 된다.

   회차의 기본 도형 안에서 이 그림의 몫은 '같은 양이 다르게 놓인다'.
   계속 도는 것 = 55개 조각이 각자 다른 위상으로 크기가 숨 쉰다. */

const BREATHE = 2.7;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const total = spec.shards ?? 55;
    const groups = spec.groups ?? 5;
    const perG = Math.ceil(total / groups);

    const lk = ease(cue(spec.lumpCue ?? 0, 0.15, 0.65));
    const kk = ease(cue(spec.keepCue ?? 1, 0.15, 0.6));
    const sk = ease(cue(spec.splitCue ?? 2, 0.15, 0.72));

    ctx.textBaseline = 'alphabetic';

    // 덩이 (11열 × 5행)
    const BC = 11, bcw = 18, bch = 16;
    const bW = BC * bcw + 16, bH = Math.ceil(total / BC) * bch + 16;
    const bX = (w - bW) / 2, bY = 56;

    // 나뉜 상자 다섯
    const gW = 62, gGap = 5, gH = 76, gY = 60;
    const gX0 = (w - (groups * gW + (groups - 1) * gGap)) / 2;

    /* ── 상자 ─────────────────────────────────────── */
    ctx.globalAlpha = clamp(lk * 2) * (1 - clamp(sk * 1.6));
    ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
    roundRect(ctx, bX, bY, bW, bH, 4); ctx.stroke();
    ctx.globalAlpha = 1;

    for (let g = 0; g < groups; g++) {
      const k = clamp(sk * 1.9 - 0.5 - g * 0.12);
      if (k < 0.05) continue;
      ctx.globalAlpha = clamp(k * 1.6);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
      setShadow(ctx, GLOW, 10, 0);
      roundRect(ctx, gX0 + g * (gW + gGap), gY, gW, gH, 4); ctx.stroke();
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    /* ── 조각 ─────────────────────────────────────── */
    for (let i = 0; i < total; i++) {
      const u = i / total;
      const born = clamp(lk * 1.8 - u * 0.8);
      if (born < 0.02) continue;

      const bc = i % BC, br = Math.floor(i / BC);
      const x0 = bX + 8 + bc * bcw + bcw / 2;
      const y0 = bY + 8 + br * bch + bch / 2;

      const g = Math.floor(i / perG), gi = i % perG;
      const gc = gi % 3, gr = Math.floor(gi / 3);
      const x1 = gX0 + g * (gW + gGap) + 17 + gc * 16;
      const y1 = gY + 17 + gr * 15;

      const k = ease(clamp(sk * 1.45 - u * 0.45));
      const px = lerp(x0, x1, k), py = lerp(y0, y1, k);

      const ph = ((i * 37) % 100) / 100 * Math.PI * 2;
      const pulse = 1 + 0.16 * Math.sin(frac(t / BREATHE) * Math.PI * 2 + ph);
      const s = (k > 0.5 ? 9 : 10) * pulse * Math.min(1, born * 1.6);

      ctx.fillStyle = k > 0.5 ? tone('accent') : tone('ink');
      ctx.globalAlpha = clamp(born * 2) * (k > 0.5 ? 1 : 0.9);
      ctx.fillRect(px - s / 2, py - s / 2, s, s);
      ctx.globalAlpha = 1;
    }

    /* ── 이름 ─────────────────────────────────────── */
    ctx.textAlign = 'center';
    {
      const beforeA = clamp(lk * 2) * (1 - clamp(sk * 2));
      const afterA = clamp(sk * 2 - 0.6);
      if (beforeA > 0.02) {
        ctx.globalAlpha = beforeA;
        ctx.fillStyle = tone('sub'); ctx.font = disp(800, 11.5);
        ctx.fillText(spec.beforeLabel || '', w / 2, 40);
        ctx.globalAlpha = 1;
      }
      if (afterA > 0.02) {
        ctx.globalAlpha = afterA;
        ctx.fillStyle = tone('accent'); ctx.font = mono(700, 12.5);
        ctx.fillText(spec.afterLabel || '', w / 2, 40);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 코드가 사라진 게 아니라는 표 ──────────────── */
    if (kk > 0.02) {
      const chips = spec.keeps || [];
      chips.slice(0, 2).forEach((c, i) => {
        const k = clamp(kk * 2.4 - i * 0.9);
        if (k < 0.02) return;
        const y = 190 + i * 38;
        ctx.globalAlpha = clamp(k * 1.6);
        ctx.textAlign = 'center';
        let fs = 12; ctx.font = disp(800, fs);
        while (fs > 9 && ctx.measureText(c).width > w - 60) { fs -= 0.5; ctx.font = disp(800, fs); }
        const tw = ctx.measureText(c).width;
        ctx.lineWidth = 3; ctx.strokeStyle = tone('sub');
        roundRect(ctx, w / 2 - tw / 2 - 14, y, tw + 28, 28, 3); ctx.stroke();
        ctx.fillStyle = tone('ink');
        ctx.fillText(c, w / 2, y + 19);
        ctx.globalAlpha = 1;
      });
    }

    ctx.textAlign = 'left';
  }
};
