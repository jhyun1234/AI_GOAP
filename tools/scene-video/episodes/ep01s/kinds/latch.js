import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* latch — 그 길은 막혀 있다.

   화살표에 X 를 긋는 그림은 "안 된다"까지만 말한다. 이 대목이 말하는 건 조금 다르다 —
   **가고 싶은 쪽으로 계속 밀고 있는데 걸려서 되돌아온다**. 규칙은 한 번 선언되고 끝나는
   게 아니라 시도할 때마다 매번 되돌린다.
   그래서 밀고 나가는 획을 계속 살려 두고, 빗장을 그 앞에 떨어뜨렸다.
   회차의 기본 도형(뻗었다 되돌아오는 획)이 가장 문자 그대로 나오는 자리다.

   🔴 오른쪽 칸에 숫자를 안 쓴다. 원문은 "예산을 그냥 늘리면"이라고만 했지 얼마로 늘릴지를
      말한 적이 없다. 없는 수치는 만들지 않는다. */

const PUSH = 1.5;   // 획이 한 번 밀었다 되돌아오는 초

function fitFont(ctx, text, maxW, weight, size) {
  let s = size;
  ctx.font = disp(weight, s);
  while (s > 8 && ctx.measureText(text).width > maxW) { s -= 1; ctx.font = disp(weight, s); }
  return s;
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const BY = 96, BH = 56;
    const leftX = 14, leftW = 138;
    const rightX = 200, rightW = w - 14 - 200;
    const latchX = 176;

    const lk = ease(cue(spec.latchCue ?? 1, 0.2, 0.5));

    ctx.textBaseline = 'middle';

    // 지금 있는 예산
    ctx.fillStyle = '#000';
    roundRect(ctx, leftX, BY, leftW, BH, 6); ctx.fill();
    ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
    roundRect(ctx, leftX, BY, leftW, BH, 6); ctx.stroke();
    ctx.textAlign = 'center';
    ctx.fillStyle = tone('ink');
    fitFont(ctx, spec.from || '', leftW - 20, 900, 17);
    ctx.fillText(spec.from || '', leftX + leftW / 2, BY + BH / 2 + 1);

    // 가고 싶은 쪽
    ctx.save();
    ctx.setLineDash([7, 6]);
    ctx.lineWidth = 3; ctx.strokeStyle = tone('track');
    roundRect(ctx, rightX, BY, rightW, BH, 6); ctx.stroke();
    ctx.restore();
    ctx.fillStyle = tone('sub');
    fitFont(ctx, spec.to || '', rightW - 20, 800, 15);
    ctx.fillText(spec.to || '', rightX + rightW / 2, BY + BH / 2 + 1);

    /* ── 계속 도는 것 ─────────────────────────────
       밀고 나가려는 획. 빗장이 내려오면 더 짧게 튕긴다. */
    const u = frac(t / PUSH);
    let f;
    if (u < 0.45) f = ease(u / 0.45);
    else if (u < 0.56) f = 1;
    else if (u < 0.88) f = 1 - ease((u - 0.56) / 0.32);
    else f = 0;
    const reach = lk > 0.35 ? latchX - 14 : rightX - 6;
    const px = lerp(leftX + leftW + 6, reach, f);
    const cyMid = BY + BH / 2;

    ctx.lineCap = 'round'; ctx.lineWidth = 5;
    ctx.strokeStyle = tone('ink');
    ctx.beginPath(); ctx.moveTo(leftX + leftW + 6, cyMid); ctx.lineTo(px, cyMid); ctx.stroke();
    ctx.fillStyle = tone('ink');
    ctx.beginPath();
    ctx.moveTo(px + 7, cyMid); ctx.lineTo(px - 3, cyMid - 6); ctx.lineTo(px - 3, cyMid + 6);
    ctx.closePath(); ctx.fill();
    ctx.lineCap = 'butt';

    /* ── 빗장 ─────────────────────────────────────
       위에서 내려와 걸린다. 걸린 뒤로는 획이 여기서 되돌아간다. */
    if (lk > 0.02) {
      const dy = lerp(-86, 0, ease(lk));
      const bx = latchX - 6, by = BY - 24 + dy, bh = BH + 48;
      const hitting = (lk > 0.5 && f > 0.92) ? 1 : 0;
      ctx.globalAlpha = clamp(lk * 1.5);
      setShadow(ctx, GLOW, hitting ? 20 : 12, 0);
      ctx.fillStyle = tone('accent');
      roundRect(ctx, bx, by, 12, bh, 4); ctx.fill();
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    /* ── 조항 ─────────────────────────────────────
       빗장에 새겨진 문장. 원문 표현 그대로다. */
    if (lk > 0.05) {
      ctx.globalAlpha = clamp((lk - 0.05) * 1.6);
      const tag = spec.ruleTag || '규칙';
      ctx.font = disp(900, 11);
      const tw = ctx.measureText(tag).width + 18;
      ctx.fillStyle = tone('accent');
      roundRect(ctx, w / 2 - tw / 2, 196, tw, 22, 3); ctx.fill();
      ctx.fillStyle = '#000';
      ctx.textAlign = 'center';
      ctx.fillText(tag, w / 2, 207);

      ctx.fillStyle = tone('ink');
      const size = fitFont(ctx, spec.rule || '', w - 26, 800, 14);
      ctx.font = disp(800, size);
      ctx.fillText(spec.rule || '', w / 2, 240);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left'; ctx.textBaseline = 'alphabetic';
  }
};
