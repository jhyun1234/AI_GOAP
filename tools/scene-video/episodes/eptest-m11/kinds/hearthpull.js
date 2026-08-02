import {
  ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, disp,
  setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* hearthpull — 가운데 칸 하나가 나머지를 붙잡던 중력이었다.

   기본 도형의 열 번째 변주이자 S1 commons 의 뒤집기. commons 에서는 가운데 칸이
   양쪽으로 팔을 뻗어 둘을 똑같이 먹였고, 여기서는 그 팔이 **거리 자체를 잡아두고
   있었다**는 것이 드러난다. 식사가 모닥불 앞에서만 되니 멀리 살 수가 없다.

   🔴 이 샷의 사건은 '흩어짐' 이 아니라 '왕복이 짧아짐' 이다. 집이 밖으로 나가는
   것과 동시에 화덕이 집 곁으로 따라붙어서, 끼니 왕복선이 길어지지 않고 오히려
   제자리로 줄어든다. 그게 원문이 말한 '중력이 풀렸다' 의 정확한 그림이다.

   계속 도는 것 = 집 다섯과 불 사이를 쉬지 않고 오가는 식사 표식 다섯. 이 왕복은
   한 번도 멈추지 않는다 — 멈추면 굶는다. 달라지는 것은 왕복의 길이뿐이다. */

const MEAL = 2.0;                        // 끼니 왕복 한 번의 초
const CX = 176, CY = 140;
const R_HOLD = 46, R_FREE = 92;
const N = 5;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const hk = ease(cue(spec.holdCue ?? 0, 0.15, 0.5));
    const mk = ease(cue(spec.mealCue ?? 1, 0.15, 0.5));
    const gk = ease(cue(spec.gravCue ?? 2, 0.15, 0.5));
    const fk = ease(cue(spec.freeCue ?? 3, 0.15, 0.8));
    const n = spec.houses || N;
    const settled = fk > 0.95;           // 🔑 이 샷의 이산 분기 — 셋째 칩이 여기서 처음 그려진다

    ctx.textBaseline = 'alphabetic';

    /* ── 머리말 ─────────────────────────────────── */
    ctx.globalAlpha = clamp(hk * 2);
    flame(ctx, 20, 20, 0.8, tone('ink'));
    ctx.textAlign = 'left';
    ctx.fillStyle = tone('ink'); ctx.font = disp(800, 11.5);
    ctx.fillText(spec.fire || '', 34, 20);
    ctx.globalAlpha = 1;

    if (mk > 0.02) {
      ctx.globalAlpha = clamp(mk * 2);
      ctx.textAlign = 'left';
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 9.5);
      ctx.fillText(spec.mealNote || '', 34, 36);
      ctx.globalAlpha = 1;
    }
    if (gk > 0.02) {
      ctx.globalAlpha = clamp(gk * 2) * lerp(1, 0.3, fk);
      ctx.textAlign = 'right';
      ctx.fillStyle = tone('accent'); ctx.font = disp(800, 11.5);
      right(ctx, spec.gravity || '', w - 14, 20, w);
      ctx.globalAlpha = 1;
    }

    /* ── 가운데 불 ──────────────────────────────── */
    ctx.globalAlpha = clamp(hk * 2) * lerp(1, 0.28, fk);
    flame(ctx, CX, CY, 1.5, tone('ink'));
    ctx.globalAlpha = 1;

    const r = lerp(R_HOLD, R_FREE, fk);
    const pull = clamp(gk * 2) * lerp(1, 0.15, fk);   // 중력이 읽히는 정도

    /* ── 다섯 채 + 각자의 왕복 ──────────────────── */
    for (let i = 0; i < n; i++) {
      const a = -Math.PI / 2 + (i / n) * Math.PI * 2;
      const c = Math.cos(a), s = Math.sin(a);
      const hx = CX + c * r, hy = CY + s * r;
      // 화덕은 집에 접선 방향으로 붙는다 — 반지름과 무관하게 늘 집 곁이다
      const px = -s, py = c;
      const ox = hx + px * 30, oy = hy + py * 30;
      // 끼니를 먹으러 가는 곳 : 공용 불 → 내 화덕
      const tx = lerp(CX, ox, fk), ty = lerp(CY, oy, fk);

      // 왕복선 — 중력이 세면 굵고 강조된다
      ctx.strokeStyle = pull > 0.35 ? tone('accent') : tone('track');
      ctx.lineWidth = lerp(3, 6, pull);
      ctx.globalAlpha = clamp(hk * 2);
      ctx.beginPath(); ctx.moveTo(hx, hy); ctx.lineTo(tx, ty); ctx.stroke();
      ctx.globalAlpha = 1;

      // 계속 도는 것 — 끼니가 오간다. 멈추면 굶는다.
      {
        const u = frac(t / MEAL + i / n);
        const pp = u < 0.5 ? u * 2 : (1 - u) * 2;
        const e = ease(pp);
        const mx = lerp(hx, tx, e), my = lerp(hy, ty, e);
        ctx.globalAlpha = clamp(hk * 2) * 0.95;
        ctx.fillStyle = tone('accent');
        roundRect(ctx, mx - 9, my - 7, 18, 14, 3); ctx.fill();
        ctx.globalAlpha = 1;
      }

      // 내 화덕 — 집이 밖으로 나갈 때 같이 따라붙는다
      if (fk > 0.35) {
        ctx.globalAlpha = clamp((fk - 0.35) * 3);
        flame(ctx, ox, oy, 0.75, tone('accent'));
        ctx.globalAlpha = 1;
      }

      ctx.globalAlpha = clamp(hk * 2);
      house(ctx, hx, hy + 13, 0.9, tone('ink'));
      ctx.globalAlpha = 1;
    }

    /* ── 정착 순서 ──────────────────────────────── */
    if (fk > 0.35) {
      /* 🔴 자리는 세 칸을 다 잡아 두고 그리기만 나중에 한다. 두 칸으로 폭을 재면
         셋째가 들어오는 순간 앞의 둘이 왼쪽으로 밀려 글자가 통째로 튄다. */
      const all = spec.order || [];
      const shown = settled ? 3 : 2;
      ctx.font = disp(800, 12);
      const ws = all.map(s => ctx.measureText(s).width + 22);
      const ARROW = 22;
      const total = ws.reduce((a, b) => a + b, 0) + ARROW * (all.length - 1);
      let x = clamp((w - total) / 2, 10, Math.max(10, w - 10 - total));
      ctx.globalAlpha = clamp((fk - 0.35) * 3);
      all.slice(0, shown).forEach((s, i) => {
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        setShadow(ctx, GLOW, 8, 0);
        roundRect(ctx, x, 250, ws[i], 26, 4); ctx.stroke();
        clearShadow(ctx);
        ctx.textAlign = 'center';
        ctx.fillStyle = tone('accent');
        ctx.fillText(s, x + ws[i] / 2, 268);
        x += ws[i];
        if (i < shown - 1) {
          ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
          ctx.beginPath();
          ctx.moveTo(x + 5, 263); ctx.lineTo(x + ARROW - 5, 263);
          ctx.moveTo(x + ARROW - 10, 258); ctx.lineTo(x + ARROW - 5, 263);
          ctx.lineTo(x + ARROW - 10, 268);
          ctx.stroke();
          x += ARROW;
        }
      });
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};

