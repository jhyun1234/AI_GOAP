import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* sameday — 어긋남. 한 일은 완전히 다른데 하루의 끝이 같다. 그리고 그 이유는
   가운데 그릇에 이름이 안 적혀 있다는 것이다.

   원문 근거(문장 그대로):
   > "옆에서 부지런히 밭을 갈던 농사꾼과 하루의 끝이 완전히 똑같았습니다.
   >  창고는 마을 것이고, 마을 것에는 이름이 안 적혀 있으니까요."
   > "곳간에 이름을 적어 넣는 작업이 결국 그걸 하는 일이었습니다."

   🔴 화면의 **빈 이름표는 내가 지어낸 비유가 아니다.** 원문이 「이름이 안 적혀 있다」·
   「이름을 적어 넣는」이라고 두 번 쓴다 — 그 낱말을 그린 것이다(`notes.비유` = 없음).

   🔴 **길이가 곧 값이다.** 두 사람 아래 칸 셋은 한쪽만 꽉 차고 다른 쪽은 테두리만
   남는데, 머리 위 그릇 둘은 **같은 함수로 같은 높이까지** 찬다(`bowlK` 하나를 둘이
   나눠 쓴다). 그래서 「똑같다」가 우연이 아니라 구조로 보장된다.

   🔴 흰색과 강조색이 같은 픽셀을 안 나눈다 (알갱이 한 변 7, 반폭 3.5):
     ⓐ 그릇 채움은 clip 안에서만 그리고 좌우 벽 안쪽 획에서 8px, 바닥에서 6px 들였다.
     ⓑ 일 칸 안 빗금은 **흰색**이라 강조색과 만날 일이 없다(칸에는 알갱이가 안 간다).
     ⓒ 가운데 그릇에서 솟는 알갱이는 x 170 · 182 로 오르는데, 그 그릇의 벽은 146·206,
        위 그릇들의 벽은 135·217 이라 **가장 가까운 것과도 30px 이상** 뜬다.
     ⓓ 알갱이가 y 52 로 가로지를 때 위 그릇 테두리 위끝은 64 → **8.5px.**
     ⓔ 알갱이가 위 그릇으로 내려가는 x 는 104·248 로 그릇의 **입** 한복판이다
        (벽 안쪽 75~133 / 219~277).
     ⓕ 🔴 `setShadow`/`GLOW` 를 안 썼다 — 번짐이 곧 혼합이다.

   🔴 긴 가로 점선을 `setLineDash` 로 그리지 않았다(ep13s-1 이 30fps 결정성 3패스에서
   그것 하나로 갈렸다). 이름표 안의 밑줄은 폭 36px 짜리 **`fillRect` 조각**이고 위상은
   `((v % P) + P) % P` 로 양수 정규화했다.

   세로 예산(캔버스 307): 최하단 = 오른쪽 이름 라벨 글립 바닥 약 290. 16px 남는다.
   가로: 오른쪽 이름 라벨이 가장 넓고 `measureText` + `clamp` 로 안쪽에 가둔다.

   ⏱ cue(0) = 「밭을 간 농사꾼이랑 하루 끝이 똑같았거든요」 — 일 칸이 한쪽만 차고(0.06~0.46)
      그다음 두 그릇이 같이 찬다(0.48~0.86).
      cue(1) = 「마을 창고엔 이름이 안 적혀 있으니까요」 — 가운데 그릇과 빈 이름표가 뜬다.
      알갱이 왕복은 `since(1)` 위의 1.5초 루프라 자막 길이가 바뀌어도 위상이 안 밀린다.

   계속 도는 것 = 일 칸 셋의 빗금 스크롤 + 그릇 셋의 채움 빗금 + 이름표 밑줄의 흐름 +
   알갱이 둘의 왕복. */

const BOWL_Y = 66, BOWL_BOT = 118, BOWL_HW = 31;
const A_CX = 104, B_CX = 248;
const FILL_INSET = 8, FILL_BOT = 110, FILL_MAX = 34;

const PERSON_Y = 178, P_W = 40, P_H = 54;

