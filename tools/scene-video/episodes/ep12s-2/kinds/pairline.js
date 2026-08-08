import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, depthGrad, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* pairline — 두 값이 같은 높이에 서야 하는 선. 하나만 올렸더니 어긋나고, 짝을 올리자 맞물린다.

   원문: "레시피가 비싸졌는데 요리 목표의 발동 조건은 예전 재료 개수 그대로였던 겁니다." /
   "그래서 발동 조건을 위기 레시피에 맞춰 올려 정합을 맞췄습니다. 밸런스 숫자 하나를 만졌더니
   그것과 짝을 이루던 다른 숫자가 어긋나는, 아주 전형적인 파급이었습니다."

   ① gapCue — 두 마름모가 같은 높이에서 시작한다. 왼쪽(RECIPE COST)만 위로 올라가고
      오른쪽(GOAL TRIGGER)은 그대로다. 벌어진 사이를 강조색 이중 화살표가 「어긋남」으로 잰다.
   ② matchCue — **페이오프.** 오른쪽이 같은 높이까지 올라오고, 둘 사이를 강조색 가로선이
      왼쪽에서 오른쪽으로 이어 잠근다. 흰 안내 점선은 그 전에 완전히 꺼진다(같은 y 에
      흰 점선과 강조색 선이 동시에 있지 않게 — 겹침 사고를 좌표가 아니라 시간으로도 막는다).
   ③ bandCue — **페이오프 뒤를 이어받는 변화.** 아래로 강조색 자물쇠 띠가 켜지고 바닥
      빗금이 왼쪽부터 끝까지 채워지며 오른쪽 화살촉이 선다. 이 잠금이 다음 편의 원인이므로
      본문의 마지막 그림이 다음 편을 가리키고 끝난다.
      🔴 **2026-08-08 4단 구조 개정 — 예고 자막이 이 샷을 떠났다.** 예전에는 세 번째 줄
      (「다음 편, 그 잠금이 홍수를 막아요.」)의 `cue(2)` 에 물려 있었는데, 그 줄이 아웃트로
      샷(SO)으로 옮겨 가면서 **없는 줄을 가리키게 됐다.** 그래서 같은 줄(matchCue)의
      **뒤쪽 구간**으로 옮겼다 — 맞물림은 c1 = 0.54 에 끝나므로 그 뒤 0.60~1.0 이 통째로
      비어 있었고, 그 빈자리가 곧 정적 구간이다. 🔑 **줄을 옮길 때 cue 인덱스가 조용히
      접힌다** — 이 회차가 실제로 밟은 자리다.

   🔴 흰 것과 강조색을 같은 픽셀에 겹치지 않는다. 잠금 가로선(강조색)은 **두 마름모 사이에서만**
   긋고(중심 ±14, 마름모 반지름 11 → 3px 여유), 어긋남 화살표는 오른쪽 마름모 오른쪽 바깥
   19px 에 세운다. 마름모는 처음부터 끝까지 흰색이다 — 맞물렸다고 색을 바꾸지 않는다.
   ⚠️ **좌표만 안 겹치면 되는 게 아니다.** `setShadow(GLOW, blur)` 는 도형 밖으로 blur 만큼
   번지므로 **글로우 반경까지 계산해서 띄워야 한다** — 초안이 라벨 252 · 띠 256 으로 4px 만
   띄웠다가 카드 직전 프레임에서 혼합 픽셀 198개가 나왔다. 지금은 22px 이다(위 LABEL_Y 주석).

   계속 도는 것 = 위쪽 자 눈금의 흐름 · 두 기둥 점선의 흐름 · 아래 띠 바닥 빗금의 흐름. */

const AXT = 78, AXB = 220;
const Y_LOW = 196, Y_HIGH = 106;
const R = 11;
/* 🔴 라벨 ↔ 예고 띠 = **22px**(2026-08-08 수정, 초안은 252/256 으로 4px 이었다).
   띠 테두리에 `setShadow(GLOW, 14)` 가 걸려 있어 글로우가 위로 ~14px 번지고, 그 자리에
   흰 라벨이 있으면 **혼합 픽셀이 생긴다**(실측 198개/51종). 같은 회차 `knobs` 는 라벨 228 ·
   상자 250 으로 22px 이 떠 있고 혼합 0개였다 — 그 실측치를 그대로 가져왔다.
   축 아래끝(AXB)도 함께 올렸다. 표식 최하단이 Y_LOW + R = 207 이라 220 이면 13px 남는다. */
