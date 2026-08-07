import {
  disp, ease, clamp, lerp, spring,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* settle — 마주치면 그 자리에서 갚고, 못 만나도 그대로 남는다.

   원문: "목수와 의뢰인이 마을을 돌아다니다가 자연스럽게 서로 가까워지는 순간, 바로 그 자리에서
   정산 장면이 뜨고 보상이 지급됩니다." / "이 방식의 가장 큰 장점은, 끝내 못 만나더라도 빚은
   사라지지 않는다는 점입니다. 예전처럼 60초 지났다고 품삯이 증발하지 않습니다."

   🔴 화면을 위아래 두 경우로 나눈 것은 원문이 한 문장 안에서 두 경우를 말하기 때문이다
   (만나면 / 끝내 못 만나면). 한쪽만 그리면 자막의 절반이 화면에 없다.
   ① meetCue 앞쪽 — 위 레인에서 두 표식이 다가와 붙고, 목수 위의 빚 캡슐이 의뢰인 위로
      건너간 뒤 체크가 그어지며 PAID 가 켜진다. **이 편의 페이오프이고 자막 앞쪽 절반에서
      끝난다** — 마지막 2.6초는 아웃트로 카드가 이 그림을 통째로 덮기 때문이다.
   ② meetCue 뒤쪽 — 아래 레인은 끝내 안 만나는 쌍이다. **S1 과 같은 60초 바**가 다시 차는데
      이번엔 빚 카드가 안 떨어진다. 테두리가 굵어지고 빗금이 끝까지 차고 밑줄이 그어진다.
      S1 에서 캡슐이 빠지던 그 동작의 자리를 비워 둔 것이 이 샷의 대사다.
   ③ holdCue(예고 자막) — 두 레인 밑으로 강조색 밑줄이 왼쪽에서 오른쪽으로 그어진다.
      예고 자막의 앞 0.7초쯤은 아웃트로 카드가 아직 안 덮으므로 그 구간에 큰 면적이 움직여야
      정적으로 안 잡힌다.

   🔴 흰 것과 강조색이 같은 픽셀에 안 겹치게 y 를 갈라 놓았다 — 빚 캡슐 60~84, 표식 96~112,
   60초 바 176~188, 아래 표식 204~220, 빚 카드 230~274, 밑줄 292. 어느 쌍도 안 만난다.

   🔴 화면에 올린 수는 60 하나뿐이고 S1 과 같은 문자열이다(원문에 두 번 실재).

   계속 도는 것 = 아래 두 표식의 서로 다른 표류 · 가이드선 점선 흐름 ·
   빚 캡슐 **바깥** 점선 후광의 흐름 · 아래 빚 카드 **바닥 띠**의 빗금 흐름.
   🔴 둘 다 글자를 안 지나간다(후광은 테두리 밖, 띠는 라벨 아래) — 아래 반려 수정 참조. */

const A_Y = 104, CAP_Y = 72, CAP_W = 78, CAP_H = 24;
const DIV_Y = 150;
const BAR_Y = 176, BAR_H = 12;
const B_Y = 212;
const CARD_Y = 230, CARD_W = 156, CARD_H = 44;
const RULE_Y = 292;

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

    const fit = (txt, weight, start, max, min = 8) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const c0 = cue(spec.meetCue ?? 0, 0.15, 0.8);
    const c1 = cue(spec.holdCue ?? 1, 0.15, 0.7);

    const meet = ease(clamp(c0 / 0.42));
    const pass = ease(clamp((c0 - 0.40) / 0.14));
    const stamp = ease(clamp((pass - 0.80) / 0.20));
    const hold = ease(clamp((c0 - 0.56) / 0.40));
    const rule = ease(clamp(c1 / 0.7));

    /* ── 위 레인 : 마주친다 ───────────────────────── */
    const sep = lerp(154, 26, meet);
    const aCarp = w / 2 - sep / 2, aCli = w / 2 + sep / 2;

    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.setLineDash([12, 10]);
    ctx.lineDashOffset = (t * 24) % 22;
    ctx.beginPath(); ctx.moveTo(20, A_Y); ctx.lineTo(w - 20, A_Y); ctx.stroke();
    ctx.setLineDash([]); ctx.lineDashOffset = 0;

    const bob = Math.sin(t * 7.4) * 3;
    ctx.fillStyle = tone('ink');
    ctx.beginPath(); ctx.arc(aCarp, A_Y + bob, 8, 0, Math.PI * 2); ctx.fill();
    ctx.beginPath(); ctx.arc(aCli, A_Y - bob, 8, 0, Math.PI * 2); ctx.fill();

    /* 빚 캡슐이 목수 위에서 의뢰인 위로 건너간다 */
    const capX = lerp(aCarp, aCli, pass);
    /* 🔴 **계속 도는 것을 캡슐 바깥으로 뺐다** (2026-08-08 반려 수정 — chase.js 와 같은 결함).
       초안은 캡슐 안쪽 전면에 강조색 빗금을 흘려 같은 강조색 라벨(DEBT)을 훑고 있었다.
       하필 이 캡슐이 **이 편의 페이오프 물체**라 가장 읽혀야 할 글자가 가장 안 읽혔다.
       움직임을 테두리 밖 점선 후광으로 옮긴다 — 글자를 한 번도 안 지나가고,
       건너갈 때 후광이 함께 따라가 이동이 더 잘 보인다.
       🔑 아래 레인의 빚 카드는 그대로 둔다 — 거기 빗금은 글자 없는 바닥 띠(y 254~266)에만
       있고 라벨 글자는 y 237~252 라 애초에 안 겹친다(검수도 「선명함」으로 확인). */
    ctx.globalAlpha = 0.55;
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
    ctx.setLineDash([10, 8]);
    ctx.lineDashOffset = -(t * 26) % 18;
    roundRect(ctx, capX - CAP_W / 2 - 5, CAP_Y - CAP_H / 2 - 3, CAP_W + 10, CAP_H + 6, (CAP_H + 6) / 2);
    ctx.stroke();
    ctx.setLineDash([]); ctx.lineDashOffset = 0;
    ctx.globalAlpha = 1;
    setShadow(ctx, GLOW, 13);
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
    roundRect(ctx, capX - CAP_W / 2, CAP_Y - CAP_H / 2, CAP_W, CAP_H, CAP_H / 2); ctx.stroke();
    clearShadow(ctx);
    if (spec.debtLabel) {
      ctx.textAlign = 'center';
      ctx.font = disp(900, fit(spec.debtLabel, 900, 14, CAP_W - 22, 9));
      ctx.fillStyle = tone('accent');
      ctx.fillText(spec.debtLabel, capX, CAP_Y + 5);
    }

    /* 갚았다 — 체크가 그어지고 PAID 가 켜진다 */
    if (stamp > 0.02) {
      const sc = spring(stamp);
      ctx.globalAlpha = clamp(stamp * 3);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.lineCap = 'round';
      const kx = capX + CAP_W / 2 + 14, ky = CAP_Y;
      const k1 = clamp(stamp * 2), k2 = clamp((stamp - 0.4) / 0.6);
      ctx.beginPath();
      ctx.moveTo(kx - 8, ky);
      ctx.lineTo(kx - 8 + 6 * k1, ky + 6 * k1);
      if (k2 > 0) ctx.lineTo(kx - 2 + 11 * k2, ky + 6 - 13 * k2);
      ctx.stroke();
      ctx.lineCap = 'butt';
      if (spec.paidLabel) {
        ctx.textAlign = 'center';
        ctx.font = disp(900, fit(spec.paidLabel, 900, 13 * sc, 90, 9));
        ctx.fillStyle = tone('accent');
        ctx.fillText(spec.paidLabel, Math.min(w - 34, Math.max(34, capX)), 42);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 가르는 선 ────────────────────────────────── */
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.setLineDash([7, 9]);
    ctx.lineDashOffset = -(t * 12) % 16;
    ctx.beginPath(); ctx.moveTo(24, DIV_Y); ctx.lineTo(w - 24, DIV_Y); ctx.stroke();
    ctx.setLineDash([]); ctx.lineDashOffset = 0;

    /* ── 아래 레인 : 끝내 못 만난다 ───────────────── */
    const bx0 = 44, bx1 = w - 44;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    roundRect(ctx, bx0, BAR_Y, bx1 - bx0, BAR_H, BAR_H / 2); ctx.stroke();
    if (hold > 0.004) {
      ctx.save();
      roundRect(ctx, bx0, BAR_Y, bx1 - bx0, BAR_H, BAR_H / 2); ctx.clip();
      ctx.fillStyle = tone('ink');
      ctx.fillRect(bx0, BAR_Y, (bx1 - bx0) * hold, BAR_H);
      ctx.restore();
    }
    if (spec.timerLabel) {
      ctx.textAlign = 'left';
      ctx.font = disp(900, fit(spec.timerLabel, 900, 11, 150, 8));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.timerLabel, bx0, BAR_Y - 8);
    }

    const bCarp = 62 + 9 * Math.sin(t * 1.05);
    const bCli = w - 62 + 12 * Math.sin(t * 1.7 + 0.8);
    ctx.fillStyle = tone('ink');
    ctx.beginPath(); ctx.arc(bCarp, B_Y, 8, 0, Math.PI * 2); ctx.fill();
    ctx.beginPath(); ctx.arc(bCli, B_Y, 8, 0, Math.PI * 2); ctx.fill();

    /* 안 사라지는 빚 — 테두리가 굵어지고 빗금이 끝까지 찬다 */
    const cx0 = w / 2 - CARD_W / 2;
    hatch(ctx, cx0 + 14, CARD_Y + CARD_H - 20, CARD_W - 28, 12,
      0.3 + 0.7 * hold, t, tone('accent'));
    setShadow(ctx, GLOW, 12 + 8 * hold);
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3 + 2 * hold;
    roundRect(ctx, cx0, CARD_Y, CARD_W, CARD_H, 8); ctx.stroke();
    clearShadow(ctx);
    if (spec.debtLabel) {
      ctx.textAlign = 'left';
      ctx.font = disp(900, fit(spec.debtLabel, 900, 15, CARD_W - 34, 9));
      ctx.fillStyle = tone('accent');
      ctx.fillText(spec.debtLabel, cx0 + 16, CARD_Y + 22);
    }

    /* ── 예고 자막이 여는 변화 — 두 레인을 잇는 밑줄 ─ */
    if (rule > 0.01) {
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 5;
      ctx.beginPath();
      ctx.moveTo(20, RULE_Y); ctx.lineTo(lerp(20, w - 20, rule), RULE_Y);
      ctx.stroke();
    }

    ctx.textAlign = 'left';
  }
};
