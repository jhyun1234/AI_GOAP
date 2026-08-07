import {
  disp, mono, ease, clamp, lerp, spring,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* ledger — 쫓아가기를 걷어내고, 그 자리에 한 줄을 적는다.

   원문: "목수는 이제 의뢰인을 쫓아가지 않습니다. 일을 끝내면 '이 사람에게 보상을 줘야 한다'는
   빚만 기록해 둡니다."

   이 샷은 앞 두 샷의 좌표를 이어받아 **같은 자리에서 반대로** 움직인다.
   ① 앞 샷들에서 목수 → 의뢰인으로 뻗던 선이 이번엔 의뢰인 쪽 끝에서부터 **되감겨** 목수 안으로
      말려 들어가 사라진다. CHASE 라벨도 같이 꺼진다.
   ② 그 선이 사라진 뒤에야 아래에 원장 카드가 스프링으로 꽂힌다. 카드 안은 빚 한 줄이다.

   🔴 이 샷에서 두 표식은 **서로 다른 주기로 떠다닌다.** S1·S2 에서는 둘의 바운스 위상이 같아
   간격이 상수였는데, 여기서는 위상도 주기도 갈라 놓아 간격이 커졌다 작아졌다 한다 —
   쫓아가기를 버렸다는 말이 화면에서 참이 되는 지점이 거기다. 라벨로 설명하지 않는다.

   🔴 카드 안 빗금은 글자 아래에 깔지 않고 카드 바닥 띠에만 넣었다. 강조색 글자 위에 같은
   강조색 빗금을 겹치면 읽히지 않는다 — 밝기 차이를 알파로 만들면 팔레트가 흐려진다.

   🔴 화면에 숫자가 없다. 원문이 이 대목에서 부르는 수가 없기 때문이다.

   계속 도는 것 = 두 표식의 서로 다른 표류 · 위 가이드선의 점선 흐름 · 카드 바닥 띠의 빗금 흐름. */

const TRK_Y = 88;
const NAME_Y = 112;
const CARD_Y = 150, CARD_W = 214, CARD_H = 92;

function hatch(ctx, x, y, bw, bh, k, t, color) {
  if (k <= 0.004) return;
  ctx.save();
  ctx.beginPath(); ctx.rect(x, y, bw * k, bh); ctx.clip();
  ctx.strokeStyle = color; ctx.lineWidth = 3;
  const step = 12, off = (t * 13) % step;
  for (let d = -bh - step; d < bw + step; d += step) {
    ctx.beginPath();
    ctx.moveTo(x + d + off, y + bh);
    ctx.lineTo(x + d + off + bh, y);
    ctx.stroke();
  }
  ctx.restore();
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const fit = (txt, weight, start, max, min = 8, f = disp) => {
      let fs = start; ctx.font = f(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = f(weight, fs); }
      return fs;
    };

    /* span 0.85 — 이 샷은 자막이 한 줄이라 창을 넓게 잡아야 그림이 자막 끝까지 산다 */
    const c0 = cue(spec.ledgerCue ?? 0, 0.15, 0.85);
    const ret = ease(clamp(c0 / 0.34));
    const card = ease(clamp((c0 - 0.30) / 0.55));

    /* 서로 다른 주기로 떠다닌다 — 이제 쫓아가지 않는다 */
    const carpX = w * 0.30 + 16 * Math.sin(t * 0.95);
    const cliX = w * 0.70 + 22 * Math.sin(t * 1.6 + 1.2);

    /* ── 위층 : 걷히는 추격선 ─────────────────────── */
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.setLineDash([12, 10]);
    ctx.lineDashOffset = (t * 26) % 22;
    ctx.beginPath(); ctx.moveTo(20, TRK_Y); ctx.lineTo(w - 20, TRK_Y); ctx.stroke();
    ctx.setLineDash([]); ctx.lineDashOffset = 0;

    if (ret < 0.995) {
      const x1 = lerp(cliX - 12, carpX + 12, ret);
      ctx.globalAlpha = 1 - ret;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(carpX + 12, TRK_Y); ctx.lineTo(x1, TRK_Y); ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(x1 - 8, TRK_Y - 5); ctx.lineTo(x1, TRK_Y); ctx.lineTo(x1 - 8, TRK_Y + 5);
      ctx.stroke();
      if (spec.chaseLabel) {
        ctx.textAlign = 'center';
        ctx.font = disp(900, fit(spec.chaseLabel, 900, 12, 110, 8));
        ctx.fillStyle = tone('sub');
        ctx.fillText(spec.chaseLabel, (carpX + cliX) / 2, TRK_Y - 18);
      }
      ctx.globalAlpha = 1;
    }

    ctx.fillStyle = tone('ink');
    for (const x of [carpX, cliX]) {
      ctx.beginPath(); ctx.arc(x, TRK_Y, 8, 0, Math.PI * 2); ctx.fill();
    }
    ctx.textAlign = 'center';
    ctx.fillStyle = tone('sub');
    if (spec.carpLabel) {
      ctx.font = disp(900, fit(spec.carpLabel, 900, 11, 110, 8));
      ctx.fillText(spec.carpLabel, carpX, NAME_Y);
    }
    if (spec.cliLabel) {
      ctx.font = disp(900, fit(spec.cliLabel, 900, 11, 110, 8));
      ctx.fillText(spec.cliLabel, cliX, NAME_Y);
    }

    /* ── 원장 카드 ────────────────────────────────── */
    if (card > 0.01) {
      const sc = spring(card);
      const cw = CARD_W * sc, ch = CARD_H * sc;
      const cx = w / 2, cy = CARD_Y + CARD_H / 2;
      const x0 = cx - cw / 2, y0 = cy - ch / 2;

      ctx.globalAlpha = clamp(card * 3);
      setShadow(ctx, GLOW, 16);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, x0, y0, cw, ch, 9); ctx.stroke();
      clearShadow(ctx);

      ctx.textAlign = 'left';
      if (spec.debtLabel) {
        ctx.font = disp(900, fit(spec.debtLabel, 900, 17, cw - 34, 10));
        ctx.fillStyle = tone('accent');
        ctx.fillText(spec.debtLabel, x0 + 17, y0 + 30);
      }
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.globalAlpha = clamp(card * 3) * 0.45;
      ctx.beginPath(); ctx.moveTo(x0 + 16, y0 + 40); ctx.lineTo(x0 + cw - 16, y0 + 40); ctx.stroke();
      ctx.globalAlpha = clamp(card * 3);
      if (spec.debtRow) {
        ctx.font = mono(700, fit(spec.debtRow, 700, 13, cw - 34, 8, mono));
        ctx.fillStyle = tone('accent');
        ctx.fillText(spec.debtRow, x0 + 17, y0 + 62);
      }
      /* 바닥 띠 — 적힌 것이 채워진다. 글자 아래에는 깔지 않는다. */
      hatch(ctx, x0 + 16, y0 + ch - 24, cw - 32, 14, ease(clamp((card - 0.25) / 0.6)), t, tone('accent'));
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
