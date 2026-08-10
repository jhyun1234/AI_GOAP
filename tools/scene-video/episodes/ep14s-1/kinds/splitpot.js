import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* splitpot — 뒤집힘. 가운데 하나였던 그릇이 사람마다 둘(주머니 · 곳간)로 갈라진다.

   원문 근거(문장 그대로):
   > "그래서 목표를 하나로 정했습니다. 창고를 지우고 각자의 주머니와 각자의 곳간을 준다."
   > "주민이 몸에 지니고 다니는 것과 자기 집 곳간에 넣어둔 것을 아예 다른 항목으로
   >  관리했습니다."

   🔴 기본 도형(하나뿐인 그릇)이 여기서 **갈라진다.** SH 에서 하나였던 그릇의 내용물이
   그대로 떠올라 새 그릇 넷으로 나뉘어 들어간다 — 없어진 것이 아니라 **주인이 생긴 것**
   이라는 게 형태로 읽혀야 해서, 알갱이를 지우지 않고 옮겼다.
   🔴 「인벤토리」·「선반」·「슬롯」은 화면에도 자막에도 없다. 원문이 이미 준 쉬운 말
   (「주머니」·「곳간」)만 쓴다(ADR-V25-13 ①풀어쓰기).

   🔴 알갱이가 넷인 것은 **새로 생긴 그릇이 넷**(사람 둘 × 주머니·곳간)이라 하나씩
   가느라 그런 것이지 곡식의 개수가 아니다. 그릇 안은 전부 셀 수 없는 채움이다.

   🔴 흰색과 강조색이 같은 픽셀을 안 나눈다 (알갱이 한 변 7, 반폭 3.5):
     ⓐ 채움은 전부 clip 안. 좌우 8px · 바닥 6px(주머니는 5px/4px) 들였다.
     ⓑ 알갱이가 머무는 자리 y 76 — 공용 그릇 위끝 94 에서 **14.5px.**
        그릇이 위로 걷힐 때(-18px) 벽이 y 78 까지 올라오지만 x 138~142 · 210~214 이고
        알갱이는 x 150.5~201.5 라 **8.5px** 뜬다.
     ⓒ 알갱이가 내려가는 x 넷: 40(곳간A) · 123(주머니A) · 229(주머니B) · 312(곳간B).
        최근접 흰 획과의 거리 = 8.5 / 9.5 / 9.5 / 8.5px. 전부 그릇의 **입**을 지난다.
     ⓓ 「곳간」 라벨(가운데 정렬, x 54~80 · 272~298)은 알갱이가 내려가는 x 에서
        **10.5px** 떨어져 있다. 라벨이 뜨는 시각(since(1) 1.70)도 알갱이가 지난 뒤다.
     ⓔ 🔴 `setShadow`/`GLOW` 를 안 썼다 — 번짐이 곧 혼합이다.

   세로 예산(캔버스 307): 최하단 = 「주머니」 글립 바닥 약 265(사람 캡슐 바닥 262.5 보다
   낮다). 41px 남는다. 그릇이 내려앉는 동안의 최저점도 같다 — **위에서 내려오므로**
   착지 자리가 곧 최저점이다.
   가로: 곳간 B 오른벽 획 바깥 328(캔버스 352).

   ⏱ cue(0) = 「그래서 창고를 통째로 지웠어요」 — 내용물이 넷으로 떠오르고(0.22~) 빈 그릇이
      위로 걷힌다(0.52~).
      cue(1) = 「각자한테 주머니랑 자기 곳간을 줬어요」 — **사건을 전부 `since(1)` 위에
      세웠다**: 주머니 0.35~0.80 · 곳간 착지 **0.95~1.60** · 알갱이 분배 1.60~2.30 ·
      라벨 1.70~2.05. 🔴 `tick` 의 `delayMs 1600` 이 이 착지와 **같은 원점**을 쓰므로
      TTS 실측이 필요 없다(자막 길이가 바뀌어도 안 움직인다).

   계속 도는 것 = 새 그릇 넷의 채움 빗금 스크롤 + 알갱이 넷의 이동. */

const POT_L = 140, POT_R = 212, POT_TOP = 96, POT_BOT = 164, POT_CX = 176;
const POT_FILL_L = 150, POT_FILL_R = 202, POT_FILL_BOT = 156, POT_FILL_H = 32;

const A_CX = 67, B_CX = 285;
const ST_HW = 41, ST_TOP = 118, ST_BOT = 190;          // 곳간 26~108 / 244~326
const ST_FILL_INSET = 8, ST_FILL_BOT = 182, ST_FILL_H = 30;

const PK_HW = 15, PK_TOP = 218, PK_BOT = 244;          // 주머니 108~138 / 214~244
const A_PK = 123, B_PK = 229;
const PK_FILL_INSET = 8, PK_FILL_BOT = 238, PK_FILL_H = 11;

const PERSON_Y = 234, P_W = 40, P_H = 54;

