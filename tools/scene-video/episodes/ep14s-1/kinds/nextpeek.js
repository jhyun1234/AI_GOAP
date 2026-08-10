import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* nextpeek — 아웃트로(4단 구조 ④). 다음 편 사건의 0.5초 테스트 장면을 루프로 돌린다.

   🔴 재료는 **다음 편(`ep14s-2`)이 맡은 사건**에서 뽑았다. 같은 원문 6단계 절:
   > "곡식 6개를 지닌 의뢰인이 고집쟁이 목수에게 집을 부탁했는데 '선불 없이는 안 해'로
   >  거절당했습니다. 의뢰인 잔고는 충분했습니다."
   > "탈락한 건 엉뚱하게도 목수 자신의 주머니 여유 공간이었습니다."
   기획 브리프 §8 이 지정한 **「화면에서 움직이는 것」**을 그대로 그렸다 —
   *"의뢰인 쪽 곡식은 충분한데 목수 주머니가 꽉 차서 품삯이 건너가지 못하고 튕겨 나온다."*
   ⚠️ 원문 끝의 「다음 이야기 예고」 문단은 **블로그의 다음 글** 예고라 안 썼다
   (ep02s·ep11s·ep12s 가 그 형태로 세 번 틀렸다).

   🔴 **수를 한 개도 안 올렸다.** 「곡식 6개」·「상한 8」·「곡식 4개」는 14-2편의 사건
   그 자체라, 여기서 먼저 쓰면 같은 수가 두 편에서 같은 뜻으로 나온다. 대신
   **의뢰인 주머니는 절반쯤, 목수 주머니는 가장자리까지** 찬 것으로만 그렸다 —
   「줄 것은 있고 받을 자리가 없다」가 높이로만 읽힌다.

   🔴 본문의 도형(그릇 · 사람 · 알갱이)은 그대로 물려받되 **축이 다르다** — 본문은
   「하나가 갈라진다」였고 여기는 「건너가지 못한다」다. 그래서 세로로 나누지 않고
   가로로 마주 세웠다.

   🔴 흰색과 강조색이 같은 픽셀을 안 나눈다 (알갱이 한 변 7, 반폭 3.5):
     ⓐ 채움 둘은 clip 안. 좌우 8px · 바닥 4px 들였다.
     ⓑ 알갱이는 출발·도착에서 주머니 위끝(94)과 **6.5px**, 벽의 x 폭에 걸치는 구간에서는
        17~24px 뜬다.
     ⓒ 막는 흰 획은 y 88~92, 알갱이는 막힐 때 y 74.5~81.5 → **6.5px.**
        그 흰 획과 강조색 채움 위끝(104)은 **12px.**
     ⓓ 판정 글자와 밑줄은 y 236~263 이라 강조색이 도는 띠(96~134)에서 100px 넘게 멀다.
     ⓔ 🔴 `setShadow`/`GLOW` 를 안 썼다 — 번짐이 곧 혼합이다.

   세로 예산(캔버스 307): 최하단 = 판정 밑줄 263. 43px 남는다.
   가로: 목수 주머니 오른벽 획 바깥 296(캔버스 352).

   ⏱ 등장은 예고 자막 `cue(0)` 에, 루프는 `t` 에 물렸다.
   🔴 소개 자막(cue 1)에는 아무것도 안 걸었다 — 마지막 3.0초는 아웃트로 카드가 `.vis` 를
      픽셀 동일 좌표로 덮으므로 그 구간에 그린 것은 한 프레임도 안 보인다.
      0.5초 루프가 예고 자막 구간에서 도니 정적 구간도 같이 해결된다. */

const CL_CX = 84, MK_CX = 268;
const PK_HW = 26, PK_TOP = 96, PK_BOT = 134;
const PK_INSET = 8, PK_FILL_BOT = 128;
const CL_FILL_H = 16, MK_FILL_H = 24;          // 절반쯤 ↔ 가장자리까지

const PERSON_Y = 190, P_W = 42, P_H = 58;

