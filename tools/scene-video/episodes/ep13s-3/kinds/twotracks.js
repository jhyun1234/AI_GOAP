import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* twotracks — 같은 시계가 두 갈래로 끝난다. 하루 만에 낫거나, 하루 반 만에 죽거나.

   원문 근거(수치 문장 그대로):
   > "명세에 적힌 값으로는, 간호를 받으면 하루 만에 낫고 아무도 돌보지 않으면
   >  하루 반 만에 죽습니다. 여유가 반나절뿐이라 꽤 촉박하죠."

   🔴 기본 도형(끝 눈금을 향해 나아가는 표식)의 **두 벌 나란히** 형태다.
   훅(deathclock)이 그 도형을 원으로 말았다면 여기서 처음 펴진다.

   🔴 **길이가 곧 수치다.** 두 홈의 길이는 같고 채움만 다르다 —
   간호 = 전체의 2/3(하루), 방치 = 전체(하루 반). 두 끝 사이가 곧 반나절이라
   「반나절」 브래킷을 그 사이에 걸면 세 수치가 한 그림에서 서로를 설명한다.
   화면의 수는 「하루」·「하루 반」·「반나절」 셋뿐이고 전부 원문 문장에 있다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **행으로 갈랐다.**
     상단 행(간호)만 강조색: 라벨 84 · 채움 98~108 · 눈금 118~132 · 「하루」 148.
     하단 행(방치)은 전부 흰색: 라벨 176 · 채움 190~200 · 눈금 210~224 · 「하루 반」 240.
     반나절 브래킷(accent)은 250~281 로 **하단 행 아래**에 있고, 그리로 내려가는
     안내선은 246 에서 끊어 브래킷 tick(250)과 4px 띄웠다.
     ⚠️ 안내선(track = 흰색 12%)이 하단 채움을 가로지르지만 둘 다 흰 계열이고,
        **안내선을 막대보다 먼저 그려** 막대가 덮게 했다.
     ⚠️ 「하루」(accent, 글립 136~150 · x 205~243)와 사망 표식(ink, 148~174 · x 292~340)은
        y 가 148~150 에서 스치지만 x 가 49px 떨어져 있어 픽셀이 안 만난다.

   세로 예산(캔버스 307): 최상단 = 회복 표식 56 · 최하단 = 「반나절」 글립 바닥 281.
   가로: 사망 표식 x 292~340(캔버스 352) · 「하루 반」 라벨은 폭을 재서 안쪽에 가둔다.

   ⏱ 자막 어순에 물렸다. 「다친 주민은 하루 반이면 죽어요」 → 하단 행이 끝까지 차고
   표식이 꺼질 듯 선다. 「간호받으면 하루 만에 낫고요」 → 상단 행이 2/3 에서 멎고
   회복 표식이 뜬다. 그 뒤 두 끝 사이에 반나절 브래킷이 걸린다.

   계속 도는 것 = 두 홈 점선의 흐름 + 두 채움 안 빗금의 스크롤(면적이 큰 움직임) +
   브래킷 점선. 자막이 끝난 뒤에도 넷이 계속 돈다. */

