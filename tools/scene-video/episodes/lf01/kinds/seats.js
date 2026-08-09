import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect } from '../../../engine/lib.js';

/* seats — 같은 두 자리(PLANNER · EXECUTOR)에 누가 앉는가로 두 게임을 가른다.
   림월드는 플레이어가 위 자리에 앉고, 우리는 주민이 두 자리를 다 쥔다.
   계획 토큰은 어느 쪽이든 위에서 아래로 계속 흐른다 — 자리 임자만 다르고
   계획이 실행으로 내려가는 것은 같기 때문이다.
   문구 출처: M13 글 35행. */

const SEAT_H = 44;

function panel(ctx, x, y, pw, ph, title, top, bot, k) {
  ctx.globalAlpha = clamp(k);
  ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
  roundRect(ctx, x, y, pw, ph, 4); ctx.stroke();
  ctx.font = mono(700, 11); ctx.fillStyle = tone('sub');
  ctx.fillText(title, x + 14, y + 20);

  const sy1 = y + 34, sy2 = y + ph - SEAT_H - 14;
  const seat = (sy, label, who, accent) => {
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    roundRect(ctx, x + 14, sy, pw - 28, SEAT_H, 4); ctx.stroke();
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText(label, x + 24, sy + 16);
    if (who) {
      ctx.font = disp(800, 15);
      ctx.fillStyle = accent ? tone('accent') : tone('ink');
      ctx.fillText(who, x + 24, sy + 36);
    }
  };
  seat(sy1, 'PLANNER', top.who, top.accent);
  seat(sy2, 'EXECUTOR', bot.who, bot.accent);
  ctx.globalAlpha = 1;
  return { sy1, sy2, cx: x + pw / 2 };
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;
    const pw = (w - M * 2 - 22) / 2, ph = h - 48, py = 34;

    const kL = ease(cue(0));      // 첫 줄부터 왼쪽 판이 선다 (여기서 안 쓰면 첫 자막이 정지 구간이 된다)
    const kPlan = ease(cue(1));
    const kSeat = ease(cue(2));
    const kR = ease(cue(3));

    const A = panel(ctx, M, py, pw, ph, 'RIMWORLD - NO GOAP',
      { who: kPlan > 0.4 ? 'PLAYER' : '', accent: true },
      { who: kSeat > 0.4 ? 'CHARACTER' : '' },
      clamp(0.25 + 0.75 * kL));

    const B = panel(ctx, M + pw + 22, py, pw, ph, 'OURS - GOAP',
      { who: kR > 0.3 ? '주민' : '', accent: true },
      { who: kR > 0.5 ? '주민' : '', accent: true },
      clamp(0.25 + 0.75 * kR));

    // ── 계획이 아래로 흐른다 (양쪽 다) ───────────────
    const flow = (P, gap) => {
      const k = ((t + gap) % P) / P;
      return ease(k);
    };
    const drop = (col, live, gap) => {
      if (!live) return;
      const k = flow(2.4, gap);
      const y = lerp(col.sy1 + SEAT_H + 4, col.sy2 - 6, k);
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(col.cx, col.sy1 + SEAT_H + 4); ctx.lineTo(col.cx, col.sy2 - 6); ctx.stroke();
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(col.cx, y, 8, 0, Math.PI * 2); ctx.fill();
    };
    // 왼쪽 판의 흐름은 판이 서자마자 계속 돈다 — 계획이 실행으로 내려가는 것은
    // 두 게임 모두에서 참이고, 이 그림에서 유일하게 멈추지 않는 것이기도 하다.
    drop(A, kL > 0.4, 0);
    drop(B, kR > 0.5, 1.2);

    // ── 못 가는 길 표시 ───────────────────────────
    if (kR > 0.7) {
      const s = '자율성 = 정체성';
      ctx.font = disp(900, 15);
      const sw = ctx.measureText(s).width;
      ctx.globalAlpha = clamp((kR - 0.7) / 0.3);
      ctx.fillStyle = tone('accent');
      ctx.fillText(s, M + pw + 22 + pw - sw - 14, py - 10);
      ctx.globalAlpha = 1;
    }
  },
};
