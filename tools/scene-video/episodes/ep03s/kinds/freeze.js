import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* freeze — 바닥 한 장만 남기고 그 위를 전부 갈아 끼운다.

   맨 아래 블록이 A* 플래너 엔진이다. lockCue 에서 이 블록만 강조색 테두리와 '보존' 표가
   붙고, rebuildCue 에서 그 위에 얹혀 있던 층 셋이 위에서부터 차례로 접혀 사라지고 같은
   자리에서 새 층 셋이 펴져 올라온다. 나가는 층은 이름이 없다(원문이 옛 배선의 이름을 대지
   않는다) — '···' 로만 둔다. 들어오는 셋은 원문 37행이 이름을 대는 것들이다.

   🔴 층을 가로로 밀어내지 않는다. 화면 폭이 좁아 밀어내면 상자가 가장자리에 몇 초씩
   걸쳐 있게 되고(잘림 판정), 무엇보다 이 단계는 '옆으로 치운 것'이 아니라 '걷어낸 것'이다.

   회차의 기본 도형 안에서 이 그림의 몫은 '한 장을 빼놓고 나머지를 바꾼다'.
   계속 도는 것 = 보존된 엔진 안에서 계속 돌고 있는 탐색 표식. 아래는 멈춘 적이 없다. */

const ORBIT = 3.4;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const above = spec.above || [];
    const n = above.length || 3;

    const lk = ease(cue(spec.lockCue ?? 0, 0.15, 0.6));
    const rk = ease(cue(spec.rebuildCue ?? 1, 0.15, 0.68));

    ctx.textBaseline = 'alphabetic';

    const bx = 26, bw = w - 52;
    const keepY = 206, keepH = 62;
    const layerH = 40, layerGap = 10;

    /* ── 갈아 끼우는 층 ─────────────────────────────
       k: 0 → 0.5 접힘(옛 층) / 0.5 → 1 펴짐(새 층). 위에서부터 차례로. */
    for (let i = 0; i < n; i++) {
      const y = 24 + i * (layerH + layerGap);
      const cy = y + layerH / 2;
      const k = clamp(rk * (n + 1.4) - i * 1.0);

      if (k < 0.52) {
        const fold = ease(clamp(k / 0.5));
        const sh = layerH * (1 - fold);
        ctx.globalAlpha = clamp(1 - fold * 1.15);
        ctx.lineWidth = 3; ctx.strokeStyle = tone('sub');
        ctx.setLineDash([7, 6]);
        roundRect(ctx, bx, cy - sh / 2, bw, Math.max(3, sh), 4); ctx.stroke();
        ctx.setLineDash([]);
        if (fold < 0.45) {
          ctx.textAlign = 'center'; ctx.fillStyle = tone('sub'); ctx.font = disp(900, 15);
          ctx.fillText(spec.anon || '···', bx + bw / 2, cy + 6);
        }
        ctx.globalAlpha = 1;
      } else {
        const grow = ease(clamp((k - 0.5) / 0.5));
        const sh = layerH * grow;
        ctx.globalAlpha = clamp(grow * 1.6);
        ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
        roundRect(ctx, bx, cy - sh / 2, bw, Math.max(3, sh), 4); ctx.stroke();
        if (grow > 0.6) {
          ctx.globalAlpha = clamp((grow - 0.6) / 0.3);
          ctx.textAlign = 'center'; ctx.fillStyle = tone('ink'); ctx.font = mono(700, 13);
          ctx.fillText(above[i] || '', bx + bw / 2, cy + 5);
        }
        ctx.globalAlpha = 1;
      }
    }

    /* ── 보존된 엔진 ───────────────────────────────── */
    {
      const hot = lk > 0.12;
      if (hot) setShadow(ctx, GLOW, 16, 0);
      ctx.lineWidth = hot ? 5 : 3;
      ctx.strokeStyle = hot ? tone('accent') : tone('ink');
      roundRect(ctx, bx, keepY, bw, keepH, 5); ctx.stroke();
      clearShadow(ctx);

      ctx.textAlign = 'left';
      ctx.fillStyle = hot ? tone('accent') : tone('ink'); ctx.font = mono(700, 14);
      ctx.fillText(spec.keepLabel || '', bx + 16, keepY + 27);
      ctx.fillStyle = tone('sub'); ctx.font = disp(800, 10.5);
      ctx.fillText(spec.keepNote || '', bx + 16, keepY + 46);

      /* 계속 도는 것 — 엔진 안에서 도는 탐색 표식.
         네 점을 잇는 닫힌 경로를 한 점이 계속 돈다. 아래는 멈춘 적이 없다는 뜻이다. */
      const px = bx + bw - 60, py = keepY + keepH / 2;
      const pts = [[px - 22, py - 14], [px + 4, py - 20], [px + 22, py + 2], [px - 6, py + 18]];
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath();
      pts.forEach((p, i) => i ? ctx.lineTo(p[0], p[1]) : ctx.moveTo(p[0], p[1]));
      ctx.closePath(); ctx.stroke();
      const u = frac(t / ORBIT) * pts.length;
      const a = pts[Math.floor(u) % pts.length], b = pts[(Math.floor(u) + 1) % pts.length];
      const f = u - Math.floor(u);
      ctx.fillStyle = hot ? tone('accent') : tone('ink');
      ctx.beginPath();
      ctx.arc(lerp(a[0], b[0], f), lerp(a[1], b[1], f), 4.5, 0, Math.PI * 2);
      ctx.fill();

      // 보존 표
      if (lk > 0.2) {
        const k = clamp((lk - 0.2) / 0.5);
        const tag = spec.keepTag || '보존';
        ctx.globalAlpha = k;
        ctx.textAlign = 'center';
        ctx.font = disp(900, 11);
        const tw = ctx.measureText(tag).width;
        setShadow(ctx, GLOW, 10, 0);
        ctx.fillStyle = tone('accent');
        roundRect(ctx, bx + bw / 2 - tw / 2 - 11, keepY - 13, tw + 22, 24, 3); ctx.fill();
        clearShadow(ctx);
        ctx.fillStyle = '#000';
        ctx.fillText(tag, bx + bw / 2, keepY + 3);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