const PAD_L = 40, PAD_R = 36;
const CARE_Y = 92, CARE_H = 22, CARE_FY = 98, CARE_FH = 10;
const NEG_Y = 184, NEG_H = 22, NEG_FY = 190, NEG_FH = 10;
const BOX_W = 48, BOX_H = 26;
const BRACKET_Y = 258;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const X0 = PAD_L, X1 = w - PAD_R;
    const F0 = X0 + 4, F1 = X1 - 4;
    const CARE_X = F0 + (F1 - F0) * 2 / 3;      // 하루 = 하루 반의 2/3

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

    /* 채움 = 단색 바탕 + 그 위를 흐르는 빗금. 빗금이 면적을 가진 움직임을 만든다. */
    const band = (x0, y0, x1, y1, color, k, speed) => {
      if (x1 - x0 < 2 || k <= 0.01) return;
      ctx.save();
      ctx.beginPath(); roundRect(ctx, x0, y0, x1 - x0, y1 - y0, (y1 - y0) / 2); ctx.clip();
      ctx.globalAlpha = k * 0.4;
      ctx.fillStyle = color;
      ctx.fillRect(x0, y0, x1 - x0, y1 - y0);
      ctx.globalAlpha = k * 0.95;
      ctx.strokeStyle = color; ctx.lineWidth = 5;
      const h = y1 - y0, S = 19, off = (t * speed) % S;
      for (let x = x0 - h - S + off; x < x1 + S; x += S) {
        ctx.beginPath(); ctx.moveTo(x, y1); ctx.lineTo(x + h, y0); ctx.stroke();
      }
      ctx.restore();
    };

    const groove = (y, h, k) => {
      ctx.save();
      ctx.globalAlpha = k;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.setLineDash([13, 10]);
      ctx.lineDashOffset = -((t * 17) % 23);
      roundRect(ctx, X0, y, X1 - X0, h, h / 2);
      ctx.stroke();
      ctx.restore();
    };

    const c0 = cue(spec.rowCue ?? 0, 0.15, 0.88);
    const st = ease(clamp(c0 / 0.12));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    const k1 = ease(clamp(c0 / 0.36));                 // 「하루 반이면 죽어요」
    const k2 = ease(clamp((c0 - 0.40) / 0.30));        // 「하루 만에 낫고요」
    const k3 = ease(clamp((c0 - 0.74) / 0.22));        // 반나절

    /* ── 안내선 : 막대보다 먼저 그린다 (막대가 덮는다) ── */
    if (k3 > 0.01) {
      ctx.save();
      ctx.globalAlpha = k3 * 0.9;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.setLineDash([7, 8]);
      ctx.lineDashOffset = (t * 14) % 15;
      ctx.beginPath(); ctx.moveTo(CARE_X, 156); ctx.lineTo(CARE_X, 246); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(X1, 226); ctx.lineTo(X1, 246); ctx.stroke();
      ctx.restore();
    }

    /* ── 하단 행 : 방치 → 하루 반 (흰색) ───────────── */
    groove(NEG_Y, NEG_H, st);
    band(F0, NEG_FY, lerp(F0, F1, k1), NEG_FY + NEG_FH, tone('ink'), st, 26);

    if (spec.neglectLabel) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 13); ctx.fillStyle = tone('ink');
      ctx.fillText(spec.neglectLabel, X0, 176);
      ctx.restore();
    }
    if (k1 > 0.5) {
      const a = clamp((k1 - 0.5) / 0.4);
      ctx.save();
      ctx.globalAlpha = a;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4; ctx.lineCap = 'round';
      ctx.beginPath(); ctx.moveTo(X1, 210); ctx.lineTo(X1, 224); ctx.stroke();
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, X1 - BOX_W / 2, 148, BOX_W, BOX_H, 7); ctx.stroke();
      ctx.restore();
      if (spec.hurtFace) ctext(spec.hurtFace, X1, 168, 800, 14, tone('ink'), BOX_W - 10, a);
      if (spec.neglectEnd) ctext(spec.neglectEnd, X1, 240, 900, 14, tone('ink'), 96, a);
    }

    /* ── 상단 행 : 간호 → 하루 (강조색) ────────────── */
    groove(CARE_Y, CARE_H, st);
    band(F0, CARE_FY, lerp(F0, CARE_X, k2), CARE_FY + CARE_FH, tone('accent'), st, 22);

    if (spec.careLabel) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 13); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.careLabel, X0, 84);
      ctx.restore();
    }
    if (k2 > 0.5) {
      const a = clamp((k2 - 0.5) / 0.4);
      ctx.save();
      ctx.globalAlpha = a;
      setShadow(ctx, GLOW, 12);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4; ctx.lineCap = 'round';
      ctx.beginPath(); ctx.moveTo(CARE_X, 118); ctx.lineTo(CARE_X, 132); ctx.stroke();
      clearShadow(ctx);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, CARE_X - BOX_W / 2, 56, BOX_W, BOX_H, 7); ctx.stroke();
      ctx.restore();
      if (spec.wellFace) ctext(spec.wellFace, CARE_X, 76, 800, 14, tone('ink'), BOX_W - 10, a);
      if (spec.careEnd) ctext(spec.careEnd, CARE_X, 148, 900, 14, tone('accent'), 88, a);
    }

    /* ── 반나절 : 두 끝 사이 ───────────────────────── */
    if (k3 > 0.01) {
      const bx = lerp(CARE_X, X1, k3);
      ctx.save();
      ctx.globalAlpha = k3;
      setShadow(ctx, GLOW, 10);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineCap = 'round';
      ctx.beginPath(); ctx.moveTo(CARE_X, BRACKET_Y); ctx.lineTo(bx, BRACKET_Y); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(CARE_X, 250); ctx.lineTo(CARE_X, BRACKET_Y); ctx.stroke();
      if (k3 > 0.9) { ctx.beginPath(); ctx.moveTo(X1, 250); ctx.lineTo(X1, BRACKET_Y); ctx.stroke(); }
      clearShadow(ctx);
      ctx.restore();
      if (spec.gapLabel) {
        ctext(spec.gapLabel, (CARE_X + X1) / 2, 278, 900, 14, tone('accent'), 110,
          clamp((k3 - 0.55) / 0.35));
      }
    }

    ctx.textAlign = 'left';
  }
};
