import {
  disp, mono, ease, clamp, lerp, spring,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* daysleft — 나눗셈을 화면에 세운다.

   원문: "FoodDaysLeft라는 파생 슬롯을 하나 만들었습니다. 창고에 있는 조리식과 생식을 각각
   포만감으로 환산해서 다 더하고, 그걸 지금 살아 있는 주민 수와 계절 소모 속도로 나눕니다.
   결과는 정수 하나 — \"지금 이 마을은 며칠 버티나.\"" /
   "그리고 HUD의 달력 옆에 \"식량 N일치\"를 띄웠습니다."

   🔴 이 회차에서 도식이 통째로 나오는 유일한 자리다. 원문이 계산을 문장으로 그대로 쓰기
   때문에 **그 문장의 모양(분자 둘 / 분모 둘 / 결과 하나)을 그대로 세운다** — 도형을 지어내는
   것이 아니라 원문이 이미 그린 것을 옮기는 것이다.
   ① 위에서 COOKED · RAW 두 흰 상자가 내려와 앉는다(창고에서 오는 것)
   ② 강조색 분수선이 왼쪽에서 오른쪽으로 그어진다
   ③ 아래에서 VILLAGERS · SEASON 두 흰 상자가 올라와 붙는다(나누는 것)
   ④ 화살표가 아래를 가리키고 강조색 카드가 튀어 들어와 FoodDaysLeft 와 「식량 N일치」가 켜진다

   🔴 **계산식 안에 숫자를 한 자도 안 적었다.** 원문이 같은 자리에서 "계산식 안에 숫자를
   복사해 넣지 않았습니다"라고 쓴다 — 화면이 그 원칙을 그대로 지킨다. 포만감 값·소모 배율은
   원문에 값이 없어 그렸으면 전부 지어낸 수치가 된다.
   🔴 결과 칩은 원문이 HUD 문구로 적은 **「식량 N일치」를 N 그대로** 올린다. 「3일치」는
   명세서에 적어 둔 바람이지 관측값이 아니라 안 쓴다.

   🔴 흰 상자(34~72 · 108~146)와 강조색(분수선 96 · 화살표 156~178 · 카드 186~262)이
      번갈아 놓이되 가장 가까운 쌍도 12px 떨어져 있다. 카드는 **테두리만 그리고 속은 검정**이라
      같은 강조색 글자를 훑는 것이 없다.

   계속 도는 것 = 분수선 위를 도는 표식 · 카드 바깥 점선 후광 · 상자 넷의 미세 표류. */

const BOX_H = 38, BOX_W = 146;
const NUM_Y = 34, LINE_Y = 96, DEN_Y = 108;
const LX = 20, RX = 186;
const ARR_T = 156, ARR_B = 178;
const CARD_X = 46, CARD_Y = 186, CARD_W = 260, CARD_H = 76;
const BASE_Y = 288;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const cx = w / 2;

    const fitFont = (txt, weight, start, max, min = 8, fam = disp) => {
      let fs = start; ctx.font = fam(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = fam(weight, fs); }
      return fs;
    };

    const c0 = cue(spec.buildCue ?? 0, 0.15, 0.70);
    const num  = ease(clamp(c0 / 0.26));
    const line = ease(clamp((c0 - 0.22) / 0.20));
    const den  = ease(clamp((c0 - 0.40) / 0.20));
    const arr  = ease(clamp((c0 - 0.52) / 0.14));
    const res  = ease(clamp((c0 - 0.58) / 0.34));

    /* ── 바닥 점선 ─────────────────────────────────── */
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.setLineDash([7, 9]);
    ctx.lineDashOffset = -(t * 15) % 16;
    ctx.beginPath(); ctx.moveTo(16, BASE_Y); ctx.lineTo(w - 16, BASE_Y); ctx.stroke();
    ctx.setLineDash([]); ctx.lineDashOffset = 0;

    if (spec.storeLabel) {
      ctx.textAlign = 'left';
      ctx.font = disp(800, fitFont(spec.storeLabel, 800, 12, 160));
      ctx.globalAlpha = clamp(num * 1.6);
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.storeLabel, LX, 22);
      ctx.globalAlpha = 1;
    }

    const box = (label, x, y, alpha) => {
      if (alpha <= 0.01) return;
      ctx.globalAlpha = clamp(alpha);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, x, y, BOX_W, BOX_H, 7); ctx.stroke();
      ctx.textAlign = 'center';
      ctx.font = mono(700, fitFont(label, 700, 14, BOX_W - 22, 8, mono));
      ctx.fillStyle = tone('ink');
      ctx.fillText(label, x + BOX_W / 2, y + BOX_H / 2 + 5);
      ctx.globalAlpha = 1;
    };

    /* ── 분자 : 창고에서 내려온다 ──────────────────── */
    const nyA = lerp(NUM_Y - 46, NUM_Y, num) + Math.sin(t * 1.7) * 1.4;
    const nyB = lerp(NUM_Y - 46, NUM_Y, num) + Math.sin(t * 1.7 + 1.9) * 1.4;
    box(spec.numA ?? '', LX, nyA, num * 1.5);
    box(spec.numB ?? '', RX, nyB, num * 1.5);

    /* 두 상자 사이의 + */
    if (num > 0.4) {
      ctx.globalAlpha = clamp((num - 0.4) / 0.4);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      const px = (LX + BOX_W + RX) / 2, py = NUM_Y + BOX_H / 2;
      ctx.beginPath();
      ctx.moveTo(px - 7, py); ctx.lineTo(px + 7, py);
      ctx.moveTo(px, py - 7); ctx.lineTo(px, py + 7);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 분수선 ────────────────────────────────────── */
    if (line > 0.005) {
      setShadow(ctx, GLOW, 12);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 5;
      ctx.lineCap = 'round';
      ctx.beginPath();
      ctx.moveTo(LX, LINE_Y); ctx.lineTo(lerp(LX, RX + BOX_W, line), LINE_Y);
      ctx.stroke();
      ctx.lineCap = 'butt';
      clearShadow(ctx);
    }
    /* 분수선 위를 도는 표식 — 자막과 무관하게 계속 움직인다 */
    if (line > 0.99) {
      const span = RX + BOX_W - LX;
      const dx = LX + ((t * 74) % span);
      ctx.fillStyle = tone('accent');
      ctx.beginPath(); ctx.arc(dx, LINE_Y, 6, 0, Math.PI * 2); ctx.fill();
    }

    /* ── 분모 : 아래에서 올라온다 ──────────────────── */
    const dyA = lerp(DEN_Y + 46, DEN_Y, den) + Math.sin(t * 1.5 + 0.6) * 1.4;
    const dyB = lerp(DEN_Y + 46, DEN_Y, den) + Math.sin(t * 1.5 + 2.4) * 1.4;
    box(spec.denA ?? '', LX, dyA, den * 1.5);
    box(spec.denB ?? '', RX, dyB, den * 1.5);

    /* ── 화살표 ────────────────────────────────────── */
    if (arr > 0.02) {
      ctx.globalAlpha = clamp(arr * 1.6);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      const ay = lerp(ARR_T, ARR_B, arr);
      ctx.beginPath(); ctx.moveTo(cx, ARR_T); ctx.lineTo(cx, ay); ctx.stroke();
      ctx.fillStyle = tone('accent');
      ctx.beginPath();
      ctx.moveTo(cx, ay + 8); ctx.lineTo(cx - 8, ay - 3); ctx.lineTo(cx + 8, ay - 3);
      ctx.closePath(); ctx.fill();
      ctx.globalAlpha = 1;
    }

    /* ── 결과 : 정수 하나 ──────────────────────────── */
    if (res > 0.01) {
      const sc = spring(res);
      ctx.save();
      ctx.translate(cx, CARD_Y + CARD_H / 2);
      ctx.scale(sc, sc);
      ctx.translate(-cx, -(CARD_Y + CARD_H / 2));
      ctx.globalAlpha = clamp(res * 1.6);

      /* 바깥 점선 후광 — 테두리 밖이라 글자를 한 번도 안 지나간다 */
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.setLineDash([10, 9]);
      ctx.lineDashOffset = -(t * 26) % 19;
      const gAlpha = ctx.globalAlpha;
      ctx.globalAlpha = gAlpha * 0.5;
      roundRect(ctx, CARD_X - 8, CARD_Y - 8, CARD_W + 16, CARD_H + 16, 14); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = gAlpha;

      setShadow(ctx, GLOW, 16);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, CARD_X, CARD_Y, CARD_W, CARD_H, 10); ctx.stroke();
      clearShadow(ctx);

      ctx.textAlign = 'center';
      if (spec.slotLabel) {
        ctx.font = mono(700, fitFont(spec.slotLabel, 700, 13, CARD_W - 30, 8, mono));
        ctx.fillStyle = tone('accent');
        ctx.fillText(spec.slotLabel, cx, CARD_Y + 26);
      }
      if (spec.hudChip) {
        ctx.font = disp(900, fitFont(spec.hudChip, 900, 27, CARD_W - 34));
        ctx.fillStyle = tone('accent');
        ctx.fillText(spec.hudChip, cx, CARD_Y + 60);
      }
      ctx.globalAlpha = 1;
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
