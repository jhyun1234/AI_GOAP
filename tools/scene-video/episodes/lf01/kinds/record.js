import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect } from '../../../engine/lib.js';

/* record — 판이 끝나고 남는 줄, 그리고 그 줄이 왜 그 값이 됐는지.
   대비한 넷은 겨울 표식을 계속 지나가고, 대비하지 않은 넷은 첫 겨울 자리에 멈춰 있다.
   움직이는 쪽과 멈춘 쪽이 한 화면에 같이 있는 것이 이 마디의 전부다.
   문자열 출처: M14 글 3·5·28행(화면 문자열과 결론 문장 그대로). */

const LINE = '겨울 3번을 넘기고 Day 44에 쓰러졌다 · 최대 8명';

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 18, L = M, R = w - M;

    const kCard = ease(cue(1));
    const kSplit = ease(cue(2));
    const kSay = ease(cue(3));

    // ── 기록 카드 ─────────────────────────────────
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('RUN RECORD', L, 26);
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
    roundRect(ctx, L, 34, R - L, 46, 4); ctx.stroke();
    if (kCard > 0.02) {
      let fs = 21;
      ctx.font = disp(900, fs);
      while (fs > 11 && ctx.measureText(LINE).width > R - L - 30) { fs -= 1; ctx.font = disp(900, fs); }
      ctx.save();
      ctx.beginPath(); ctx.rect(L + 3, 37, (R - L - 6) * ease(kCard), 40); ctx.clip();
      ctx.fillStyle = tone('accent');
      ctx.fillText(LINE, L + 15, 65);
      ctx.restore();
    }

    // ── 겨울 표식이 있는 길 ────────────────────────
    const laneY = 132, x0 = L + 30, x1 = R - 22;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(x0, laneY); ctx.lineTo(x1, laneY); ctx.stroke();
    for (let i = 0; i < 3; i++) {
      const x = lerp(x0, x1, 0.16 + i * 0.34);
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(x, laneY - 22); ctx.lineTo(x, laneY + 22); ctx.stroke();
      ctx.font = mono(700, 9); ctx.fillStyle = tone('sub');
      ctx.fillText('W' + (i + 1), x + 5, laneY - 26);
    }

    // ── 대비한 넷 — 계속 지나간다 ───────────────────
    for (let i = 0; i < 4; i++) {
      const P = 6.4, k = ((t + i * 1.05) % P) / P;
      const x = lerp(x0, x1, k);
      ctx.fillStyle = tone('accent');
      ctx.beginPath(); ctx.arc(x, laneY, 9, 0, Math.PI * 2); ctx.fill();
    }

    // ── 대비 안 한 넷 — 첫 겨울 자리에 멈춰 있다 ──────
    if (kSplit > 0.05) {
      const fx = lerp(x0, x1, 0.16);
      ctx.globalAlpha = clamp(kSplit);
      for (let i = 0; i < 4; i++) {
        const x = fx - 44 + i * 24, y = laneY + 44;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(x - 7, y - 7); ctx.lineTo(x + 7, y + 7);
        ctx.moveTo(x + 7, y - 7); ctx.lineTo(x - 7, y + 7);
        ctx.stroke();
      }
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText('NO WINTER PREP  x4', fx + 52, laneY + 50);
      ctx.globalAlpha = 1;
    }

    // ── 결론 ─────────────────────────────────────
    if (kSay > 0.02) {
      const s = '기회는 세계가 열어 주지만, 잡느냐는 개체의 성격이 결정한다';
      let fs = 17;
      ctx.font = disp(900, fs);
      while (fs > 10 && ctx.measureText(s).width > R - L) { fs -= 1; ctx.font = disp(900, fs); }
      ctx.globalAlpha = clamp(kSay);
      ctx.fillStyle = tone('ink');
      ctx.fillText(s, L, h - 14);
      ctx.globalAlpha = 1;
    }
  },
};
