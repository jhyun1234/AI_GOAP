import {
  ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, disp,
  setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* zerodiv — 칸을 올릴 액션이 하나도 없어서 분모가 0이 된다.

   기본 도형의 여덟 번째 변주. 이번 칸은 '내 밭 개수' 이고, 그 칸을 움직일 수 있는
   액션이 카탈로그에 없다. 플래너는 목표까지 얼마나 남았는지 어림잡을 때 그 값을
   나눗셈에 쓰므로, 없으면 분모가 0이 된다.

   🔴 분수를 통째로 그린 것은 원문이 원인을 나눗셈으로 설명하기 때문이다.
   위에서 아래로 흐르던 값이 분수선에서 옆으로 흩어지는 것이 크래시의 그림이고,
   스택 추적이 쓸모없다는 말은 위쪽 칩 한 줄이 맡는다 — 로그 화면을 그리지 않는다.
   (로그를 그리면 이 회차의 어휘인 '칸' 에서 벗어난다.)

   계속 도는 것 = 분자에서 분수선으로 계속 내려가는 값 셋. 분모가 0이 되는 순간
   그 흐름의 진행 방향이 아래에서 옆으로 갈린다 — S8 drop(delayMs 1010)이
   그 프레임에 물려 있다. */

const POUR = 1.5;                        // 값이 분자에서 분수선까지 내려가는 초
const NUM = { x: 60, y: 30, w: 232, h: 32 };
const BAR_Y = 118;
const DEN = { x: 60, y: 126, w: 232, h: 32 };
const ROW = { x: 30, w: 292, h: 26, y0: 194, gap: 4 };

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const ck = ease(cue(spec.crashCue ?? 0, 0.15, 0.5));
    const dk = ease(cue(spec.divCue ?? 1, 0.15, 0.9));
    const mk = ease(cue(spec.missCue ?? 2, 0.15, 0.5));
    const rows = spec.rows || [];
    const zero = dk > 0.5;                // 🔑 이 샷의 이산 분기

    ctx.textBaseline = 'alphabetic';

    /* ── 칩 ─────────────────────────────────────── */
    ctx.globalAlpha = clamp(ck * 2);
    ctx.fillStyle = tone('accent');
    roundRect(ctx, 14, 9, 4, 12, 2); ctx.fill();
    ctx.textAlign = 'left';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 9.5);
    ctx.fillText(spec.chip || '', 25, 20);
    ctx.globalAlpha = 1;

    /* ── 분자 ───────────────────────────────────── */
    ctx.globalAlpha = clamp(ck * 2);
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, NUM.x, NUM.y, NUM.w, NUM.h, 4); ctx.stroke();
    ctx.textAlign = 'center';
    ctx.fillStyle = tone('ink'); ctx.font = disp(800, 12.5);
    put(ctx, spec.numer || '', NUM.x + NUM.w / 2, NUM.y + 21, w);
    ctx.globalAlpha = 1;

    /* ── 계속 도는 것 : 어림잡기 ────────────────── */
    for (let i = 0; i < 3; i++) {
      const k = frac(t / POUR + i * 0.34);
      let x = NUM.x + NUM.w / 2 + (i - 1) * 62;
      let y = lerp(NUM.y + NUM.h, BAR_Y - 9, k);
      let a = Math.sin(Math.PI * clamp(k * 1.06)) * 0.95;
      if (zero) {
        // 분모가 0이면 값이 분수선을 못 넘고 옆으로 흩어진다
        const spill = clamp((k - 0.62) / 0.38);
        y = lerp(NUM.y + NUM.h, BAR_Y - 9, Math.min(k, 0.62) / 0.62);
        x += (i === 0 ? -1 : i === 1 ? 0.55 : 1) * spill * 46;
        a *= (1 - spill * 0.85);
      }
      ctx.globalAlpha = clamp(ck * 2) * a;
      ctx.fillStyle = tone('accent');
      roundRect(ctx, x - 10, y - 8, 20, 16, 3); ctx.fill();
    }
    ctx.globalAlpha = 1;

    /* ── 분수선 ─────────────────────────────────── */
    ctx.globalAlpha = clamp(ck * 2);
    ctx.strokeStyle = zero ? tone('accent') : tone('ink'); ctx.lineWidth = 4;
    if (zero) setShadow(ctx, GLOW, 10, 0);
    ctx.beginPath(); ctx.moveTo(50, BAR_Y); ctx.lineTo(w - 50, BAR_Y); ctx.stroke();
    clearShadow(ctx);
    ctx.globalAlpha = 1;

    /* ── 분모 ───────────────────────────────────── */
    ctx.globalAlpha = clamp(ck * 2);
    ctx.strokeStyle = zero ? tone('accent') : tone('ink'); ctx.lineWidth = 3;
    if (zero) setShadow(ctx, GLOW, 10, 0);
    roundRect(ctx, DEN.x, DEN.y, DEN.w, DEN.h, 4); ctx.stroke();
    clearShadow(ctx);
    ctx.textAlign = 'center';
    if (zero) {
      ctx.fillStyle = tone('accent'); ctx.font = disp(900, 26);
      put(ctx, spec.zero || '0', DEN.x + DEN.w / 2, DEN.y + 26, w);
    } else {
      ctx.fillStyle = tone('ink'); ctx.font = disp(800, 12.5);
      put(ctx, spec.denom || '', DEN.x + DEN.w / 2, DEN.y + 21, w);
    }
    ctx.globalAlpha = 1;

    /* ── 갈라짐 + 이름 ──────────────────────────── */
    if (zero) {
      const zk = clamp((dk - 0.5) * 3);
      ctx.globalAlpha = zk;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      [-1, 1].forEach(s => {
        ctx.beginPath();
        ctx.moveTo(w / 2 + s * 62, 176);
        ctx.lineTo(w / 2 + s * (62 + 22 * zk), 176 - 9 * zk);
        ctx.moveTo(w / 2 + s * 62, 176);
        ctx.lineTo(w / 2 + s * (62 + 16 * zk), 176 + 8 * zk);
        ctx.stroke();
      });
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('accent'); ctx.font = disp(900, 15);
      put(ctx, spec.crash || '', w / 2, 181, w);
      ctx.globalAlpha = 1;
    }

    /* ── 액션 카탈로그 ──────────────────────────── */
    rows.forEach((r, i) => {
      const y = ROW.y0 + i * (ROW.h + ROW.gap);
      const a = clamp((mk - i * 0.1) * 2.4);
      if (a <= 0.02) return;
      const filled = !r.has && mk > 0.55;
      ctx.globalAlpha = a;
      ctx.strokeStyle = filled ? tone('accent') : tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, ROW.x, y, ROW.w, ROW.h, 3); ctx.stroke();
      ctx.textAlign = 'left';
      ctx.fillStyle = tone('ink'); ctx.font = disp(800, 12);
      ctx.fillText(r.name || '', ROW.x + 14, y + 18);
      ctx.textAlign = 'right';
      if (filled) {
        // 빠져 있던 한 줄이 뒤늦게 꽂힌다
        ctx.fillStyle = tone('accent'); ctx.font = disp(800, 11.5);
        right(ctx, spec.missing || '', ROW.x + ROW.w - 14, y + 18, w);
      } else {
        ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
        right(ctx, r.effect || '', ROW.x + ROW.w - 14, y + 18, w);
      }
      ctx.globalAlpha = 1;
    });

    /* ── 무죄 도장 ──────────────────────────────── */
    if (mk > 0.6) {
      const ik = clamp((mk - 0.6) * 3);
      ctx.globalAlpha = ik;
      ctx.font = disp(800, 12);
      const tw = ctx.measureText(spec.innocent || '').width;
      const bw = Math.min(w - 28, tw + 26);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, (w - bw) / 2, 262, bw, 26, 4); ctx.stroke();
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('ink');
      put(ctx, spec.innocent || '', w / 2, 280, w);
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
