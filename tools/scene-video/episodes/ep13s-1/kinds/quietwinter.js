import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* quietwinter — S1. 겨울이 그냥 지나가고, 늑대가 지나가도 부상자 칸이 0에서 안 움직인다.

   원문: "주민들은 겨울 내내 부지런히 밭을 갈고… 겨울이 끝났고, 아무도 마을을 떠나지
   않았습니다. 한 명도요." / "실제로 오래 방치해 보니 부상자가 0명이었습니다. 위협은
   예고대로 오고 밭은 부서지는데, 사람은 아무도 다치지 않았습니다." /
   "늑대는 맵 가장자리에서 걸어 들어와 목표 지점에 도착한 순간 피해를 입힌 뒤 다시 걸어
   나가 사라집니다."

   🔴 이 샷이 회차의 어휘를 세운다 — 왼쪽 위 범례가 **강조색 쐐기 = 늑대**를 못 박고,
   그 뒤 S2 · S3 는 같은 쐐기를 라벨 없이 쓴다. 흰 것은 전부 마을의 것(사람 · 밭 · 계기).

   ⏱ cue 0(겨울 자막) = 시간 띠가 왼쪽에서 오른쪽으로 차고 끝에서 「겨울 끝」이 켜진다.
      🔴 초고의 끝 라벨은 「봄」이었는데 **원문에 없는 낱말**이라 1차 검수에서 반려됐다
      (원본·브리프 양쪽 0회). 원문이 실제로 준 말은 「겨울이 끝났고」까지다.
      cue 1(늑대 자막) = 쐐기가 왼쪽 밖에서 들어와 가운데 밭을 부수고 오른쪽으로 나간다.
      「부상자 0」 계기는 켜진 뒤 **끝까지 0 그대로**다 — 안 변하는 것이 이 샷의 사건이다.
      `t` 로만 도는 것 = 사람 표식의 바운스 · 띠 점선 흐름 · 시간 띠 안 홈의 흐름.

   🔴 겹침 감사 (두 축)
   · 강조색은 쐐기 하나뿐이다 — y 171~189, 후광 14 를 더해 **y 157~203**.
     위로 사람 표식 바닥(바운스 최대 142)까지 **15px**, 아래로 밭 칸 위(212)까지 **9px**.
     좌우로는 캔버스 밖에서 들어오되 알파가 x 14 안쪽에서 0 이라 가장자리에 잉크가 안 남는다.
   · 범례 쐐기는 **후광을 안 준다**(위 점선 80 과 8px 뿐이라 후광을 주면 닿는다).
     x 14~30 이고 첫 표식(x 56~76)과 26px 떨어져 있다.
   · 흰↔흰: 「겨울」·「겨울 끝」 글리프 바닥 37 ↔ 시간 띠 위 44.5 = 7.5px(둘은 **띠를 가리키는 한 쌍**) ·
     띠 바닥 65.5 ↔ 위 점선 80 = 14.5 · 범례 글자 바닥 104 ↔ 표식 위 114 = 10 ·
     표식 바닥 142 ↔ 밭 위 212 = 70 · 밭 바닥 236 ↔ 아래 점선 250 = 14 ·
     점선 250 ↔ 계기 상자 위 256.5 = 6.5 · 계기 안 「부상자」와 「0」은 x 로 20px 갈라 놨다.
   · 「겨울」(좌 x 16~41)과 「겨울 끝」(우 x 286~336)은 같은 행이지만 x 로 245px 떨어져 있다.
     끝 라벨이 2자 → 4자로 길어졌어도 **우측정렬 + `fitFont(…, 12.5, 110)`** 이라 왼쪽으로만
     자라므로 새 겹침이 안 생긴다. */

const BAR_Y = 46, BAR_H = 18;
const TOP_RULE = 80, BOT_RULE = 250;
const LEG_Y = 100;
const MARK_Y = 128, MARK_R = 10;
const MARK_X = [66, 122, 178, 234, 290];
const WOLF_Y = 180;
const FIELD_Y = 212, FIELD_S = 24;
const FIELD_CX = [96, 176, 256];
const CHIP_Y = 258, CHIP_H = 34;

/* 🔴 가로 점선은 `setLineDash` 를 안 쓴다 — 조각을 직접 채운다(결정성).
   사유는 `firstgrave.js` 의 같은 함수 주석에 적어 뒀다. 모양은 이전과 같다. */
function dashRule(ctx, y, x0, x1, t, speed) {
  const P = 14, DASH = 7;
  const off = (((t * speed) % P) + P) % P;
  ctx.save();
  ctx.fillStyle = tone('track');
  for (let x = x0 - P + off; x < x1; x += P) {
    const a = Math.max(x0, x), b = Math.min(x1, x + DASH);
    if (b > a) ctx.fillRect(a, y - 1.5, b - a, 3);
  }
  ctx.restore();
}