const PARK_Y = 76;
const PARK = [
  { x: 154, tx: 40, ty: 160, kind: 'store' },   // 곳간 A
  { x: 168, tx: A_PK, ty: 232, kind: 'pocket' },// 주머니 A
  { x: 184, tx: B_PK, ty: 232, kind: 'pocket' },// 주머니 B
  { x: 198, tx: 312, ty: 160, kind: 'store' }   // 곳간 B
];

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

    const c0 = cue(spec.wipeCue ?? 0, 0.15, 0.85);
    const st = ease(clamp(c0 / 0.12));
    if (st <= 0.02) { ctx.textAlign = 'left'; return; }

    person(A_CX, PERSON_Y, spec.busyFace, st);
    person(B_CX, PERSON_Y, spec.idleFace, st);

    /* ── 가운데 그릇 : 내용물이 떠오르고 빈 그릇이 걷힌다 ── */
    const riseK = ease(clamp((c0 - 0.20) / 0.34));
    const wipeK = ease(clamp((c0 - 0.52) / 0.34));
    const potA = st * (1 - wipeK);
    const potDY = -18 * wipeK;
    if (potA > 0.01) {
      bowl(POT_L, POT_R, POT_TOP + potDY, POT_BOT + potDY, potA);
      const ph = POT_FILL_H * (1 - riseK);
      if (ph > 2) {
        fill(POT_FILL_L, POT_FILL_BOT + potDY - ph, POT_FILL_R, POT_FILL_BOT + potDY,
          tone('accent'), potA, 18);
      }
      ctext(spec.potLabel, POT_CX, 186, 800, 13, tone('ink'), 96, potA);
    }

    /* ── 각자에게 : 전부 since(1) 위에 세운다 ─────────
       주머니 0.35~0.80 → 곳간 착지 0.95~1.60(= tick 1600) → 분배 1.60~2.30 → 라벨 1.70~2.05 */
    const s1 = since(spec.giveCue ?? 1);
    const pocketK = s1 > 0 ? ease(clamp((s1 - 0.35) / 0.45)) : 0;
    const landK = s1 > 0 ? ease(clamp((s1 - 0.95) / 0.65)) : 0;
    const labelK = s1 > 0 ? ease(clamp((s1 - 1.70) / 0.35)) : 0;

    /* 주머니 — 위에서 살짝 내려와 캡슐 옆에 붙는다 */
    if (pocketK > 0.02) {
      const dy = -10 * (1 - pocketK);
      for (const cx of [A_PK, B_PK]) {
        bowl(cx - PK_HW, cx + PK_HW, PK_TOP + dy, PK_BOT + dy, pocketK, 3);
      }
      ctext(spec.pocketLabel, A_PK, 262, 800, 10.5, tone('sub'), 60, labelK);
      ctext(spec.pocketLabel, B_PK, 262, 800, 10.5, tone('sub'), 60, labelK);
    }

    /* 곳간 — 머리 위로 내려앉는다. 착지 = since(1) 1.60초 */
    if (landK > 0.02) {
      const dy = -22 * (1 - landK);
      for (const cx of [A_CX, B_CX]) {
        bowl(cx - ST_HW, cx + ST_HW, ST_TOP + dy, ST_BOT + dy, landK);
      }
      ctext(spec.storeLabel, A_CX, 110, 800, 12, tone('sub'), 60, labelK);
      ctext(spec.storeLabel, B_CX, 110, 800, 12, tone('sub'), 60, labelK);
    }

    /* ── 떠 있던 알갱이 넷이 새 그릇으로 하나씩 ────── */
    const arrived = [0, 0, 0, 0];
    for (let i = 0; i < PARK.length; i++) {
      const g = PARK[i];
      const rk = ease(clamp((c0 - (0.22 + i * 0.05)) / 0.26));
      if (rk <= 0.01) continue;
      const fk = s1 > 0 ? ease(clamp((s1 - (1.60 + i * 0.07)) / 0.55)) : 0;
      arrived[i] = fk;

      let gx = lerp(POT_CX, g.x, rk);
      let gy = lerp(POT_FILL_BOT - 10, PARK_Y, rk);
      let ga = st;
      if (fk > 0) {
        if (fk < 0.45) { gx = lerp(g.x, g.tx, ease(fk / 0.45)); gy = PARK_Y; }
        else { gx = g.tx; gy = lerp(PARK_Y, g.ty, ease((fk - 0.45) / 0.55)); }
        ga = st * (1 - ease(clamp((fk - 0.86) / 0.14)));
      }
      grain(gx, gy, ga);
    }

    /* ── 새 그릇의 채움 : 알갱이가 닿은 뒤부터 ──────── */
    const storeK = [arrived[0], arrived[3]];
    const pockK = [arrived[1], arrived[2]];
    const ripple = 2 * Math.sin(t * 2.1);
    [A_CX, B_CX].forEach((cx, n) => {
      const k = ease(clamp((storeK[n] - 0.55) / 0.45));
      if (k <= 0.01) return;
      const h = ST_FILL_H * k + ripple * k;
      fill(cx - ST_HW + ST_FILL_INSET, ST_FILL_BOT - h, cx + ST_HW - ST_FILL_INSET, ST_FILL_BOT,
        tone('accent'), landK, 19 + n * 3);
    });
    [A_PK, B_PK].forEach((cx, n) => {
      const k = ease(clamp((pockK[n] - 0.55) / 0.45));
      if (k <= 0.01) return;
      const h = PK_FILL_H * k;
      fill(cx - PK_HW + PK_FILL_INSET, PK_FILL_BOT - h, cx + PK_HW - PK_FILL_INSET, PK_FILL_BOT,
        tone('accent'), pocketK, 13 + n * 3);
    });

    ctx.textAlign = 'left';
  }
};
