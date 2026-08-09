import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* bothways — 두 줄이 서로의 빈 칸을 채워 준다. 위는 사람 줄(흰 칸), 아래는 AI 줄
   (강조 칸)이고, 화살이 양쪽으로 같은 수만큼 오간다.
   「AI가 사람을 부린다」도 「사람이 AI를 부린다」도 아니라는 것을 방향의 대칭으로만
   말한다 — 글자로 결론을 쓰지 않는다.
   연속 모션 = 두 방향 화살이 계속 오간다. 이건 한 번 일어난 사건이 아니라 매일의 모양이다.
   문자열 출처: Docs/CLAUDE.md 작업 프로토콜(Editor 안내) · M11 글 70행 · M8 글 28행. */

const DOWN = ['방향 결정', '판정'];        // 사람 → AI
const UP = ['구현', 'Editor 안내'];        // AI → 사람

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    const kEditor = ease(cue(0));   // 에디터 조작은 AI가 안내
    const kSym = ease(cue(1));      // 서로 못 하는 걸 대신한다
    const kHole = ease(cue(2));     // 이 경계에도 구멍이 있다

    const laneH = 46;
    const topY = 44, botY = h - 44 - laneH;
    const laneX = M, laneW = w - M * 2;

    // ── 두 줄 ─────────────────────────────────────
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, laneX, topY, laneW, laneH, 4); ctx.stroke();
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
    roundRect(ctx, laneX, botY, laneW, laneH, 4); ctx.stroke();

    ctx.font = mono(700, 10);
    ctx.fillStyle = tone('sub');
    ctx.fillText('HUMAN', laneX, topY - 10);
    ctx.fillText('AI', laneX, botY + laneH + 18);

    // ── 각 줄 안의 칸 ──────────────────────────────
    const slotW = 122, slotG = 16;
    const sx0 = laneX + 18;
    for (let i = 0; i < 2; i++) {
      const kIn = clamp((kSym - i * 0.2) / 0.5);
      // 위 : 사람이 내려보내는 것
      {
        const cx = sx0 + i * (slotW + slotG);
        ctx.save(); ctx.globalAlpha = clamp(0.3 + 0.7 * kIn);
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        roundRect(ctx, cx, topY + 10, slotW, laneH - 20, 3); ctx.stroke();
        let fs = 14; ctx.font = disp(800, fs);
        while (ctx.measureText(DOWN[i]).width > slotW - 14 && fs > 9) {
          fs -= 1; ctx.font = disp(800, fs);
        }
        const sw = ctx.measureText(DOWN[i]).width;
        ctx.fillStyle = tone('ink');
        ctx.fillText(DOWN[i], cx + (slotW - sw) / 2, topY + laneH / 2 + 5);
        ctx.restore();
      }
      // 아래 : AI가 올려보내는 것
      {
        const kIn2 = i === 1 ? kEditor : clamp((kSym - 0.1) / 0.5);
        const cx = w - M - 18 - slotW - i * (slotW + slotG);
        ctx.save(); ctx.globalAlpha = clamp(0.3 + 0.7 * kIn2);
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        roundRect(ctx, cx, botY + 10, slotW, laneH - 20, 3); ctx.stroke();
        let fs = 14; ctx.font = disp(800, fs);
        while (ctx.measureText(UP[i]).width > slotW - 14 && fs > 9) {
          fs -= 1; ctx.font = disp(800, fs);
        }
        const sw = ctx.measureText(UP[i]).width;
        ctx.fillStyle = tone('accent');
        ctx.fillText(UP[i], cx + (slotW - sw) / 2, botY + laneH / 2 + 5);
        ctx.restore();
      }
    }

    // ── 연속 모션 : 양방향 화살 ─────────────────────
    {
      const y0 = topY + laneH, y1 = botY;
      const PR = 2.4;
      const arrows = [
        { x: sx0 + slotW / 2, dir: 1, ph: 0.0 },
        { x: sx0 + slotW + slotG + slotW / 2, dir: 1, ph: 0.5 },
        { x: w - M - 18 - slotW / 2, dir: -1, ph: 0.25 },
        { x: w - M - 18 - slotW - slotG - slotW / 2, dir: -1, ph: 0.75 },
      ];
      for (const a of arrows) {
        const k = ((t / PR) + a.ph) % 1;
        const y = a.dir > 0 ? lerp(y0 + 4, y1 - 4, ease(k)) : lerp(y1 - 4, y0 + 4, ease(k));
        ctx.fillStyle = a.dir > 0 ? tone('ink') : tone('accent');
        ctx.beginPath();
        ctx.moveTo(a.x, y + a.dir * 7);
        ctx.lineTo(a.x - 6, y - a.dir * 5);
        ctx.lineTo(a.x + 6, y - a.dir * 5);
        ctx.closePath(); ctx.fill();
      }
      // 가운데 안내선
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.setLineDash([6, 8]);
      ctx.beginPath();
      ctx.moveTo(laneX + 6, (y0 + y1) / 2); ctx.lineTo(w - M - 6, (y0 + y1) / 2);
      ctx.stroke();
      ctx.setLineDash([]);
    }

    // ── 구멍 ──────────────────────────────────────
    if (kHole > 0.05) {
      const y0 = topY + laneH, y1 = botY;
      const gx = w / 2 + 40, gy = (y0 + y1) / 2;
      ctx.save(); ctx.globalAlpha = clamp(kHole);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
      const r = lerp(2, 15, ease(kHole));
      ctx.beginPath(); ctx.arc(gx, gy, r, 0, Math.PI * 2); ctx.stroke();
      ctx.font = mono(700, 10); ctx.fillStyle = tone('ink');
      ctx.fillText('HOLE', gx + r + 10, gy + 4);
      ctx.restore();
    }
  },
};
