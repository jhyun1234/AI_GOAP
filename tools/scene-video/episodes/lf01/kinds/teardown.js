import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect } from '../../../engine/lib.js';

/* teardown — 세 밀스톤짜리 층이 걷히는 자리. 왼쪽 탑은 위에서부터 사라지고
   잔해가 계속 흘러내린다. 오른쪽 두 판은 세율만 다르고 결과가 같다 —
   두 기둥의 높이가 같은 것이 이 그림의 전부다.
   출처: Docs/M19_화폐전면철거와_실물마을_실행명세서.md 0절. */

const LAYERS = ['M18', 'M17', 'M16'];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 18;

    const kGone = ease(cue(0));
    const kSame = ease(cue(1));

    // ── 화폐 층 탑 ────────────────────────────────
    const tx = M, tw = 176, lh = 40, base = 190;
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('CURRENCY LAYER', tx, 40);
    for (let i = 0; i < LAYERS.length; i++) {
      const y = base - (LAYERS.length - i) * lh;
      const k = clamp(1 - (kGone * LAYERS.length - i));
      if (k <= 0.02) continue;
      ctx.globalAlpha = k;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, tx, y, tw, lh - 6, 3); ctx.stroke();
      ctx.font = disp(800, 15); ctx.fillStyle = tone('ink');
      ctx.fillText(LAYERS[i], tx + 14, y + 24);
      ctx.globalAlpha = 1;
    }
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(tx, base); ctx.lineTo(tx + tw + 18, base); ctx.stroke();

    // 잔해가 계속 흘러내린다
    for (let i = 0; i < 3; i++) {
      const P = 1.9, k = ((t + i * 0.63) % P) / P;
      const dy = lerp(base - 132, base - 6, ease(k));
      const dx = tx + 24 + i * 52;
      ctx.globalAlpha = clamp(1 - k * 0.85);
      ctx.fillStyle = tone('sub');
      ctx.fillRect(dx, dy, 34, 12);
      ctx.globalAlpha = 1;
    }

    // ── 두 판 ────────────────────────────────────
    const px = 268, pw = (w - M - px - 26) / 2;
    const cols = [{ tag: 'TAX 0%', x: px }, { tag: 'TAX 30%', x: px + pw + 26 }];
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('STARVED', px, 40);
    for (const c of cols) {
      const k = clamp(kSame * 1.2);
      ctx.globalAlpha = k;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      const bh = 96 * ease(k);
      roundRect(ctx, c.x, base - bh, pw, bh, 3); ctx.stroke();
      ctx.font = disp(900, 26); ctx.fillStyle = tone('accent');
      const s = '2';
      ctx.fillText(s, c.x + pw / 2 - ctx.measureText(s).width / 2, base - 14);
      ctx.font = disp(800, 13); ctx.fillStyle = tone('ink');
      ctx.fillText(c.tag, c.x, base + 24);
      ctx.globalAlpha = 1;
    }
    if (kSame > 0.7) {
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(px, base - 96 - 12); ctx.lineTo(px + pw * 2 + 26, base - 96 - 12);
      ctx.stroke();
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText('SAME RESULT', px, base - 96 - 20);
    }
  },
};
