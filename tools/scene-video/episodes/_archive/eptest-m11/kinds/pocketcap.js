import {
  ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, disp,
  setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* pocketcap — 칸에 천장이 생기자, 목표가 칸의 크기에 맞춰 다시 쓰인다.

   기본 도형의 네 번째 변주. 앞의 셋이 칸의 '개수' 였다면 여기는 칸의 '크기' 다.
   창고는 사실상 무한이었으니 '익은 밭을 전부 비운다' 가 가능했는데, 여덟 칸짜리
   주머니가 되자 두 번째 수확이 오른쪽 벽에 부딪혀 계획 자체가 안 선다.

   🔴 벽을 오른쪽 끝에 세운 것은 도형이 곧 뜻이기 때문이다 — 상한은 위에서 누르는
   뚜껑이 아니라 더 갈 수 없는 자리다. 그래서 넘친 곡식은 아래로 떨어지지 않고
   벽에 부딪혀 되돌아온다. S4 thud(delayMs 920)가 그 부딪힘에 물려 있다.

   계속 도는 것 = 익은 밭 셋에서 계속 떨어지는 곡식. 수확은 목표가 무엇이든 돈다. */

const FALL = 1.7;                        // 곡식이 밭에서 주머니까지 떨어지는 초
const FIELD_Y = 26, FIELD_H = 26, FIELD_W = 56;
const DROP_TOP = 56, DROP_BOT = 150;
const POCK = { y: 156, h: 44, x0: 18, w: 36, gap: 4 };
const WALL = POCK.x0 + 8 * POCK.w + 7 * POCK.gap;   // 334

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const ck = ease(cue(spec.capCue ?? 0, 0.15, 0.5));
    const ok = ease(cue(spec.oldCue ?? 1, 0.15, 0.5));
    const nk = ease(cue(spec.newCue ?? 2, 0.15, 0.5));
    const cap = spec.cap || 8, target = spec.target || 6;
    const nf = spec.fields || 3;

    ctx.textBaseline = 'alphabetic';

    /* ── 익은 밭 ────────────────────────────────── */
    ctx.textAlign = 'left';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 9.5);
    ctx.fillText(spec.fieldLabel || '', POCK.x0, 18);

    const fcx = [];
    for (let i = 0; i < nf; i++) {
      const cx = 68 + i * 108;
      fcx.push(cx);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, cx - FIELD_W / 2, FIELD_Y, FIELD_W, FIELD_H, 4); ctx.stroke();
      // 밭 안 이랑 — 무엇이 익었는지 읽히게
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      for (let j = 0; j < 3; j++) {
        const y = FIELD_Y + 8 + j * 6;
        ctx.beginPath();
        ctx.moveTo(cx - FIELD_W / 2 + 9, y); ctx.lineTo(cx + FIELD_W / 2 - 9, y);
        ctx.stroke();
      }
    }

    /* ── 계속 도는 것 : 떨어지는 곡식 ───────────── */
    ctx.fillStyle = tone('accent');
    fcx.forEach((cx, i) => {
      const k = frac(t / FALL + i * 0.33);
      const y = lerp(DROP_TOP, DROP_BOT, k);
      ctx.globalAlpha = Math.sin(Math.PI * clamp(k * 1.08)) * 0.95;
      roundRect(ctx, cx - 10, y - 8, 20, 16, 3); ctx.fill();
    });
    ctx.globalAlpha = 1;

    /* ── 주머니 여덟 칸 ─────────────────────────── */
    const shown = nk > 0.02
      ? lerp(cap, target, clamp(nk * 1.6))
      : cap * ease(clamp(ok / 0.5));

    for (let i = 0; i < cap; i++) {
      const x = POCK.x0 + i * (POCK.w + POCK.gap);
      ctx.globalAlpha = clamp(ck * 2);
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, x, POCK.y, POCK.w, POCK.h, 3); ctx.stroke();
      const f = clamp(shown - i);
      if (f > 0.02) {
        ctx.fillStyle = tone('accent');
        if (nk > 0.5) setShadow(ctx, GLOW, 8, 0);
        roundRect(ctx, x + 3, POCK.y + 3 + (POCK.h - 6) * (1 - f),
          POCK.w - 6, (POCK.h - 6) * f, 2); ctx.fill();
        clearShadow(ctx);
      }
      ctx.globalAlpha = 1;
    }

    /* 상한 벽 — 더 갈 수 없는 자리 */
    ctx.globalAlpha = clamp(ck * 2);
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
    setShadow(ctx, GLOW, 10, 0);
    ctx.beginPath();
    ctx.moveTo(WALL + 2, POCK.y - 8); ctx.lineTo(WALL + 2, POCK.y + POCK.h + 8);
    ctx.stroke();
    clearShadow(ctx);
    ctx.textAlign = 'right';
    ctx.fillStyle = tone('accent'); ctx.font = disp(800, 10.5);
    right(ctx, `${spec.capLabel || ''} ${cap}`, WALL + 4, POCK.y - 14, w);
    ctx.globalAlpha = 1;

    /* ── 두 번째 수확이 벽에 부딪힌다 ───────────── */
    if (ok > 0.02) {
      /* 🔑 ok = 0.72 가 이 샷의 유일한 이산 분기다 — 진행 방향이 오른쪽에서
         왼쪽으로 뒤집힌다. 벽에 닿는 그 프레임에 thud 를 물렸다. */
      const hit = ok > 0.72;
      const ox = hit
        ? lerp(WALL - 16, WALL - 74, clamp((ok - 0.72) / 0.28))
        : lerp(POCK.x0 + 5 * (POCK.w + POCK.gap), WALL - 16, clamp(ok / 0.72));
      ctx.globalAlpha = clamp(ok * 3) * (hit ? 1 : 0.9);
      ctx.fillStyle = tone('ink');
      roundRect(ctx, ox - 11, POCK.y - 32, 22, 18, 3); ctx.fill();
      if (hit) {
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        const sp = clamp((ok - 0.72) * 8);
        for (let i = -1; i <= 1; i++) {
          ctx.beginPath();
          ctx.moveTo(WALL - 6, POCK.y - 23 + i * 7);
          ctx.lineTo(WALL - 6 - 10 * sp, POCK.y - 23 + i * 12);
          ctx.stroke();
        }
      }
      ctx.globalAlpha = 1;

      ctx.textAlign = 'center';
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 9.5);
      ctx.globalAlpha = clamp((ok - 0.5) * 3);
      put(ctx, spec.overNote || '', w / 2, 216, w);
      ctx.globalAlpha = 1;
    }

    /* ── 목표 카드 둘 ───────────────────────────── */
    card(ctx, w, 224, spec.oldGoal || '', false, clamp(ok * 2), ok > 0.75 ? clamp((ok - 0.75) * 5) : 0);
    card(ctx, w, 256, spec.newGoal || '', true, clamp(nk * 2), 0);

    ctx.textAlign = 'left';
  }
};

