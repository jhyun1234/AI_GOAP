import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* graze — 자연스러워 보이던 구현. 곁에 선 사람도, 스쳐 간 사람도 결과가 같다.

   원문 근거:
   > "여기서 자연스러워 보이는 구현은 '누가 간호를 시작하면 방치 시간을 0으로 되돌린다'입니다.
   >  실제로 처음엔 저도 그게 맞는 줄 알았습니다."
   > "그런데 그렇게 만들면 사망이 사실상 불가능해집니다. 지나가던 주민이 0.1초만 곁에
   >  스쳐도 죽음의 시계가 처음으로 되감기니까요."

   🔴 **한 화면에 두 배우를 갈아 끼운다.** 자막 0(간호가 시작되면 0으로)에는 강조색
   「간호」 칩이 표식 곁에 내려와 앉고, 자막 1(0.1초만 스쳐도)에는 그 칩이 사라지고
   아래 길로 **흰 통행인**이 지나간다. **되감기는 두 번 다 똑같이 일어난다** —
   이 편이 뒤집을 문장("곁에 머문 것과 스쳐 간 것이 구분되지 않는다")이 그림에서 먼저 선다.

   🔴 훅(deathclock)의 되감기와 이 되감기는 다른 그림이다. 훅에는 원인이 없고(원이 혼자
   돈다) 여기에는 **원인 배우와 이름표(0.1초·되감기)**가 있다. 훅이 증상, 여기가 원인이다.

   ⏱ **되감기 시각은 `since(1)` 위에 세웠다** — `t` 로 돌리면 자막 길이가 바뀔 때마다
   사건 시각이 통째로 밀리고 `sfx.delayMs` 를 다시 못 푼다. `since(1)` 은 그 자막이
   시작된 뒤 흐른 초라 **자막 시작 기준인 `delayMs` 와 같은 원점**을 쓴다.
   주기 1.7초이므로 첫 되감기는 자막 시작 + 0.95~1.11초에서 일어난다(TTS 길이와 무관).

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **층으로 갈랐다.**
     되감기 라벨(accent) 55~71 · 표식(ink) 86~114 · 되감기 화살(accent) 120~124 ·
     홈(track) 132~156 · 채움(ink) 138~150 · 「하루 반」(ink) 174~189 ·
     「0.1초」(accent) 188~206 · 길(track) 226 · 통행인(ink) 216~236.
     「0.1초」와 「하루 반」은 y 가 1px 스치지만 x 가 59px 떨어져 있다(185~235 / 294~338).
     「간호」 칩(accent, x 249~303)은 채움의 최대 오른쪽 끝(210)보다 39px 오른쪽에 선다.

   🔴 통행인은 화면 밖에서 들어오지만 **가장자리에 닿기 전에 알파가 0 이 된다**
     (px 16 이하 / w−16 이상에서 0). 캔버스 바깥 2열 띠를 한 프레임도 안 칠한다.

   세로 예산(캔버스 307): 최저 = 통행인 바닥 236. 71px 남는다.

   계속 도는 것 = 홈 점선 · 길 점선 · 채움 빗금(면적) · 되감기 주기(자막 1 구간 내내). */

