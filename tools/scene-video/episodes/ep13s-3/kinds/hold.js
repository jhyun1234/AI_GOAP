import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* hold — 고친 한 줄. 간호는 시계를 되감지 않고 **그 자리에 멎게** 한다.

   원문 근거:
   > "그래서 간호는 시계를 되감지 않고 멈추기만 하도록 했습니다."
   > "곁을 지키는 동안엔 죽음이 다가오지 않지만…"

   🔴 앞 샷(graze)의 **되받기이자 뒤집기**다. 같은 홈·같은 표식·같은 「간호」 칩인데
   채움이 0 으로 접히지 않고 **지나온 만큼 그대로 남는다.** 두 샷을 잇대어 보면
   달라진 것이 딱 하나(되감기 화살표가 없어지고 그 자리에 멈춤 막대가 선다)라
   "한 줄만 고쳤다"가 그림으로 읽힌다.
   🔑 멎는 지점도 graze 의 되감기 지점과 **같은 x**(홈의 62%)로 맞췄다 — 앞 샷에서
   매번 지워지던 바로 그 자리가 이번엔 남는다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **층으로 갈랐다.**
     표식(ink) 86~114 · 홈(track) 132~156 · 채움(ink) 138~150 ·
     멈춤 막대(accent) 162~180 · 「멈춤」(accent) 184~200 · 「하루 반」(ink) 174~189.
     「멈춤」과 「하루 반」은 y 가 겹치지만 x 가 62px 떨어져 있다(188~232 / 294~338).
     「간호」 칩(accent)은 채움의 오른쪽 끝보다 39px 오른쪽에 선다.

   🔴 **소리를 안 붙인 샷이다.** 이 편의 두 소리는 「쓸려 지워진다(S2 sweep)」와
     「끝내 무겁게 닿는다(S4 thud)」이고, 그 사이에서 아무 일도 안 일어나는 것이
     이 샷의 내용이라 침묵이 곧 뜻이다.

   세로 예산(캔버스 307): 최저 = 「멈춤」 글립 바닥 200. 107px 남는다.

   계속 도는 것 = 홈 점선 + **채움 안 빗금이 멈춤 막대에 밀려와 부딪히는 흐름**(면적) +
   막대 글로우의 느린 맥동. 자막이 끝난 뒤에도 셋이 돈다 — 시계는 멎었지만
   시뮬레이션은 안 멎는다. */

const PAD_L = 44, PAD_R = 36;
const GY = 132, GH = 24, FY = 138, FH = 12;
const MARK_CY = 100, MARK_W = 48, MARK_H = 28;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const X0 = PAD_L, X1 = w - PAD_R, F0 = X0 + 5;
    const HOLD = F0 + (X1 - 8 - F0) * 0.62;
    const CHIP_X = HOLD + 66, CHIP_W = 54, CHIP_H = 28;

    const ctext = (txt, x, y, weight, size, color, maxW, alpha) => {
      if (alpha <= 0.01) return;
      let fs = size; ctx.font = disp(weight, fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(weight, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.textAlign = 'center'; ctx.fillStyle = color;
      ctx.fillText(txt, clamp(x, 12 + tw / 2, w - 12 - tw / 2), y);
      ctx.restore();
    };

    const c0 = cue(spec.holdCue ?? 0, 0.15, 0.86);
    const st = ease(clamp(c0 / 0.12));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    const grow = ease(clamp(c0 / 0.40));
    const chipK = ease(clamp((c0 - 0.20) / 0.24));
    const stopK = ease(clamp((c0 - 0.46) / 0.24));
    const hx = lerp(F0, HOLD, grow);

    /* ── 구획 이름 ─────────────────────────────────── */
    if (spec.title) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
      ctx.restore();
    }

    /* ── 홈 ────────────────────────────────────────── */
    ctx.save();
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.setLineDash([13, 10]);
    ctx.lineDashOffset = -((t * 17) % 23);
    roundRect(ctx, X0, GY, X1 - X0, GH, GH / 2); ctx.stroke();
    ctx.restore();

    /* ── 채움 : 지나온 만큼 그대로 남는다 ────────────
       빗금은 계속 오른쪽으로 밀려오지만 막대에 닿는 자리에서 옅어진다 —
       "죽음이 계속 밀어붙이는데 여기서 멎어 있다". */
    if (hx - F0 > 2) {
      const fw = hx - F0;
      ctx.save();
      ctx.beginPath(); roundRect(ctx, F0, FY, fw, FH, FH / 2); ctx.clip();
      ctx.globalAlpha = st * 0.4; ctx.fillStyle = tone('ink');
      ctx.fillRect(F0, FY, fw, FH);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 5;
      const S = 19, off = (t * 30) % S;
      for (let x = F0 - FH - S + off; x < hx + S; x += S) {
        const fade = clamp((hx - x) / 26);           // 막대 앞에서 옅어진다
        ctx.globalAlpha = st * 0.95 * fade;
        ctx.beginPath(); ctx.moveTo(x, FY + FH); ctx.lineTo(x + FH, FY); ctx.stroke();
      }
      ctx.restore();
    }

    /* ── 끝 눈금 : 하루 반. 이번에도 안 닿는다 ──────── */
    ctx.save();
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4; ctx.lineCap = 'round';
    ctx.beginPath(); ctx.moveTo(X1, 124); ctx.lineTo(X1, 170); ctx.stroke();
    ctx.restore();
    if (spec.endLabel) ctext(spec.endLabel, X1, 186, 900, 14, tone('ink'), 96, st);

    /* ── 표식 ──────────────────────────────────────── */
    ctx.save();
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, hx - MARK_W / 2, MARK_CY - MARK_H / 2, MARK_W, MARK_H, 7); ctx.stroke();
    ctx.restore();
    if (spec.face) ctext(spec.face, hx, MARK_CY + 5, 800, 14, tone('ink'), MARK_W - 10, st);

    /* ── 곁에 앉은 간호 ────────────────────────────── */
    if (chipK > 0.01 && spec.careLabel) {
      const cy = lerp(48, MARK_CY, chipK);
      ctx.save();
      ctx.globalAlpha = chipK;
      setShadow(ctx, GLOW, 12);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, CHIP_X - CHIP_W / 2, cy - CHIP_H / 2, CHIP_W, CHIP_H, 7); ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
      ctext(spec.careLabel, CHIP_X, cy + 5, 800, 13, tone('accent'), CHIP_W - 10, chipK);
    }

    /* ── 멈춤 막대 : 되감기 화살표가 있던 자리를 대신한다 ── */
    if (stopK > 0.01) {
      const pulse = 10 + 5 * Math.sin(t * 2.4);
      ctx.save();
      ctx.globalAlpha = stopK;
      setShadow(ctx, GLOW, pulse);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 5; ctx.lineCap = 'round';
      ctx.beginPath();
      ctx.moveTo(hx, 162); ctx.lineTo(hx, lerp(162, 180, stopK));
      ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
      if (spec.holdLabel) ctext(spec.holdLabel, hx, 198, 900, 14, tone('accent'), 110, stopK);
    }

    ctx.textAlign = 'left';
  }
};
