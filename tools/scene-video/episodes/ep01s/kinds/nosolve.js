import {
  disp, CJK, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* nosolve — 이 편이 이름을 붙이는 자리.

   원문이 직접 이름을 붙여 둔 개념("무해(無解)")이라 화면에 올려야 하고, 뒤의 S7
   "풀 수 없는 문제는, 주지 않는다"가 이 이름을 되받는다.

   그런데 이름표를 큰 글자로 띄우기만 하면 그건 텍스트 카드다. 그래서 **뜻을 먼저 그리고
   그 위에 이름을 앉혔다** — 목표에서 풀이로 내려가야 할 획이 매번 같은 높이에서 끊긴다.
   아래 '풀이' 자리는 끝까지 비어 있다. 그 끊기는 자리에 이름이 박힌다.
   회차의 기본 도형 안에서 이 그림의 몫은 '끊기는 획'이다. */

const DROP = 1.55;   // 시도 하나가 목표에서 끊기는 자리까지 내려오는 초
const TRIES = 4;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const cx = w / 2;
    const goalTop = 10, goalBot = 38;
    const breakY = 112;
    const slotTop = 226, slotBot = 268;

    const nk = ease(cue(spec.nameCue ?? 0, 0.2, 0.5));
    const gk = ease(cue(spec.glossCue ?? 1, 0.2, 0.55));

    ctx.textBaseline = 'alphabetic';

    // 목표 — 여기서 출발한다
    ctx.font = disp(800, 12);
    const gw = ctx.measureText(spec.goalLabel || '목표').width + 26;
    ctx.fillStyle = tone('ink');
    roundRect(ctx, cx - gw / 2, goalTop, gw, goalBot - goalTop, 4); ctx.fill();
    ctx.fillStyle = '#000'; ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
    ctx.fillText(spec.goalLabel || '목표', cx, (goalTop + goalBot) / 2 + 1);
    ctx.textBaseline = 'alphabetic';

    // 닿아야 할 길 — 점선으로만 남는다
    ctx.save();
    ctx.setLineDash([6, 7]);
    ctx.lineWidth = 3; ctx.strokeStyle = tone('track');
    ctx.beginPath(); ctx.moveTo(cx, goalBot + 4); ctx.lineTo(cx, slotTop - 4); ctx.stroke();
    ctx.restore();

    /* ── 계속 도는 것 ─────────────────────────────
       시도 넷이 차례로 내려오다 같은 높이에서 끊긴다. 이 반복이 '무해'의 정의 그 자체다. */
    ctx.lineCap = 'round'; ctx.lineWidth = 5;
    for (let i = 0; i < TRIES; i++) {
      const u = frac((t + 0.9) / DROP + i / TRIES);
      const y = lerp(goalBot + 6, breakY, u);
      const a = clamp(u / 0.12) * clamp((1 - u) / 0.22);
      if (a <= 0.01) continue;
      ctx.globalAlpha = a;
      ctx.strokeStyle = tone('ink');
      ctx.beginPath(); ctx.moveTo(cx, y - 16); ctx.lineTo(cx, y); ctx.stroke();
      // 끊기는 순간 그 자리가 잠깐 밝아진다
      if (u > 0.82) {
        ctx.globalAlpha = (u - 0.82) / 0.18 * (1 - u) / 0.18;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.moveTo(cx - 16, breakY + 3); ctx.lineTo(cx + 16, breakY + 3); ctx.stroke();
        ctx.lineWidth = 5;
      }
    }
    ctx.globalAlpha = 1; ctx.lineCap = 'butt';

    /* ── 이름 ─────────────────────────────────────
       끊기는 바로 그 자리에 박힌다. 이름이 자리를 갖는 게 이 샷의 전부다. */
    if (nk > 0.02) {
      const s = spring(nk);
      const name = spec.name || '';
      const origin = spec.origin || '';
      let base = 50;
      ctx.font = disp(900, base * 1.05);
      while (base > 20 && ctx.measureText(name).width + 74 > w - 40) {
        base -= 2; ctx.font = disp(900, base * 1.05);
      }
      ctx.font = disp(900, base * s);
      const nw = ctx.measureText(name).width;
      const oFont = `900 ${Math.round(base * 0.42)}px ${CJK}`;
      ctx.font = oFont;
      const ow = ctx.measureText(origin).width;
      const totalW = nw + 12 + ow;
      const x0 = cx - totalW / 2;

      ctx.globalAlpha = clamp(nk * 1.6);
      ctx.textAlign = 'left';
      setShadow(ctx, 'rgba(255,255,255,0.18)', 14, 0);
      ctx.font = disp(900, base * s);
      ctx.fillStyle = tone('ink');
      ctx.fillText(name, x0, 160);
      clearShadow(ctx);
      ctx.font = oFont;
      ctx.fillStyle = tone('accent');
      ctx.fillText(origin, x0 + nw + 12, 160);
      ctx.globalAlpha = 1;
    }

    /* ── 뜻 ───────────────────────────────────────
       밑줄이 왼쪽에서 오른쪽으로 그어지며 앉는다. */
    if (gk > 0.02 && spec.gloss) {
      ctx.textAlign = 'center';
      ctx.font = disp(800, 18);
      const tw = ctx.measureText(spec.gloss).width;
      ctx.globalAlpha = clamp(gk * 1.6);
      ctx.fillStyle = tone('ink');
      ctx.fillText(spec.gloss, cx, 196);
      ctx.globalAlpha = 1;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
      ctx.beginPath();
      ctx.moveTo(cx - tw / 2, 204);
      ctx.lineTo(cx - tw / 2 + tw * ease(gk), 204);
      ctx.stroke();
    }

    /* ── 풀이 자리 ────────────────────────────────
       끝까지 비어 있다. 이 빈 칸이 이름의 뜻이다. */
    ctx.save();
    ctx.setLineDash([7, 7]);
    ctx.lineWidth = 3; ctx.strokeStyle = tone('track');
    roundRect(ctx, cx - 78, slotTop, 156, slotBot - slotTop, 5); ctx.stroke();
    ctx.restore();
    ctx.textAlign = 'center';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 12);
    ctx.fillText(spec.slotLabel || '풀이', cx, (slotTop + slotBot) / 2 + 5);

    ctx.textAlign = 'left';
  }
};
