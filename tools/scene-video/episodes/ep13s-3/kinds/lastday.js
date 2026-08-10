import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* lastday — 남는 한 줄. 간호하던 사람이 자리를 뜨자 시계가 멈춘 지점에서 다시 흐른다.

   원문 근거:
   > "간호하던 사람이 배가 고파 밥 먹으러 가는 순간 시계는 멈춘 지점에서 다시 흐릅니다."
   > "이 한 줄 덕분에 '간호하다 말고 자리를 뜬 사이에 사람이 죽는' 장면이
   >  게임 안에서 실제로 가능해졌습니다."
   > "죽은 자리에는 무덤이 하나 남습니다."
   > (전멸 화면 목업) "마을의 마지막 날 … 아무도 남지 않았다."

   🔴 앞 샷(hold)의 되받기이자 마지막 뒤집기다. **「멈춤」이 꺼지고 그 자리에서**
   머리가 다시 나아가 끝 눈금에 처음으로 닿는다 — 훅에서 한 번도 못 닿던 그 눈금이다.
   회차의 그림 문장이 여기서 닫힌다: 되감기고(S2) → 붙들리고(S3) → 끝내 닿는다(S4).

   🔴 **화면에 수를 한 개도 안 올렸다.** 전멸 화면 목업에 있는 `Day {일수}`·
   `사망 {N} · 이탈 {N} · 정착 {N}` 은 전부 플레이스홀더라 구체적인 수로 채우면
   지어낸 수치가 된다. 그래서 카드는 목업에서 **수가 없는 두 줄만** 가져왔다 —
   제목(마을의 마지막 날)과 마지막 줄(아무도 남지 않았다).

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **층과 시간으로 갈랐다.**
     무덤(accent) 74~114 는 표식(ink) 86~114 와 같은 자리에 서지만 **표식이 완전히
     꺼진 뒤(c0 0.72)에야** 오르기 시작한다. 끝 눈금(ink) 122~172 와는 8px 뜬다.
     카드 테두리(accent) 194.5~285.5 는 「하루 반」(ink, 174~189) 아래로 5.5px 뜨고,
     카드 안 「아무도 남지 않았다」(sub)는 테두리에서 30px 이상 안쪽에 있다.

   🔴 카드는 **아래에서 8px 만 밀려 올라온다.** 크게 슬라이드시키면 등장 첫 프레임의
     바닥이 캔버스(307) 밖으로 나가 「아래 가장자리 잘림」에 걸린다 —
     가장 밖으로 나가는 순간의 바닥이 **293.5** 라 13.5px 남는다.

   ⏱ 페이오프(끝 눈금 도달 → 무덤)는 **자막 0** 위에서 끝난다. 카드는 자막 1 이 진다.
   이 샷은 마지막 샷이 아니므로 아웃트로 카드가 덮지 않는다.

   계속 도는 것 = 홈 점선 + 채움 빗금(끝까지 찬 뒤에도 계속 흐른다 — 원문의
   "전멸해도 시뮬레이션은 멈추지 않습니다"가 이 흐름이다) + 무덤 글로우 맥동. */

