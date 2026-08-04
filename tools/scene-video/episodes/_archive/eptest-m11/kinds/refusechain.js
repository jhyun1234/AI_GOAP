import {
  ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, disp,
  setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* refusechain — 칸의 남은 여유가 다섯 칸을 타고 내려가 거절이 된다.

   기본 도형의 여섯 번째 변주. 앞에서 '칸의 크기' 였던 상한 8 이 여기서는 사람을
   막는다. 원문 98행의 사슬을 그대로 세로로 세웠다 — 채집 목표 4 → 평상시 소지 4 →
   품삯 5 → 상한 8 초과 → 수령 불가 → 항상 거절.

   🔴 ep00i backchain 과 반대다. 저건 목표에서 거꾸로 되짚는 탐색이라 화살이 뒤로
   간다. 여기 화살은 앞으로만 간다 — 되짚을 데가 없다는 것이 이 샷의 주장이다
   ('어느 한 칸도 그 자체로는 틀리지 않았는데 결과가 고장').

   그래서 위 네 칸에는 '틀리지 않음' 이 붙고, 강조색은 마지막 칸에만 간다.
   고장은 칸에 있지 않고 칸들 사이에 있다.

   계속 도는 것 = 판정 토큰이 왼쪽 난간을 타고 내려와 매번 끝에서 떨어진다.
   이 사슬은 지금도 돌고 있고 매번 같은 자리에서 떨어진다. */

const RUN = 3.4;                         // 토큰이 사슬을 한 번 타는 초
const RAIL_X = 22;
const BOX = { x: 42, w: 280, h: 36, gap: 10, y0: 32 };
const N = 5;
const CHAIN_BOT = BOX.y0 + N * BOX.h + (N - 1) * BOX.gap;   // 252

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const ck = ease(cue(spec.chainCue ?? 0, 0.15, 0.5));
    const stk = ease(cue(spec.stockCue ?? 1, 0.15, 0.6));
    const ok = ease(cue(spec.overCue ?? 2, 0.15, 0.9));
    const bk = ease(cue(spec.badCue ?? 3, 0.15, 0.5));
    const links = spec.links || [];

    ctx.textBaseline = 'alphabetic';

    /* 각 칸이 켜지는 정도 — 자막이 부르는 순서 그대로 */
    const on = [
      clamp(stk * 2.4),
      clamp((stk - 0.35) * 2.4),
      clamp(ok * 2.2),
      clamp((ok - 0.45) * 2.2),
      clamp(bk * 2.2)
    ];
    /* 🔑 ok > 0.9 가 이 샷의 이산 분기다 — 네 번째 칸('수령 불가')의 테두리가
       ink → accent 로 갈린다. S6 thud(delayMs 2060)가 그 프레임에 물려 있다. */
    const blocked = ok > 0.9;

    /* ── 머리말 ─────────────────────────────────── */
    ctx.textAlign = 'left';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 9.5);
    ctx.globalAlpha = clamp(ck * 2);
    ctx.fillText(spec.okNote || '', BOX.x, 20);
    ctx.globalAlpha = 1;

    /* ── 난간 ───────────────────────────────────── */
    ctx.globalAlpha = clamp(ck * 2);
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(RAIL_X, BOX.y0); ctx.lineTo(RAIL_X, CHAIN_BOT + 8);
    ctx.stroke();
    ctx.globalAlpha = 1;

    /* ── 다섯 칸 ────────────────────────────────── */
    for (let i = 0; i < N; i++) {
      const y = BOX.y0 + i * (BOX.h + BOX.gap);
      const a = clamp(ck * 2) * (0.28 + 0.72 * on[i]);
      const last = i === N - 1;
      const hot = (last && bk > 0.4) || (i === 3 && blocked);

      ctx.globalAlpha = a;
      ctx.strokeStyle = hot ? tone('accent') : tone('ink'); ctx.lineWidth = 3;
      if (hot) setShadow(ctx, GLOW, 10, 0);
      roundRect(ctx, BOX.x, y, BOX.w, BOX.h, 4); ctx.stroke();
      clearShadow(ctx);

      ctx.textAlign = 'left';
      ctx.fillStyle = hot ? tone('accent') : tone('ink');
      ctx.font = disp(800, 11.5);
      ctx.fillText(links[i] || '', BOX.x + 14, y + 23);

      // 위 네 칸 — 이 칸 자체는 틀리지 않았다
      if (!last && on[i] > 0.5) {
        ctx.textAlign = 'right';
        ctx.fillStyle = tone('sub'); ctx.font = disp(700, 8.5);
        right(ctx, spec.okMark || '', BOX.x + BOX.w - 12, y + 23, w);
      }
      ctx.globalAlpha = 1;

      // 이음쇠 — 앞으로만 간다
      if (i < N - 1 && on[i] > 0.4) {
        ctx.globalAlpha = clamp((on[i] - 0.4) * 3);
        ctx.fillStyle = tone('sub');
        const my = y + BOX.h;
        ctx.beginPath();
        ctx.moveTo(BOX.x + 26, my + 8);
        ctx.lineTo(BOX.x + 18, my + 1);
        ctx.lineTo(BOX.x + 34, my + 1);
        ctx.closePath(); ctx.fill();
        ctx.globalAlpha = 1;
      }
    }

    /* ── 계속 도는 것 : 판정 토큰 ───────────────── */
    {
      const k = frac(t / RUN);
      const ty = lerp(BOX.y0, CHAIN_BOT + 26, k);
      const past = ty > CHAIN_BOT;
      ctx.globalAlpha = clamp(ck * 2) * (past ? clamp(1 - (ty - CHAIN_BOT) / 26) : Math.min(1, k * 8));
      /* 🔴 여기엔 GLOW 를 안 건다. 토큰이 캔버스 왼쪽 9px 자리에 상시 서 있어서
         blur 8 을 씌우면 잉크가 가장자리 띠에 닿는다(check.mjs 좌우 잘림 검사). */
      ctx.fillStyle = tone('accent');
      roundRect(ctx, RAIL_X - 13, ty - 12, 26, 24, 4); ctx.fill();
      ctx.globalAlpha = 1;
    }

    /* ── 결과 ───────────────────────────────────── */
    if (bk > 0.35) {
      ctx.globalAlpha = clamp((bk - 0.35) * 3);
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('accent'); ctx.font = disp(900, 15);
      setShadow(ctx, GLOW, 10, 0);
      put(ctx, spec.breakNote || '', w / 2, 278, w);
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};

function put(ctx, s, cx, y, cw, pad = 14) {
  if (!s) return;
  const tw = ctx.measureText(s).width;
  const x = tw < cw - pad * 2 ? clamp(cx, pad + tw / 2, cw - pad - tw / 2) : cw / 2;
  ctx.fillText(s, x, y);
}
function right(ctx, s, x, y, cw, pad = 12) {
  if (!s) return;
  const tw = ctx.measureText(s).width;
  ctx.fillText(s, tw < cw - pad * 2 ? clamp(x, pad + tw, cw - pad) : cw - pad, y);
}
