import {
  ease, clamp, lerp, frac, rnd,
  fitCanvas, mkCanvas, tone, roundRect, disp,
  setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* commons — 이름이 안 적힌 한 칸으로 두 하루가 수렴한다.

   이 회차의 기본 도형(칸과 거기 적힌 이름)의 첫 변주. 왼쪽 트랙에는 일한 흔적이
   빽빽하고 오른쪽은 텅 비었는데, 가운데 칸이 양쪽으로 팔을 뻗어 둘을 똑같이 먹인다.
   그래서 '하루의 끝' 눈금 위에서 두 표식이 정확히 같은 높이에 앉는다.
   칸 안의 이름 자리는 끝까지 비어 있다 — 그게 이 편의 원인이다.

   🔴 S10 hearthpull 과 정확히 뒤집힌 짝이다. 여기서는 가운데 하나가 팔을 뻗어
   붙잡고, 거기서는 그 붙잡음이 풀려 다섯 채가 밖으로 나간다. 같은 '가운데 하나 +
   왕복' 이지만 방향이 반대라 재탕이 아니다.

   계속 도는 것 = 창고에서 양쪽 팔로 흘러 나가는 식량 표식 넷. 이건 장식이 아니라
   이 샷의 주장 그 자체다(누가 채웠는지와 무관하게 계속 나간다). */

const FLOW = 2.4;                        // 식량이 팔을 한 번 지나가는 초
const COL_L = 76, COL_R = 276;           // 두 하루 트랙의 x
const TRACK_TOP = 48, TRACK_BOT = 206;
const BOX = { x: 112, y: 92, w: 128, h: 74 };
const ARM_Y = 129;
const END_Y = 252;                       // '하루의 끝' 눈금

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const L = spec.left || {}, R = spec.right || {};
    const dk = ease(cue(spec.dayCue ?? 0, 0.15, 0.5));
    const sk = ease(cue(spec.sameCue ?? 1, 0.15, 0.45));
    const bk = ease(cue(spec.blankCue ?? 2, 0.15, 0.5));

    ctx.textBaseline = 'alphabetic';
    ctx.lineCap = 'butt';

    /* ── 두 하루 트랙 ───────────────────────────── */
    [[COL_L, L], [COL_R, R]].forEach(([cx, P]) => {
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('ink'); ctx.font = disp(800, 13);
      put(ctx, P.name || '', cx, 22, w);
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 9.5);
      put(ctx, P.note || '', cx, 37, w);

      /* 🔴 track(알파 0.12)으로는 안 보인다. 오른쪽 트랙이 '비어 있다' 로 읽히려면
         트랙 자체가 먼저 보여야 한다 — 빈 것과 없는 것은 다른 말이다. */
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.globalAlpha = 0.45;
      ctx.beginPath(); ctx.moveTo(cx, TRACK_TOP); ctx.lineTo(cx, TRACK_BOT); ctx.stroke();
      ctx.globalAlpha = 1;

      /* 일한 흔적. 🔴 개수는 숫자가 아니라 질감이다 — 원문에 작업 횟수가 없으므로
         rnd 로 간격과 길이를 흩어 세어지지 않게 한다. */
      const n = P.ticks || 0;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      for (let i = 0; i < n; i++) {
        const y = TRACK_TOP + 14 + (TRACK_BOT - TRACK_TOP - 28) * ((i + 0.5) / n)
                + (rnd(i * 7 + 3) - 0.5) * 8;
        const len = 9 + rnd(i * 13 + 5) * 8;
        ctx.globalAlpha = clamp((dk - i / (n * 2.2)) * 3);
        ctx.beginPath(); ctx.moveTo(cx - len, y); ctx.lineTo(cx + len, y); ctx.stroke();
      }
      ctx.globalAlpha = 1;
    });

    /* ── 가운데 칸 : 공용 창고 ───────────────────── */
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, BOX.x, BOX.y, BOX.w, BOX.h, 5); ctx.stroke();

    ctx.textAlign = 'center';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
    put(ctx, spec.store || '', BOX.x + BOX.w / 2, BOX.y - 9, w);

    // 이름 자리 — 끝까지 비어 있다. blankCue 에서 강조색으로 갈린다.
    const nameCol = bk > 0.45 ? tone('accent') : tone('sub');
    ctx.fillStyle = nameCol; ctx.font = disp(700, 9.5);
    ctx.textAlign = 'left';
    ctx.fillText(spec.ownerLabel || '', BOX.x + 14, BOX.y + 27);
    ctx.setLineDash([6, 5]);
    ctx.strokeStyle = nameCol; ctx.lineWidth = 3;
    if (bk > 0.45) setShadow(ctx, GLOW, 9, 0);
    roundRect(ctx, BOX.x + 14, BOX.y + 36, BOX.w - 28, 24, 3); ctx.stroke();
    clearShadow(ctx);
    ctx.setLineDash([]);

    /* ── 팔 : 창고가 양쪽을 먹인다 ───────────────── */
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(BOX.x, ARM_Y); ctx.lineTo(COL_L, ARM_Y);
    ctx.moveTo(BOX.x + BOX.w, ARM_Y); ctx.lineTo(COL_R, ARM_Y);
    ctx.stroke();

    // 계속 도는 것 — 식량 표식이 팔을 타고 바깥으로 나간다(트랙보다 굵게 깔아야 보인다)
    ctx.fillStyle = tone('accent');
    for (let s = 0; s < 2; s++) {
      for (let i = 0; i < 2; i++) {
        const k = frac(t / FLOW + i * 0.5);
        const x = s === 0
          ? lerp(BOX.x, COL_L, k)
          : lerp(BOX.x + BOX.w, COL_R, k);
        const a = Math.sin(Math.PI * clamp(k * 1.15)) * 0.95;
        ctx.globalAlpha = a;
        roundRect(ctx, x - 8, ARM_Y - 6.5, 16, 13, 3); ctx.fill();
      }
    }
    ctx.globalAlpha = 1;

    /* ── 하루의 끝 ──────────────────────────────── */
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(36, END_Y); ctx.lineTo(w - 36, END_Y); ctx.stroke();

    ctx.textAlign = 'left';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
    ctx.fillText(spec.endLabel || '', 36, END_Y + 18);

    /* 두 표식이 내려와 같은 높이에 앉는다.
       🔑 sk > 0.9 가 이 샷의 유일한 이산 분기다 — 색이 갈리고 두 표식을 잇는 줄이
       켜진다. S1 tick(delayMs 1100)이 이 프레임에 물려 있다. */
    const landed = sk > 0.9;
    const bottom = lerp(224, END_Y - 3, sk);
    const col = landed ? tone('accent') : tone('ink');
    if (landed) setShadow(ctx, GLOW, 12, 0);
    ctx.fillStyle = col;
    [COL_L, COL_R].forEach(cx => {
      roundRect(ctx, cx - 13, bottom - 16, 26, 16, 3); ctx.fill();
    });
    clearShadow(ctx);
    if (landed) {
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.globalAlpha = clamp((sk - 0.9) * 12);
      ctx.beginPath();
      ctx.moveTo(COL_L + 13, bottom - 8); ctx.lineTo(COL_R - 13, bottom - 8);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};

/* 가운데 정렬 글자를 캔버스 안쪽에 가둔다. 🔴 폭 제한은 박스가 아니라 글자에 건다. */
function put(ctx, s, cx, y, cw, pad = 12) {
  if (!s) return;
  const tw = ctx.measureText(s).width;
  const x = tw < cw - pad * 2 ? clamp(cx, pad + tw / 2, cw - pad - tw / 2) : cw / 2;
  ctx.fillText(s, x, y);
}