/* 늑대 = 오른쪽을 향한 쐐기 + 뒤쪽 귀 둘. 세 샷이 같은 모양을 쓴다. */
function wolfWedge(ctx, x, y, s = 1) {
  ctx.beginPath();
  ctx.moveTo(x + 13 * s, y);
  ctx.lineTo(x - 9 * s, y - 9 * s);
  ctx.lineTo(x - 3 * s, y);
  ctx.lineTo(x - 9 * s, y + 9 * s);
  ctx.closePath();
  ctx.fill();
  ctx.beginPath();
  ctx.moveTo(x - 9 * s, y - 9 * s);
  ctx.lineTo(x - 15 * s, y - 14 * s);
  ctx.lineTo(x - 12 * s, y - 5 * s);
  ctx.closePath();
  ctx.fill();
  ctx.beginPath();
  ctx.moveTo(x - 9 * s, y + 9 * s);
  ctx.lineTo(x - 15 * s, y + 14 * s);
  ctx.lineTo(x - 12 * s, y + 5 * s);
  ctx.closePath();
  ctx.fill();
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const fitFont = (txt, weight, start, max, min = 8) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const c0 = cue(spec.winterCue ?? 0, 0.15, 0.80);
    const c1 = cue(spec.wolfCue ?? 1, 0.15, 0.85);
    const fillK = ease(clamp(c0 / 0.74));
    const walk = ease(clamp(c1 / 0.88));

    /* ── 시간 띠 : 겨울이 그냥 지나간다 ───────────── */
    if (spec.seasonFrom) {
      ctx.textAlign = 'left';
      ctx.font = disp(800, fitFont(spec.seasonFrom, 800, 12.5, 110));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.seasonFrom, 16, 34);
    }
    if (spec.seasonTo && fillK > 0.86) {
      ctx.save();
      ctx.globalAlpha = clamp((fillK - 0.86) / 0.14);
      ctx.textAlign = 'right';
      ctx.font = disp(800, fitFont(spec.seasonTo, 800, 12.5, 110));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.seasonTo, w - 16, 34);
      ctx.restore();
    }
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    roundRect(ctx, 16, BAR_Y, w - 32, BAR_H, 5); ctx.stroke();

    const fw = (w - 40) * fillK;
    if (fw > 2) {
      ctx.save();
      roundRect(ctx, 20, BAR_Y + 6, fw, BAR_H - 12, 3); ctx.clip();
      ctx.fillStyle = tone('ink');
      ctx.fillRect(20, BAR_Y + 6, fw, BAR_H - 12);
      /* 홈이 흐른다 — 글자 없는 도형 위라 클립해도 안전하다 */
      ctx.fillStyle = tone('bg');
      const off = (t * 15) % 16;
      for (let d = -16; d < fw + 16; d += 16) ctx.fillRect(20 + d + off, BAR_Y + 6, 3, BAR_H - 12);
      ctx.restore();
    }

    /* ── 마을 띠 ─────────────────────────────────── */
    dashRule(ctx, TOP_RULE, 16, w - 16, t, 12);
    dashRule(ctx, BOT_RULE, 16, w - 16, t, -10);

    /* ── 범례 : 이 쐐기가 늑대다 (cue 1 에서 켜진다) ─ */
    const leg = ease(clamp(c1 / 0.18));
    if (leg > 0.02) {
      ctx.save();
      ctx.globalAlpha = clamp(leg * 1.6);
      ctx.fillStyle = tone('accent');
      wolfWedge(ctx, 24, LEG_Y - 4, 0.62);          // 후광 없음 — 위 점선과 8px 뿐이다
      if (spec.wolfLabel) {
        ctx.textAlign = 'left';
        ctx.font = disp(800, fitFont(spec.wolfLabel, 800, 12.5, 120));
        ctx.fillStyle = tone('sub');
        ctx.fillText(spec.wolfLabel, 42, LEG_Y);
      }
      ctx.restore();
    }

    /* ── 사람들 ──────────────────────────────────── */
    ctx.fillStyle = tone('ink');
    for (let i = 0; i < MARK_X.length; i++) {
      const my = MARK_Y + Math.sin(t * 4.6 + i * 1.4) * 4;
      ctx.beginPath(); ctx.arc(MARK_X[i], my, MARK_R, 0, Math.PI * 2); ctx.fill();
    }

    /* ── 밭 : 가운데 하나가 부서진다 ─────────────── */
    const px = lerp(-18, w + 18, walk);
    const broke = ease(clamp((px - FIELD_CX[1]) / 30));
    for (let i = 0; i < FIELD_CX.length; i++) {
      const gone = i === 1 ? broke : 0;
      ctx.save();
      ctx.globalAlpha = 1 - 0.45 * gone;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      /* 위상을 양수로 정규화한다 — 값은 mod 10 으로 같으므로 그림은 안 바뀐다 */
      if (gone > 0.3) { ctx.setLineDash([5, 5]); ctx.lineDashOffset = (((-(t * 12)) % 10) + 10) % 10; }
      roundRect(ctx, FIELD_CX[i] - FIELD_S / 2, FIELD_Y, FIELD_S, FIELD_S, 4);
      ctx.stroke();
      ctx.restore();
    }

    /* ── 늑대 : 들어와 · 때리고 · 나간다 ──────────── */
    const wa = clamp(Math.min(px - 14, (w - 14) - px) / 26);
    if (wa > 0.02) {
      ctx.save();
      ctx.globalAlpha = wa;
      setShadow(ctx, GLOW, 14);
      ctx.fillStyle = tone('accent');
      wolfWedge(ctx, px, WOLF_Y);
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 계기 : 부상자 0. 켜진 뒤 끝까지 안 변한다 ── */
    const chip = ease(clamp((c1 - 0.30) / 0.22));
    if (chip > 0.02 && spec.injLabel) {
      const cx = w / 2;
      ctx.save();
      ctx.globalAlpha = clamp(chip * 1.5);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, cx - 58, CHIP_Y, 116, CHIP_H, 8); ctx.stroke();
      ctx.fillStyle = tone('ink');
      ctx.textAlign = 'right';
      ctx.font = disp(800, fitFont(spec.injLabel, 800, 15, 62));
      ctx.fillText(spec.injLabel, cx - 10, CHIP_Y + 23);
      ctx.textAlign = 'left';
      ctx.font = disp(900, 26);
      ctx.fillText(String(spec.injNumber ?? '0'), cx + 10, CHIP_Y + 25);
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