const LANE_A = 84, LANE_B = 78;                // 출발 높이 / 막히는 높이
const BLOCK_Y = 90;
const GR = 7;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
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

    const fill = (x0, y0, x1, y1, alpha, speed) => {
      if (x1 - x0 < 2 || y1 - y0 < 2 || alpha <= 0.01) return;
      ctx.save();
      ctx.beginPath(); ctx.rect(x0, y0, x1 - x0, y1 - y0); ctx.clip();
      ctx.globalAlpha = alpha * 0.42;
      ctx.fillStyle = tone('accent');
      ctx.fillRect(x0, y0, x1 - x0, y1 - y0);
      ctx.globalAlpha = alpha * 0.95;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 5;
      const h = y1 - y0, S = 18, off = ((t * speed) % S + S) % S;
      for (let x = x0 - h - S + off; x < x1 + S; x += S) {
        ctx.beginPath(); ctx.moveTo(x, y1); ctx.lineTo(x + h, y0); ctx.stroke();
      }
      ctx.restore();
    };

    const person = (cx, face, alpha) => {
      if (alpha <= 0.01) return;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, cx - P_W / 2, PERSON_Y - P_H / 2, P_W, P_H, P_W / 2);
      ctx.stroke();
      ctx.restore();
      ctext(face, cx, PERSON_Y + 6, 800, 14, tone('ink'), P_W - 8, alpha);
    };

    /* ── 구획 이름 (창 밖) ─────────────────────────── */
    if (spec.label) {
      ctx.save();
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.label, 20, 28);
      ctx.restore();
    }

    const pk = ease(cue(spec.peekCue ?? 0, 0.15, 0.30));
    if (pk <= 0.02) { ctx.textAlign = 'left'; return; }

    /* ── 마주 선 둘 ────────────────────────────────── */
    person(CL_CX, spec.clientFace, pk);
    person(MK_CX, spec.makerFace, pk);
    ctext(spec.clientName, CL_CX, 240, 800, 11, tone('sub'), 80, pk);
    ctext(spec.makerName, MK_CX, 240, 800, 11, tone('sub'), 80, pk);

    /* ── 주머니 둘 : 줄 것은 있고 받을 자리가 없다 ──── */
    bowl(CL_CX - PK_HW, CL_CX + PK_HW, PK_TOP, PK_BOT, pk);
    bowl(MK_CX - PK_HW, MK_CX + PK_HW, PK_TOP, PK_BOT, pk);
    fill(CL_CX - PK_HW + PK_INSET, PK_FILL_BOT - CL_FILL_H, CL_CX + PK_HW - PK_INSET, PK_FILL_BOT,
      pk, 15);
    fill(MK_CX - PK_HW + PK_INSET, PK_FILL_BOT - MK_FILL_H, MK_CX + PK_HW - PK_INSET, PK_FILL_BOT,
      pk, 12);

    /* ── 0.5초 테스트 장면 ─────────────────────────── */
    const u = frac(t / (spec.loopSec ?? 0.5));
    let gx, gy;
    if (u < 0.50) {
      const k = u / 0.50;
      gx = lerp(CL_CX, MK_CX, k);
      gy = lerp(LANE_A, LANE_B, k) - 32 * Math.sin(Math.PI * k);
    } else if (u < 0.64) {
      const k = (u - 0.50) / 0.14;
      gx = MK_CX;
      gy = LANE_B - 3 * Math.sin(Math.PI * k * 2);
    } else {
      const k = (u - 0.64) / 0.36;
      gx = lerp(MK_CX, CL_CX, k);
      gy = lerp(LANE_B, LANE_A, k) - 18 * Math.sin(Math.PI * k);
    }
    ctx.save();
    ctx.globalAlpha = pk;
    ctx.fillStyle = tone('accent');
    ctx.fillRect(gx - GR / 2, gy - GR / 2, GR, GR);
    ctx.restore();

    /* ── 막힘 : 문턱에 흰 획이 서고 판정이 켜진다 ──── */
    const bk = ease(clamp((u - 0.48) / 0.10)) * (1 - ease(clamp((u - 0.88) / 0.12)));
    if (bk > 0.02) {
      ctx.save();
      ctx.globalAlpha = pk * bk;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4; ctx.lineCap = 'round';
      ctx.beginPath();
      ctx.moveTo(MK_CX - PK_HW + 4, BLOCK_Y); ctx.lineTo(MK_CX + PK_HW - 4, BLOCK_Y);
      ctx.stroke();
      ctx.restore();

      ctext(spec.refuseLabel, (CL_CX + MK_CX) / 2, 252, 900, 16, tone('ink'), 140, pk * bk);
      ctx.save();
      ctx.globalAlpha = pk * bk * 0.8;
      ctx.fillStyle = tone('ink');
      ctx.fillRect(152, 260, 48, 3);
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