const PAD_L = 44, PAD_R = 36;
const GY = 132, GH = 24, FY = 138, FH = 12;
const MARK_CY = 100, MARK_W = 48, MARK_H = 28;
const STONE_W = 24, STONE_BASE = 114, STONE_H = 40;
const CARD_Y = 196, CARD_H = 88, CARD_SLIDE = 8;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const X0 = PAD_L, X1 = w - PAD_R, F0 = X0 + 5;
    const HOLD = F0 + (X1 - 8 - F0) * 0.62;
    const END = X1 - 4;
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

    const c0 = cue(spec.deathCue ?? 0, 0.15, 0.86);
    const c1 = cue(spec.cardCue ?? 1, 0.10, 0.35);
    const cardK = ease(c1);
    const top = 1 - 0.55 * cardK;          // 카드가 오르면 위쪽이 물러난다

    const leave = ease(clamp((c0 - 0.04) / 0.22));    // 간호가 자리를 뜬다
    const runK = ease(clamp((c0 - 0.24) / 0.38));     // 멈춘 지점에서 다시 흐른다
    const gone = ease(clamp((c0 - 0.62) / 0.10));     // 표식이 꺼진다
    const rise = ease(clamp((c0 - 0.72) / 0.18));     // 무덤이 선다
    const hx = lerp(HOLD, END, runK);

    /* ── 구획 이름 ─────────────────────────────────── */
    if (spec.title) {
      ctx.save();
      ctx.globalAlpha = top * 0.9; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
      ctx.restore();
    }

    /* ── 홈 ────────────────────────────────────────── */
    ctx.save();
    ctx.globalAlpha = top;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.setLineDash([13, 10]);
    ctx.lineDashOffset = -((t * 17) % 23);
    roundRect(ctx, X0, GY, X1 - X0, GH, GH / 2); ctx.stroke();
    ctx.restore();

    /* ── 채움 : 멈춘 지점에서 다시 흐른다 ───────────── */
    ctx.save();
    ctx.beginPath(); roundRect(ctx, F0, FY, hx - F0, FH, FH / 2); ctx.clip();
    ctx.globalAlpha = top * 0.4; ctx.fillStyle = tone('ink');
    ctx.fillRect(F0, FY, hx - F0, FH);
    ctx.globalAlpha = top * 0.95;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 5;
    const S = 19, off = (t * 30) % S;
    for (let x = F0 - FH - S + off; x < hx + S; x += S) {
      ctx.beginPath(); ctx.moveTo(x, FY + FH); ctx.lineTo(x + FH, FY); ctx.stroke();
    }
    ctx.restore();

    /* ── 끝 눈금 : 이번엔 닿는다 ───────────────────── */
    ctx.save();
    ctx.globalAlpha = top;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4; ctx.lineCap = 'round';
    ctx.beginPath(); ctx.moveTo(X1, 124); ctx.lineTo(X1, 170); ctx.stroke();
    ctx.restore();
    if (spec.endLabel) ctext(spec.endLabel, X1, 186, 900, 14, tone('ink'), 96, top);

    /* ── 표식 : 끝 눈금에 닿는 순간 꺼진다 ──────────── */
    if (gone < 0.99) {
      const a = top * (1 - gone);
      ctx.save();
      ctx.globalAlpha = a;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, hx - MARK_W / 2, MARK_CY - MARK_H / 2, MARK_W, MARK_H, 7); ctx.stroke();
      ctx.restore();
      if (spec.face) ctext(spec.face, hx, MARK_CY + 5, 800, 14, tone('ink'), MARK_W - 10, a);
    }

    /* ── 자리를 뜨는 간호 ──────────────────────────── */
    if (leave < 0.99) {
      const a = top * (1 - leave);
      const cx = CHIP_X + 30 * leave, cy = lerp(MARK_CY, 52, leave);
      ctx.save();
      ctx.globalAlpha = a;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, cx - CHIP_W / 2, cy - CHIP_H / 2, CHIP_W, CHIP_H, 7); ctx.stroke();
      ctx.restore();
      if (spec.careLabel) ctext(spec.careLabel, cx, cy + 5, 800, 13, tone('accent'), CHIP_W - 10, a);
      if (spec.leaveLabel) {
        ctext(spec.leaveLabel, CHIP_X, 40, 900, 13, tone('accent'), 120,
          top * Math.sin(Math.PI * clamp(leave * 0.9)));
      }
    }

    /* ── 남는 자국 : 무덤 ──────────────────────────── */
    if (rise > 0.01) {
      const hgt = STONE_H * rise, y0 = STONE_BASE - hgt;
      const r = Math.min(STONE_W / 2, hgt / 2);
      ctx.save();
      ctx.globalAlpha = top;
      setShadow(ctx, GLOW, 12 + 5 * Math.sin(t * 1.9));
      ctx.fillStyle = tone('accent');
      ctx.beginPath();
      ctx.moveTo(X1 - STONE_W / 2, STONE_BASE);
      ctx.lineTo(X1 - STONE_W / 2, y0 + r);
      ctx.arcTo(X1 - STONE_W / 2, y0, X1, y0, r);
      ctx.arcTo(X1 + STONE_W / 2, y0, X1 + STONE_W / 2, y0 + r, r);
      ctx.lineTo(X1 + STONE_W / 2, STONE_BASE);
      ctx.closePath();
      ctx.fill();
      clearShadow(ctx);
      ctx.restore();
      if (spec.graveLabel) {
        ctext(spec.graveLabel, X1, 64, 900, 13, tone('accent'), 96, top * clamp((rise - 0.5) / 0.35));
      }
    }

    /* ── 마을의 마지막 날 ──────────────────────────── */
    if (cardK > 0.01) {
      const cy = CARD_Y + CARD_SLIDE * (1 - cardK);
      ctx.save();
      ctx.globalAlpha = cardK;
      setShadow(ctx, GLOW, 14);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.setLineDash([15, 11]);
      ctx.lineDashOffset = -((t * 20) % 26);
      roundRect(ctx, 26, cy, w - 52, CARD_H, 12); ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
      if (spec.cardTitle) {
        ctext(spec.cardTitle, w / 2, cy + 38, 900, 17, tone('accent'), w - 92,
          clamp((cardK - 0.15) / 0.4));
      }
      if (spec.cardLine) {
        ctext(spec.cardLine, w / 2, cy + 68, 800, 13.5, tone('sub'), w - 92,
          clamp((cardK - 0.45) / 0.4));
      }
    }

    ctx.textAlign = 'left';
  }
};