function card(ctx, cw, y, text, hot, a, strike) {
  if (a <= 0.02) return;
  ctx.globalAlpha = a;
  ctx.font = disp(800, 12.5);
  const tw = ctx.measureText(text).width;
  const bw = Math.min(cw - 24, tw + 26);
  const bx = (cw - bw) / 2;
  ctx.strokeStyle = hot ? tone('accent') : tone('ink'); ctx.lineWidth = 3;
  if (hot) setShadow(ctx, GLOW, 10, 0);
  roundRect(ctx, bx, y, bw, 26, 4); ctx.stroke();
  clearShadow(ctx);
  /* 🔴 박스는 글자를 감싸고(bw = tw + 26), 글자는 다시 캔버스 안쪽에 가둔다.
     박스에만 폭 제한을 걸면 글자가 박스를 뚫고 나간다(ep02s S4 선례). */
  ctx.textAlign = 'center';
  ctx.fillStyle = hot ? tone('accent') : tone('sub');
  put(ctx, text, cw / 2, y + 18, cw, 14);
  if (strike > 0.02) {
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(bx + 8, y + 13);
    ctx.lineTo(bx + 8 + (bw - 16) * strike, y + 13);
    ctx.stroke();
  }
  ctx.globalAlpha = 1;
}

function put(ctx, s, cx, y, cw, pad = 12) {
  if (!s) return;
  const tw = ctx.measureText(s).width;
  const x = tw < cw - pad * 2 ? clamp(cx, pad + tw / 2, cw - pad - tw / 2) : cw / 2;
  ctx.fillText(s, x, y);
}
/* 오른쪽 정렬 글자가 왼쪽으로 새 나가지 않게 끝점을 안쪽으로 당긴다. */
function right(ctx, s, x, y, cw, pad = 12) {
  if (!s) return;
  const tw = ctx.measureText(s).width;
  ctx.fillText(s, tw < cw - pad * 2 ? clamp(x, pad + tw, cw - pad) : cw - pad, y);
}
