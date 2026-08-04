import {
  ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, disp,
  setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* nameplate — 이름 없는 한 칸이 지워지고, 이름표 넷으로 갈라진다.

   기본 도형의 두 번째 변주. commons 에서 양쪽을 먹이던 그 칸이 여기서 사선으로
   그어져 사라지고, 그 안에서 하나로 오르내리던 식량 게이지가 넷으로 갈라져
   각자 자기 자리에서 따로 오르내린다. 갈라짐이 곧 개인화다.

   🔴 갈라지지 않는 것도 같이 그린다 — '목재 · 석재' 띠는 전폭 그대로 남는다.
   원칙('각자 먹고살되, 마을은 함께 짓는다')이 화면에 있어야 갈라짐의 경계가 읽힌다.

   계속 도는 것 = 게이지의 오르내림. 창고 하나일 때는 폭 112 로 크게 한 덩어리가,
   갈라진 뒤에는 넷이 서로 다른 위상으로. 갈라짐 자체가 움직임의 성격을 바꾼다. */

const BREATH = 3.1;                      // 게이지가 한 번 오르내리는 초
const STORE = { x: 112, y: 46, w: 128, h: 56 };
const CARD = { y: 118, w: 76, h: 74, gap: 8, x0: 12 };
const BAND = { x: 12, y: 206, w: 328, h: 30 };
const CELL = { y: 258, w: 22, h: 14, gap: 6 };

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const ek = ease(cue(spec.eraseCue ?? 0, 0.15, 0.5));
    const shk = ease(cue(spec.sharedCue ?? 1, 0.15, 0.5));
    const spk = ease(cue(spec.spreadCue ?? 2, 0.15, 0.5));
    const bins = spec.bins || [];
    const n = spec.people || 4;

    ctx.textBaseline = 'alphabetic';

    /* ── 원칙 띠 ────────────────────────────────── */
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(13.5, 12); ctx.lineTo(13.5, 34); ctx.stroke();
    ctx.textAlign = 'left';
    ctx.fillStyle = tone('ink'); ctx.font = disp(800, 11.5);
    left(ctx, spec.principle || '', 24, 27, w);

    /* ── 지워지는 칸 ────────────────────────────── */
    ctx.globalAlpha = lerp(1, 0.3, ek);
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, STORE.x, STORE.y, STORE.w, STORE.h, 5); ctx.stroke();
    ctx.textAlign = 'center';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
    put(ctx, spec.store || '', STORE.x + STORE.w / 2, STORE.y - 8, w);

    // 한 덩어리 게이지 — 계속 오르내린다
    {
      const gx = STORE.x + 8, gw = STORE.w - 16;
      const base = 26 + Math.sin(t * Math.PI * 2 / BREATH) * 10;
      ctx.globalAlpha = lerp(1, 0, ease(clamp(ek * 1.4)));
      ctx.fillStyle = tone('accent');
      roundRect(ctx, gx, STORE.y + STORE.h - 8 - base, gw, base, 3); ctx.fill();
    }
    ctx.globalAlpha = 1;

    /* 지움 획 — 칸 왼쪽에서 오른쪽으로 자란다.
       🔑 S2 sweep(delayMs 670)이 이 획의 한복판(ek=0.5)에 에너지 정점을 맞춘다. */
    if (ek > 0.01) {
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
      ctx.globalAlpha = 0.85;
      const x1 = lerp(STORE.x - 4, STORE.x + STORE.w + 4, ek);
      const y1 = lerp(STORE.y + STORE.h + 4, STORE.y - 4, ek);
      ctx.beginPath();
      ctx.moveTo(STORE.x - 4, STORE.y + STORE.h + 4); ctx.lineTo(x1, y1);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 이름표 넷 ──────────────────────────────── */
    ctx.textAlign = 'left';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 9.5);
    left(ctx, spec.peopleLabel || '', 12, CARD.y - 8, w);

    for (let i = 0; i < n; i++) {
      const cx = CARD.x0 + i * (CARD.w + CARD.gap);
      const a = clamp((ek - i * 0.09) * 2.2);
      if (a <= 0.02) continue;
      ctx.globalAlpha = a;

      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, cx, CARD.y, CARD.w, CARD.h, 4); ctx.stroke();

      // 이름이 적힌 자리 — 이름은 원문에 없으므로 획으로만 둔다(익명)
      ctx.fillStyle = tone('accent');
      setShadow(ctx, GLOW, 8, 0);
      roundRect(ctx, cx + 10, CARD.y + 9, (CARD.w - 20) * clamp(a * 1.2), 5, 2.5); ctx.fill();
      clearShadow(ctx);

      // 두 칸 — 주머니 / 곳간. 각자 따로 오르내린다.
      bins.forEach((b, j) => {
        const by = CARD.y + 24 + j * 25;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        roundRect(ctx, cx + 8, by, CARD.w - 16, 21, 3); ctx.stroke();
        const ph = (i * 0.37 + j * 0.53);
        const fill = 0.34 + 0.3 * (Math.sin((t / BREATH + ph) * Math.PI * 2) + 1) / 2;
        ctx.fillStyle = tone('accent');
        roundRect(ctx, cx + 10, by + 2, (CARD.w - 20) * fill, 17, 2); ctx.fill();
        ctx.fillStyle = tone('sub'); ctx.font = disp(700, 8.5);
        ctx.textAlign = 'left';
        ctx.fillText(b, cx + 13, by + 15);
      });
      ctx.globalAlpha = 1;
    }

    /* ── 갈라지지 않는 것 ───────────────────────── */
    if (shk > 0.02) {
      ctx.globalAlpha = clamp(shk * 1.5);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, BAND.x, BAND.y, BAND.w, BAND.h, 4); ctx.stroke();
      ctx.textAlign = 'left';
      ctx.fillStyle = tone('ink'); ctx.font = disp(800, 12);
      ctx.fillText(spec.shared || '', BAND.x + 14, BAND.y + 20);
      ctx.textAlign = 'right';
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 9.5);
      ctx.fillText(spec.sharedNote || '', BAND.x + BAND.w - 14, BAND.y + 20);
      ctx.globalAlpha = 1;
    }

    /* ── 열한 군데 ──────────────────────────────── */
    if (spk > 0.02) {
      const m = spec.spread || 11;
      const total = m * CELL.w + (m - 1) * CELL.gap;
      const x0 = (w - total) / 2;
      ctx.textAlign = 'left';
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 9.5);
      left(ctx, spec.spreadLabel || '', x0, CELL.y - 8, w);
      // 훑는 하이라이트 — 열한 칸을 계속 지나간다
      const headIdx = frac(t / 2.6) * m;
      for (let i = 0; i < m; i++) {
        const x = x0 + i * (CELL.w + CELL.gap);
        const a = clamp((spk - i / (m * 1.7)) * 2.4);
        if (a <= 0.02) continue;
        const near = clamp(1.25 - Math.abs(i + 0.5 - headIdx) * 0.85);
        ctx.globalAlpha = a;
        ctx.fillStyle = tone('accent');
        if (near > 0.5) setShadow(ctx, GLOW, 9, 0);
        const grow = 1 + near * 0.35;
        roundRect(ctx, x, CELL.y - (CELL.h * (grow - 1)) / 2,
          CELL.w, CELL.h * grow, 2.5);
        ctx.fill();
        clearShadow(ctx);
      }
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};

function put(ctx, s, cx, y, cw, pad = 12) {
  if (!s) return;
  const tw = ctx.measureText(s).width;
  const x = tw < cw - pad * 2 ? clamp(cx, pad + tw / 2, cw - pad - tw / 2) : cw / 2;
  const al = ctx.textAlign; ctx.textAlign = 'center';
  ctx.fillText(s, x, y); ctx.textAlign = al;
}
/* 왼쪽 정렬 글자도 오른쪽으로 새지 않게 시작점을 당긴다. */
function left(ctx, s, x, y, cw, pad = 12) {
  if (!s) return;
  const tw = ctx.measureText(s).width;
  ctx.fillText(s, tw < cw - pad * 2 ? Math.min(x, cw - pad - tw) : pad, y);
}
