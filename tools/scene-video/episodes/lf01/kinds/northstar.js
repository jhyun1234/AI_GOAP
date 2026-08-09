import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect } from '../../../engine/lib.js';

/* northstar — 이 게임의 '왜'. 축은 시간 하나뿐이고, 그 위에서 규모와 위협이 같이 자란다.
   겨울은 마디라서 밴드로 찍고, 시간 커서는 계속 흐른다 — 경주는 멈추지 않는다.
   출처: M14 글 26행 · Docs/CLAUDE.md 게임 건강 기준. */

const WINTERS = [0.20, 0.46, 0.72, 0.94];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 18, L = M, R = w - M, AXY = 148;

    const kName = ease(cue(1));
    const kBeat = ease(cue(2));
    const kThreat = ease(cue(3));

    // ── 이름 ─────────────────────────────────────
    if (kName > 0.02) {
      const s = '성장하며 버티는 기록 경주';
      ctx.font = disp(900, 22);
      const sw = ctx.measureText(s).width;
      ctx.globalAlpha = clamp(kName);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, L, 20, sw + 26, 34, 4); ctx.stroke();
      ctx.fillStyle = tone('accent');
      ctx.fillText(s, L + 13, 45);
      ctx.globalAlpha = 1;
    }

    // ── 축 ───────────────────────────────────────
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(L, AXY); ctx.lineTo(R, AXY); ctx.stroke();

    // ── 겨울 마디 ─────────────────────────────────
    WINTERS.forEach((f, i) => {
      const k = clamp((kBeat - i * 0.16) / 0.5);
      if (k <= 0.02) return;
      const x = lerp(L + 24, R - 34, f);
      ctx.globalAlpha = k;
      ctx.fillStyle = tone('track');
      ctx.fillRect(x, 70, 30, AXY - 70);
      ctx.font = mono(700, 9); ctx.fillStyle = tone('sub');
      ctx.fillText('W' + (i + 1), x + 4, 66);
      ctx.globalAlpha = 1;
    });

    // ── 규모 (위) ─────────────────────────────────
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.beginPath();
    for (let i = 0; i <= 40; i++) {
      const f = i / 40;
      const x = lerp(L, R, f), y = AXY - 4 - 62 * Math.pow(f, 0.9);
      if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
    }
    ctx.stroke();
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('VILLAGE SIZE', L + 4, AXY - 12);

    // ── 위협 (아래) ───────────────────────────────
    if (kThreat > 0.02) {
      const n = 12;
      for (let i = 0; i < n; i++) {
        const k = clamp(kThreat * n - i);
        if (k <= 0.02) continue;
        const x = lerp(L, R - 30, i / n);
        const bh = (10 + 46 * Math.pow(i / n, 1.2)) * ease(k);
        ctx.fillStyle = tone('accent');
        ctx.fillRect(x, AXY + 6, 24, bh);
      }
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText('THREAT TIER OVER TIME', L + 4, h - 12);
    }

    // ── 시간 커서 — 계속 흐른다 ────────────────────
    const P = 5.2, k = (t % P) / P;
    const cx = lerp(L, R, k);
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(cx, 66); ctx.lineTo(cx, AXY + 66); ctx.stroke();
    ctx.fillStyle = tone('ink');
    ctx.beginPath(); ctx.arc(cx, AXY, 8, 0, Math.PI * 2); ctx.fill();
  },
};
