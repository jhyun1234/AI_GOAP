import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* thirdway — 갈래 셋 중 하나를 고른다.

   왼쪽 한 점에서 갈래 셋이 뻗어 세 장의 선택지 카드로 간다. weighCue 에서 초점이 위에서
   아래로 한 장씩 옮겨 가며 셋을 저울질하고("유지냐, 폐기냐, 제3의 길이냐"), pickCue 에서
   고른 한 장만 강조색으로 남고 나머지 둘은 흐려지며 갈래에 걸쇠가 걸린다. 그 아래에
   원문 23행의 문장이 그대로 못박힌다.

   🔴 걸쇠는 '되돌리지 않는다'는 뜻이다. 이 편의 나머지 전부가 이 한 번의 결정 위에 얹힌다.

   회차의 기본 도형 안에서 이 그림의 몫은 '셋 중 하나로 잠근다'.
   계속 도는 것 = 세 갈래를 타고 나가는 표식. 고른 뒤에도 세 갈래 모두 흐르되, 버린 둘은
   흐릿하게만 흐른다 — 버려진 선택지도 계속 거기 있다. */

const FLOW = 2.9;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const opts = spec.options || [];
    const n = opts.length || 3;
    const pick = spec.pick ?? n - 1;

    const shk = ease(cue(spec.showCue ?? 0, 0.15, 0.65));
    const wk = ease(cue(spec.weighCue ?? 1, 0.15, 0.8));
    const pk = ease(cue(spec.pickCue ?? 2, 0.15, 0.4));

    ctx.textBaseline = 'alphabetic';

    const cardX = 46, cardW = w - cardX - 14, cardH = 46;
    const top = 26, gapY = 58;
    const rootX = 14, rootY = top + gapY + cardH / 2;

    // 뿌리
    ctx.globalAlpha = clamp(shk * 3);
    ctx.fillStyle = tone('sub');
    ctx.beginPath(); ctx.arc(rootX, rootY, 5, 0, Math.PI * 2); ctx.fill();
    ctx.globalAlpha = 1;

    const focus = Math.min(n - 1, Math.floor(wk * n * 0.999));

    for (let i = 0; i < n; i++) {
      const o = opts[i] || {};
      const y = top + i * gapY;
      const cy = y + cardH / 2;
      const vis = clamp(shk * (n + 1) - i);
      if (vis < 0.02) continue;

      const chosen = i === pick;
      const dropped = pk > 0.12 && !chosen;
      const alpha = dropped ? lerp(1, 0.32, clamp((pk - 0.12) / 0.5)) : 1;

      // 갈래
      ctx.globalAlpha = clamp(vis * 1.5) * alpha;
      ctx.strokeStyle = chosen && pk > 0.12 ? tone('accent') : tone('sub');
      ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(rootX + 6, rootY);
      ctx.lineTo(rootX + 16, rootY);
      ctx.lineTo(rootX + 16, lerp(rootY, cy, clamp(vis * 1.3)));
      ctx.lineTo(lerp(rootX + 16, cardX, clamp(vis * 1.3)), cy);
      ctx.stroke();

      /* 계속 도는 것 — 갈래를 타고 나가는 표식 */
      {
        const u = frac(t / FLOW + i * 0.27);
        const mx = lerp(rootX + 16, cardX, u);
        ctx.globalAlpha = (dropped ? 0.3 : 0.8) * (1 - Math.abs(u - 0.5) * 1.6) * clamp(vis * 1.5);
        ctx.fillStyle = chosen && pk > 0.12 ? tone('accent') : tone('sub');
        ctx.fillRect(mx - 3, cy - 3, 6, 6);
      }
      ctx.globalAlpha = clamp(vis * 1.5) * alpha;

      // 카드
      const s = Math.min(1, spring(vis));
      const sw = cardW * s, sh = cardH * s;
      const sx = cardX + (cardW - sw) / 2, sy = y + (cardH - sh) / 2;
      const hot = chosen && pk > 0.12;
      if (hot) setShadow(ctx, GLOW, 14, 0);
      ctx.lineWidth = hot ? 5 : 3;
      ctx.strokeStyle = hot ? tone('accent') : tone('ink');
      roundRect(ctx, sx, sy, sw, sh, 4); ctx.stroke();
      clearShadow(ctx);

      // 저울질 — 초점이 지나가는 카드에 왼쪽 굵은 표식
      if (wk > 0.02 && pk < 0.1 && i === focus) {
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 5;
        ctx.beginPath(); ctx.moveTo(sx + 3, sy + 8); ctx.lineTo(sx + 3, sy + sh - 8); ctx.stroke();
      }

      // 번호
      ctx.textAlign = 'left';
      ctx.fillStyle = hot ? tone('accent') : tone('sub');
      ctx.font = disp(900, 12);
      ctx.fillText(String(i + 1), sx + 12, sy + sh / 2 + 5);

      // 이름
      let fs = 13.5; ctx.font = disp(800, fs);
      while (fs > 9.5 && ctx.measureText(o.name || '').width > sw - 46) { fs -= 0.5; ctx.font = disp(800, fs); }
      ctx.fillStyle = hot ? tone('accent') : tone('ink');
      ctx.fillText(o.name || '', sx + 30, sy + sh / 2 + 5);

      // 걸쇠 — 고른 카드에만
      if (hot && pk > 0.25) {
        const lk = clamp((pk - 0.25) / 0.45);
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
        setShadow(ctx, GLOW, 10, 0);
        ctx.beginPath();
        ctx.moveTo(sx + sw - 22, sy + sh / 2 - 8 * lk);
        ctx.lineTo(sx + sw - 12, sy + sh / 2);
        ctx.lineTo(sx + sw - 22, sy + sh / 2 + 8 * lk);
        ctx.stroke();
        clearShadow(ctx);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 못박힌 문장 ───────────────────────────────── */
    if (pk > 0.3) {
      const k = clamp((pk - 0.3) / 0.5);
      const txt = spec.slogan || '';
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      let fs = 17; ctx.font = disp(900, fs);
      while (fs > 11 && ctx.measureText(txt).width > w - 40) { fs -= 0.5; ctx.font = disp(900, fs); }
      const s = Math.min(1, spring(k));
      ctx.font = disp(900, fs * s);
      setShadow(ctx, GLOW, 12, 0);
      ctx.fillStyle = tone('accent');
      ctx.fillText(txt, w / 2, 268);
      clearShadow(ctx);
      const tw = ctx.measureText(txt).width;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(w / 2 - tw / 2, 278); ctx.lineTo(w / 2 - tw / 2 + tw * k, 278);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