const CELL_X_HW = 26, CELL_H = 14, CELL_N = 3;
const CELL_Y0 = 216, CELL_GAP = 19;

const MID_CX = 176, MID_L = 146, MID_R = 206, MID_TOP = 150, MID_BOT = 196;
const MID_FILL_L = 156, MID_FILL_R = 196, MID_FILL_BOT = 188, MID_FILL_H = 22;

const TAG_L = 152, TAG_R = 200, TAG_TOP = 204, TAG_H = 20;

const LANE_Y = 52;
const GR = 7;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const ctext = (txt, x, y, weight, size, color, maxW, alpha) => {
      if (!txt || alpha <= 0.01) return;
      let fs = size; ctx.font = disp(weight, fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(weight, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.textAlign = 'center'; ctx.fillStyle = color;
      ctx.fillText(txt, clamp(x, 12 + tw / 2, w - 12 - tw / 2), y);
      ctx.restore();
    };

    const bowl = (lx, rx, top, bot, alpha, lw = 4) => {
      if (alpha <= 0.01) return;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = lw;
      ctx.lineCap = 'round'; ctx.lineJoin = 'round';
      ctx.beginPath();
      ctx.moveTo(lx, top); ctx.lineTo(lx, bot); ctx.lineTo(rx, bot); ctx.lineTo(rx, top);
      ctx.stroke();
      ctx.restore();
    };

    const fill = (x0, y0, x1, y1, color, alpha, speed) => {
      if (x1 - x0 < 2 || y1 - y0 < 2 || alpha <= 0.01) return;
      ctx.save();
      ctx.beginPath(); ctx.rect(x0, y0, x1 - x0, y1 - y0); ctx.clip();
      ctx.globalAlpha = alpha * 0.42;
      ctx.fillStyle = color;
      ctx.fillRect(x0, y0, x1 - x0, y1 - y0);
      ctx.globalAlpha = alpha * 0.95;
      ctx.strokeStyle = color; ctx.lineWidth = 5;
      const h = y1 - y0, S = 18, off = ((t * speed) % S + S) % S;
      for (let x = x0 - h - S + off; x < x1 + S; x += S) {
        ctx.beginPath(); ctx.moveTo(x, y1); ctx.lineTo(x + h, y0); ctx.stroke();
      }
      ctx.restore();
    };

    const person = (cx, cy, face, alpha) => {
      if (alpha <= 0.01) return;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, cx - P_W / 2, cy - P_H / 2, P_W, P_H, P_W / 2);
      ctx.stroke();
      ctx.restore();
      ctext(face, cx, cy + 5, 800, 13.5, tone('ink'), P_W - 8, alpha);
    };

    const grain = (x, y, alpha) => {
      if (alpha <= 0.01) return;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.fillStyle = tone('accent');
      ctx.fillRect(x - GR / 2, y - GR / 2, GR, GR);
      ctx.restore();
    };

    const c0 = cue(spec.dayCue ?? 0, 0.15, 0.85);
    const st = ease(clamp(c0 / 0.14));
    if (st <= 0.02) { ctx.textAlign = 'left'; return; }

    /* ── 사람 둘 ───────────────────────────────────── */
    person(A_CX, PERSON_Y, spec.busyFace, st);
    person(B_CX, PERSON_Y, spec.idleFace, st);

    /* ── 아래: 한 일. 왼쪽만 찬다 ───────────────────── */
    for (let i = 0; i < CELL_N; i++) {
      const y = CELL_Y0 + i * CELL_GAP;
      for (const cx of [A_CX, B_CX]) {
        ctx.save();
        ctx.globalAlpha = st;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        roundRect(ctx, cx - CELL_X_HW, y, CELL_X_HW * 2, CELL_H, 4); ctx.stroke();
        ctx.restore();
      }
      const k = ease(clamp((c0 - (0.06 + i * 0.12)) / 0.14));
      if (k > 0.01) {
        fill(A_CX - CELL_X_HW + 4, y + 3,
          lerp(A_CX - CELL_X_HW + 4, A_CX + CELL_X_HW - 4, k), y + CELL_H - 3,
          tone('ink'), st * 0.9, 14 + i * 5);
      }
    }

    /* ── 위: 하루의 끝. 두 그릇이 같은 높이까지 찬다 ── */
    const bowlK = ease(clamp((c0 - 0.48) / 0.38));
    const bh = FILL_MAX * bowlK;
    for (const cx of [A_CX, B_CX]) {
      bowl(cx - BOWL_HW, cx + BOWL_HW, BOWL_Y, BOWL_BOT, st);
      if (bh > 2) {
        fill(cx - BOWL_HW + FILL_INSET, FILL_BOT - bh, cx + BOWL_HW - FILL_INSET, FILL_BOT,
          tone('accent'), st, 19);
      }
    }
    const endA = ease(clamp((c0 - 0.44) / 0.18));
    ctext(spec.endLabel, A_CX, 136, 800, 11.5, tone('sub'), 96, st * endA);
    ctext(spec.endLabel, B_CX, 136, 800, 11.5, tone('sub'), 96, st * endA);

    /* ── 이름 ──────────────────────────────────────── */
    ctext(spec.busyName, A_CX, 286, 800, 11, tone('sub'), 108, st);
    ctext(spec.idleName, B_CX, 286, 800, 10, tone('sub'), 116, st);

    /* ── 가운데 그릇 + 빈 이름표 ────────────────────── */
    const c1 = cue(spec.nameCue ?? 1, 0.15, 0.55);
    const midK = ease(clamp(c1 / 0.30));
    if (midK > 0.02) {
      bowl(MID_L, MID_R, MID_TOP, MID_BOT, midK);
      fill(MID_FILL_L, MID_FILL_BOT - MID_FILL_H, MID_FILL_R, MID_FILL_BOT,
        tone('accent'), midK, 17);

      const tagK = ease(clamp((c1 - 0.26) / 0.26));
      if (tagK > 0.02) {
        ctx.save();
        ctx.globalAlpha = tagK;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        roundRect(ctx, TAG_L, TAG_TOP, TAG_R - TAG_L, TAG_H, 5); ctx.stroke();
        /* 적을 자리인데 비어 있다 — 밑줄만 흐른다.
           🔴 setLineDash 를 안 쓰고 fillRect 조각으로 채운다(결정성). */
        const P = 11, D = 6, x0 = TAG_L + 6, x1 = TAG_R - 6;
        const off = ((t * 9) % P + P) % P;
        ctx.globalAlpha = tagK * 0.85;
        ctx.fillStyle = tone('ink');
        for (let x = x0 - P + off; x < x1; x += P) {
          const a = Math.max(x0, x), b = Math.min(x1, x + D);
          if (b > a) ctx.fillRect(a, TAG_TOP + 9, b - a, 3);
        }
        ctx.restore();
      }
    }

    /* ── 가운데에서 양쪽으로, 동시에 하나씩 ──────────
       `since(1)` 위의 루프라 자막 길이가 바뀌어도 위상이 안 밀린다. */
    const s1 = since(spec.nameCue ?? 1);
    if (s1 > 0.85) {
      const gp = frac((s1 - 0.85) / (spec.loopSec ?? 1.5));
      const ga = midK * ease(clamp(gp / 0.06)) * (1 - ease(clamp((gp - 0.80) / 0.14)));
      for (const [sx, tx] of [[170, A_CX], [182, B_CX]]) {
        let gx, gy;
        if (gp < 0.30) { gx = sx; gy = lerp(MID_FILL_BOT - 12, LANE_Y, gp / 0.30); }
        else if (gp < 0.62) { gx = lerp(sx, tx, (gp - 0.30) / 0.32); gy = LANE_Y; }
        else if (gp < 0.88) { gx = tx; gy = lerp(LANE_Y, 86, (gp - 0.62) / 0.26); }
        else continue;
        grain(gx, gy, ga);
      }
    }

    ctx.textAlign = 'left';
  }
};