const PAD_L = 44, PAD_R = 36;
const GY = 132, GH = 24, FY = 138, FH = 12;
const MARK_CY = 100, MARK_W = 48, MARK_H = 28;
const LANE_Y = 226, PASS_R = 10;
const ARROW_Y = 122;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const X0 = PAD_L, X1 = w - PAD_R, F0 = X0 + 5;
    const MEET = F0 + (X1 - 8 - F0) * 0.62;
    const CHIP_X = MEET + 66, CHIP_W = 54, CHIP_H = 28;

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

    const c0 = cue(spec.ruleCue ?? 0, 0.15, 0.92);
    const st = ease(clamp(c0 / 0.12));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    /* ── 두 배우의 무게 ────────────────────────────── */
    const s1 = since(spec.grazeCue ?? 1);
    const sw = s1 >= 0 ? ease(clamp(s1 / 0.45)) : 0;      // 0 = 간호 칩 / 1 = 통행인
    const P = spec.loopSec ?? 1.7;
    const q = s1 >= 0 ? frac(s1 / P) : 0;

    /* 머리 : 두 구동이 같은 자리(F0)에서 만나므로 이음매에 튐이 없다 */
    const hxA = F0 + (MEET - F0) * ease(clamp(c0 / 0.52)) * (1 - ease(clamp((c0 - 0.56) / 0.10)));
    const hxB = F0 + (MEET - F0) * ease(clamp(q / 0.55)) * (1 - ease(clamp((q - 0.56) / 0.09)));
    const hx = lerp(hxA, hxB, sw);

    const flash = lerp(
      Math.sin(Math.PI * clamp((c0 - 0.56) / 0.16)),
      Math.sin(Math.PI * clamp((q - 0.56) / 0.16)), sw);

    /* ── 구획 이름 ─────────────────────────────────── */
    if (spec.title) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
      ctx.restore();
    }

    /* ── 길 : 마을 사람들이 지나다니는 자리 ─────────── */
    ctx.save();
    ctx.globalAlpha = st * 0.9;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.setLineDash([11, 10]);
    ctx.lineDashOffset = (t * 22) % 21;
    ctx.beginPath(); ctx.moveTo(20, LANE_Y); ctx.lineTo(w - 20, LANE_Y); ctx.stroke();
    ctx.restore();

    /* ── 홈과 채움 ─────────────────────────────────── */
    ctx.save();
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.setLineDash([13, 10]);
    ctx.lineDashOffset = -((t * 17) % 23);
    roundRect(ctx, X0, GY, X1 - X0, GH, GH / 2); ctx.stroke();
    ctx.restore();

    if (hx - F0 > 2) {
      ctx.save();
      ctx.beginPath(); roundRect(ctx, F0, FY, hx - F0, FH, FH / 2); ctx.clip();
      ctx.globalAlpha = st * 0.4; ctx.fillStyle = tone('ink');
      ctx.fillRect(F0, FY, hx - F0, FH);
      ctx.globalAlpha = st * 0.95;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 5;
      const S = 19, off = (t * 26) % S;
      for (let x = F0 - FH - S + off; x < hx + S; x += S) {
        ctx.beginPath(); ctx.moveTo(x, FY + FH); ctx.lineTo(x + FH, FY); ctx.stroke();
      }
      ctx.restore();
    }

    /* ── 끝 눈금 : 하루 반 ─────────────────────────── */
    ctx.save();
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4; ctx.lineCap = 'round';
    ctx.beginPath(); ctx.moveTo(X1, 124); ctx.lineTo(X1, 170); ctx.stroke();
    ctx.restore();
    if (spec.endLabel) ctext(spec.endLabel, X1, 186, 900, 14, tone('ink'), 96, st);

    /* ── 표식 : 다친 주민. 머리 위에 얹혀 함께 나아간다 ── */
    ctx.save();
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, hx - MARK_W / 2, MARK_CY - MARK_H / 2, MARK_W, MARK_H, 7); ctx.stroke();
    ctx.restore();
    if (spec.face) ctext(spec.face, hx, MARK_CY + 5, 800, 14, tone('ink'), MARK_W - 10, st);

    /* ── 배우 ① 곁에 앉는 간호 (자막 0) ──────────────── */
    const chipK = ease(clamp((c0 - 0.24) / 0.22)) * (1 - sw);
    if (chipK > 0.01 && spec.careLabel) {
      const cy = lerp(48, MARK_CY, ease(clamp((c0 - 0.24) / 0.22)));
      ctx.save();
      ctx.globalAlpha = chipK;
      setShadow(ctx, GLOW, 12);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, CHIP_X - CHIP_W / 2, cy - CHIP_H / 2, CHIP_W, CHIP_H, 7); ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
      ctext(spec.careLabel, CHIP_X, cy + 5, 800, 13, tone('accent'), CHIP_W - 10, chipK);
    }

    /* ── 배우 ② 스쳐 가는 통행인 (자막 1) ────────────── */
    if (sw > 0.01) {
      const V = (w + 40 - MEET) / 0.25;
      const px = MEET + (0.55 - q) * V;
      const edge = clamp((px - 16) / 26) * clamp((w - 16 - px) / 26);
      if (edge > 0.01) {
        ctx.save();
        ctx.globalAlpha = sw * edge;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.arc(px, LANE_Y, PASS_R, 0, Math.PI * 2); ctx.stroke();
        ctx.restore();
      }
      if (spec.grazeLabel) {
        ctext(spec.grazeLabel, MEET, 202, 900, 14, tone('accent'), 96,
          sw * ease(clamp(1 - Math.abs(q - 0.56) / 0.18)));
      }
    }

    /* ── 되감기 : 두 배우 모두에게서 똑같이 일어난다 ── */
    if (flash > 0.02) {
      const from = F0 + (MEET - F0) * (1 - clamp((sw < 0.5 ? (c0 - 0.56) : (q - 0.56)) / 0.10));
      ctx.save();
      ctx.globalAlpha = flash;
      setShadow(ctx, GLOW, 12);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineCap = 'round';
      ctx.beginPath(); ctx.moveTo(Math.max(from, F0 + 18), ARROW_Y); ctx.lineTo(F0, ARROW_Y); ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(F0 + 11, ARROW_Y - 7); ctx.lineTo(F0, ARROW_Y); ctx.lineTo(F0 + 11, ARROW_Y + 7);
      ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
      if (spec.rewindLabel) ctext(spec.rewindLabel, w / 2, 68, 900, 14, tone('accent'), 130, flash);
    }

    ctx.textAlign = 'left';
  }
};