const LABEL_Y = 234;
const BAND_T = 256, BAND_B = 296;

function diamond(ctx, cx, cy) {
  ctx.beginPath();
  ctx.moveTo(cx, cy - R); ctx.lineTo(cx + R, cy);
  ctx.lineTo(cx, cy + R); ctx.lineTo(cx - R, cy);
  ctx.closePath();
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

    const LX = w * 0.30, RX = w * 0.70;
    const c0 = cue(spec.gapCue ?? 0, 0.15, 0.75);
    const c1 = cue(spec.matchCue ?? 1, 0.15, 0.7);
    /* 🔴 예고 줄이 아웃트로 샷으로 나가면서 이 샷의 자막이 2줄이 됐다. 옛 `nextCue: 2` 는
       **없는 줄**을 가리킨다 — 기본값도 1 로 내렸다. 띠는 같은 줄의 뒤쪽 구간을 쓴다. */
    const c2 = cue(spec.bandCue ?? 1, 0.15, 0.7);

    const appear = ease(clamp(c0 / 0.18));
    if (appear <= 0.004) { ctx.textAlign = 'left'; return; }

    const leftY = lerp(Y_LOW, Y_HIGH, ease(clamp((c0 - 0.22) / 0.34)));
    const rightY = lerp(Y_LOW, Y_HIGH, ease(clamp((c1 - 0.06) / 0.30)));
    const gapShow = ease(clamp((c0 - 0.62) / 0.24)) * (1 - ease(clamp(c1 / 0.34)));
    const guideA = ease(clamp((c0 - 0.50) / 0.24)) * (1 - ease(clamp(c1 / 0.30)));
    const lockA = ease(clamp((c1 - 0.34) / 0.20));
    /* 🔴 띠의 세 문턱을 **페이오프 뒤로** 다시 잡았다(2026-08-08 4단 개정).
       옛 값(0~0.30 · 0.14~0.42 · 0.12~0.28)은 **예고 줄의 c2** 를 전제로 아웃트로 카드가
       덮는 시각(c2 = 0.4307) 안쪽에 욱여넣은 것이었다. 이제 이 샷은 마지막 샷이 아니라
       카드가 안 덮고, 대신 **맞물림(lock, c1 = 0.34~0.54)과 겹치면 안 된다** — 페이오프와
       예고가 동시에 움직이면 어느 쪽도 안 읽힌다.
       그래서 띠는 0.58 에 켜지고(맞물림 완성 +0.04) 빗금이 0.98 까지 계속 자란다.
       🔑 **그 뒷구간이 곧 정적 구간이었다.** c1 은 창 W = 0.15 + rel × 0.7 로 자막 끝보다
       일찍 1 에 닿아, 맞물림만 있으면 샷 뒤쪽 1초가 통째로 멈춘다. 빗금이 그 자리를 메운다. */
    const bandA = ease(clamp((c2 - 0.58) / 0.16));
    const hatchK = ease(clamp((c2 - 0.64) / 0.34));
    const arrowA = ease(clamp((c2 - 0.70) / 0.14));
    const bob = Math.sin(t * 2.6) * 1.5;

    /* ── 위쪽 자 — 자막과 무관하게 흐른다 ─────────── */
    ctx.globalAlpha = appear * 0.9;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    const step = 26, sl = (t * 25) % step;
    for (let i = -1; i * step + 16 - sl < w - 12; i++) {
      const x = 16 + i * step - sl;
      if (x < 14 || x > w - 14) continue;
      ctx.beginPath(); ctx.moveTo(x, 54); ctx.lineTo(x, 62); ctx.stroke();
    }
    ctx.beginPath(); ctx.moveTo(14, 62); ctx.lineTo(w - 14, 62); ctx.stroke();
    ctx.globalAlpha = 1;

    /* ── 두 기둥 ─────────────────────────────── */
    ctx.globalAlpha = appear;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.setLineDash([9, 8]);
    ctx.lineDashOffset = (t * 20) % 17;
    for (const x of [LX, RX]) {
      ctx.beginPath(); ctx.moveTo(x, AXT); ctx.lineTo(x, AXB); ctx.stroke();
    }
    ctx.setLineDash([]); ctx.lineDashOffset = 0;
    ctx.globalAlpha = 1;

    /* ── 안내 점선 — 잠금선이 뜨기 전에 완전히 꺼진다 ── */
    if (guideA > 0.02) {
      ctx.globalAlpha = guideA * 0.6;
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.setLineDash([8, 9]);
      ctx.lineDashOffset = -(t * 22) % 17;
      ctx.beginPath();
      ctx.moveTo(LX + R + 5, leftY); ctx.lineTo(RX - R - 5, leftY);
      ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;
    }

    /* ── 잠금 가로선 — 두 마름모 사이에서만 ────────── */
    if (lockA > 0.02) {
      const a = LX + R + 3, b = RX - R - 3;
      ctx.globalAlpha = appear;
      setShadow(ctx, GLOW, 14);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.moveTo(a, Y_HIGH); ctx.lineTo(lerp(a, b, lockA), Y_HIGH);
      ctx.stroke();
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    /* ── 어긋남 — 오른쪽 마름모 바깥에 세운다 ───────── */
    if (gapShow > 0.02 && Math.abs(rightY - leftY) > 6) {
      const gx = RX + R + 19;
      ctx.globalAlpha = gapShow;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(gx, leftY); ctx.lineTo(gx, rightY); ctx.stroke();
      for (const [y, s] of [[leftY, 1], [rightY, -1]]) {
        ctx.beginPath();
        ctx.moveTo(gx - 6, y + s * 9); ctx.lineTo(gx, y); ctx.lineTo(gx + 6, y + s * 9);
        ctx.stroke();
      }
      if (spec.gapLabel) {
        ctx.textAlign = 'left';
        ctx.font = disp(900, fit(spec.gapLabel, 900, 12, w - 16 - (gx + 10), 8));
        ctx.fillStyle = tone('accent');
        ctx.fillText(spec.gapLabel, gx + 10, (leftY + rightY) / 2 + 5);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 두 마름모 ────────────────────────────── */
    ctx.globalAlpha = appear;
    setShadow(ctx, 'rgba(255,255,255,0.18)', 12);
    for (const [x, y] of [[LX, leftY + bob], [RX, rightY - bob]]) {
      ctx.fillStyle = depthGrad(ctx, x - R, y - R, x + R, y + R, 'ink');
      diamond(ctx, x, y); ctx.fill();
    }
    clearShadow(ctx);
    ctx.textAlign = 'center';
    ctx.fillStyle = tone('sub');
    if (spec.leftLabel) {
      ctx.font = disp(900, fit(spec.leftLabel, 900, 11, 118, 8));
      ctx.fillText(spec.leftLabel, LX, LABEL_Y);
    }
    if (spec.rightLabel) {
      ctx.font = disp(900, fit(spec.rightLabel, 900, 11, 118, 8));
      ctx.fillText(spec.rightLabel, RX, LABEL_Y);
    }
    ctx.globalAlpha = 1;

    /* ── 예고 — 다음 편의 원인이 아래에서 켜진다 ────── */
    if (bandA > 0.02 && spec.bandLabel) {
      const bx = 18, bw = w - 36, bh = BAND_B - BAND_T;
      ctx.globalAlpha = bandA;
      setShadow(ctx, GLOW, 14);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, bx, BAND_T, bw, bh, 10); ctx.stroke();
      clearShadow(ctx);

      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(44, 268, 6, Math.PI, 0); ctx.stroke();
      roundRect(ctx, 36, 268, 16, 14, 3); ctx.stroke();

      ctx.textAlign = 'left';
      ctx.font = disp(900, fit(spec.bandLabel, 900, 14, 220, 9));
      ctx.fillStyle = tone('accent');
      ctx.fillText(spec.bandLabel, 66, 279);

      if (arrowA > 0.02) {
        ctx.globalAlpha = bandA * arrowA;
        ctx.beginPath();
        ctx.moveTo(296, 268); ctx.lineTo(314, 275); ctx.lineTo(296, 282);
        ctx.closePath();
        ctx.fillStyle = tone('accent'); ctx.fill();
        ctx.globalAlpha = bandA;
      }

      /* 바닥 띠 — 글자가 없는 곳에서만, 왼쪽부터 채워진다 */
      const sx = bx + 8, sw = (bw - 16) * hatchK;
      if (sw > 2) {
        ctx.save();
        roundRect(ctx, sx, 286, sw, 7, 3); ctx.clip();
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        const hs = 14, off = (t * 28) % hs;
        for (let x = bx - 12; x < bx + bw + 12; x += hs) {
          ctx.beginPath();
          ctx.moveTo(x + off, 294); ctx.lineTo(x + off + 8, 285);
          ctx.stroke();
        }
        ctx.restore();
      }
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