/* 불 — 삼각 불꽃 + 받침. 크기만 다르고 공용도 개인도 같은 도형이다. */
function flame(ctx, cx, cy, s, col) {
  const wdt = 13 * s, hgt = 17 * s;
  ctx.strokeStyle = col; ctx.lineWidth = 3;
  ctx.beginPath();
  ctx.moveTo(cx, cy - hgt);
  ctx.lineTo(cx + wdt, cy + hgt * 0.45);
  ctx.lineTo(cx - wdt, cy + hgt * 0.45);
  ctx.closePath(); ctx.stroke();
  ctx.beginPath();
  ctx.moveTo(cx - wdt - 4 * s, cy + hgt * 0.72);
  ctx.lineTo(cx + wdt + 4 * s, cy + hgt * 0.72);
  ctx.stroke();
}

function house(ctx, cx, baseY, s, col) {
  const bw = 24 * s, bh = 15 * s, rh = 11 * s;
  ctx.strokeStyle = col; ctx.lineWidth = 3;
  ctx.beginPath();
  ctx.moveTo(cx - bw / 2, baseY);
  ctx.lineTo(cx - bw / 2, baseY - bh);
  ctx.lineTo(cx, baseY - bh - rh);
  ctx.lineTo(cx + bw / 2, baseY - bh);
  ctx.lineTo(cx + bw / 2, baseY);
  ctx.closePath(); ctx.stroke();
  ctx.fillStyle = col;
  ctx.globalAlpha *= 0.85;
  ctx.fillRect(cx - bw / 2 + 3, baseY - bh + 3, bw - 6, bh - 4);
  ctx.globalAlpha /= 0.85;
}

function right(ctx, s, x, y, cw, pad = 12) {
  if (!s) return;
  const tw = ctx.measureText(s).width;
  ctx.fillText(s, tw < cw - pad * 2 ? clamp(x, pad + tw, cw - pad) : cw - pad, y);
}
