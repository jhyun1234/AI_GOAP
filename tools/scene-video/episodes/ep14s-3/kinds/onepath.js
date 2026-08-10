import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* onepath — 사건. 「집은 목수한테 부탁해야만 생기거든요.」

   원문 근거:
   > "집이 생기는 경로는 '누군가가 목수에게 부탁하기' 하나뿐인데…"  (8단계 절)
   > "이 게임에서 집은 목수에게 부탁해서만 지어집니다
   >  (이전 밀스톤에서 자동 건설을 일부러 폐기했습니다)."          (6단계 절)

   🔴 이 샷은 **병목을 먼저 세우는 자리**다. 마지막 샷(selfbuild)의 남는 한 줄
   「조건 없이 열면 … 부탁 시스템 자체가 붕괴」가 여기서 세운 깔때기를 해체하며 되받는다.
   그래서 회차 안 되받기가 「뒤집기」 조건을 만족한다 — 모이던 것이 흩어진다.

   🖼️ 라벨을 전부 가려도 남는 사건:
       **여러 갈래가 가운데 한 칸으로 전부 모였다가, 거기서만 한 갈래가 나가 결과를 만든다.**

   🔴 「경로가 하나뿐」이라는 사실을 **막힌 다른 길**로 그리지 않았다 —
      원문은 다른 경로가 막혀 있다고 쓰지 않았고 애초에 하나만 있다고 쓴다.
      없는 것을 그리면 없는 사실이 생긴다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **층 + 이동 구간 상한**으로 갈랐다.
     위 자리(ink)  57~83  · 가운데 자리(ink) 135~169 · 집(ink) 208~250.
     강조색은 ⓐ길 위를 흐르는 점(윗길 y 89~129 · 아랫길 y 173.5~202.5)과
     ⓑ「부탁」 라벨(글립 106~122, x 25~56) ⓒ「목수」 라벨(글립 145~161, x 217~279) 뿐이다.
     ⓒ 는 가운데 자리(ink, x 145~207)의 **오른쪽 밖**에 세웠고 `maxW 62` 로 폭을 묶어
     최악의 경우에도 x 217 보다 왼쪽으로 못 온다 — 10px 뜬다.
     점의 이동 범위는 `clamp` 가 아니라 **경로 끝값 자체**로 잘라 두었고(94~124 / 178~198)
     점에는 글로우를 안 건다 — 글로우를 걸면 4~6px 여유가 그대로 먹힌다.

   🔴 화면에 수를 한 개도 안 올렸다. 부탁하는 자리 셋은 `spec.askers` 로만 있다.

   세로 예산(캔버스 307): 최저 = 집 바닥 250. 57px 남는다.
   가로: 위 자리가 57~295, 「부탁」 라벨이 23~58(캔버스 352). ctext 가 폭을 재서 가둔다.

   ⏱ 등장은 자막 `cue(0)` 에, 흐르는 점과 빗금은 `t` 에 물렸다.
   계속 도는 것 = 점 넷이 길을 흘러 내려간다(주기 spec.flowSec) + 집 몸통 빗금 + 길 점선 흐름. */

const ASK_CY = 70, ASK_W = 40, ASK_H = 26;
const HUB_CY = 152, HUB_W = 62, HUB_H = 34;
const HOUSE_BASE = 250, HOUSE_H = 42, HOUSE_W = 56;
const L1_TOP = 94, L1_BOT = 124;        // 윗길 (부탁이 모인다)
const L2_TOP = 178, L2_BOT = 198;       // 아랫길 (집이 나간다)
const DOT_R = 4.5;

function housePath(ctx, cx, base, w, h) {
  const bodyW = w * 0.78, bodyH = h * 0.60, eave = base - bodyH;
  ctx.beginPath();
  ctx.moveTo(cx - bodyW / 2, base);
  ctx.lineTo(cx - bodyW / 2, eave);
  ctx.lineTo(cx - w / 2, eave);
  ctx.lineTo(cx, base - h);
  ctx.lineTo(cx + w / 2, eave);
  ctx.lineTo(cx + bodyW / 2, eave);
  ctx.lineTo(cx + bodyW / 2, base);
  ctx.closePath();
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const ctext = (txt, x, y, weight, size, color, maxW, alpha) => {
      if (alpha <= 0.01 || !txt) return;
      let fs = size; ctx.font = disp(weight, fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(weight, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.textAlign = 'center'; ctx.fillStyle = color;
      ctx.fillText(txt, clamp(x, 12 + tw / 2, w - 12 - tw / 2), y);
      ctx.restore();
    };

    const HX = w / 2;
    const m = Math.max(2, spec.askers ?? 3);
    const axs = [];
    for (let i = 0; i < m; i++) axs.push(w * (0.22 + 0.56 * (i / (m - 1))));

    const c0 = cue(spec.pathCue ?? 0, 0.15, 0.75);
    const st = ease(clamp(c0 / 0.12));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    const laneK = ease(clamp((c0 - 0.10) / 0.30));
    const askLab = ease(clamp((c0 - 0.22) / 0.18));
    const hubLab = ease(clamp((c0 - 0.12) / 0.18));
    const houseK = ease(clamp((c0 - 0.44) / 0.34));
    const flow = Math.max(0.4, spec.flowSec ?? 1.3);

    /* ── 길 : 셋이 하나로 모인다 ────────────────────── */
    if (laneK > 0.02) {
      ctx.save();
      ctx.globalAlpha = st * laneK;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.setLineDash([8, 7]);
      ctx.lineDashOffset = (15 - ((t * 21) % 15)) % 15;   // 양수 정규화(결정성)
      for (let i = 0; i < m; i++) {
        ctx.beginPath();
        ctx.moveTo(axs[i], L1_TOP);
        ctx.lineTo(lerp(axs[i], HX, laneK), lerp(L1_TOP, L1_BOT, laneK));
        ctx.stroke();
      }
      ctx.restore();
    }
    if (houseK > 0.02) {
      ctx.save();
      ctx.globalAlpha = st * houseK;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.setLineDash([8, 7]);
      ctx.lineDashOffset = (15 - ((t * 21) % 15)) % 15;
      ctx.beginPath();
      ctx.moveTo(HX, L2_TOP); ctx.lineTo(HX, lerp(L2_TOP, L2_BOT, houseK));
      ctx.stroke();
      ctx.restore();
    }

    /* ── 흐르는 부탁 ────────────────────────────────
       점의 y 는 L1_TOP~L1_BOT 밖으로 절대 안 나간다 — 위아래 흰 도형과의 간격이
       그 상한 하나로 보장된다(반지름 4.5 를 빼도 89~129). */
    if (laneK > 0.5) {
      ctx.save();
      ctx.fillStyle = tone('accent');
      for (let i = 0; i < m; i++) {
        const u = frac(t / flow + i * 0.31);
        const px = lerp(axs[i], HX, u), py = lerp(L1_TOP, L1_BOT, u);
        ctx.globalAlpha = st * laneK * (0.35 + 0.65 * Math.sin(Math.PI * u));
        ctx.beginPath(); ctx.arc(px, py, DOT_R, 0, Math.PI * 2); ctx.fill();
      }
      ctx.restore();
    }
    if (houseK > 0.5) {
      const u = frac(t / flow + 0.5);
      ctx.save();
      ctx.globalAlpha = st * houseK * (0.35 + 0.65 * Math.sin(Math.PI * u));
      ctx.fillStyle = tone('accent');
      ctx.beginPath();
      ctx.arc(HX, lerp(L2_TOP, L2_BOT, u), DOT_R, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();
    }

    /* ── 부탁하는 자리 셋 ──────────────────────────── */
    for (let i = 0; i < m; i++) {
      ctx.save();
      ctx.globalAlpha = st;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, axs[i] - ASK_W / 2, ASK_CY - ASK_H / 2, ASK_W, ASK_H, 7);
      ctx.stroke();
      ctx.restore();
      ctext(spec.askerFace, axs[i], ASK_CY + 5, 800, 12.5, tone('ink'), ASK_W - 10, st);
    }
    ctext(spec.askLabel, w * 0.115, 118, 900, 14, tone('accent'), 70, st * askLab);

    /* ── 가운데 한 칸 ──────────────────────────────── */
    ctx.save();
    ctx.globalAlpha = st;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, HX - HUB_W / 2, HUB_CY - HUB_H / 2, HUB_W, HUB_H, 8);
    ctx.stroke();
    ctx.restore();
    ctext(spec.hubFace, HX, HUB_CY + 5, 800, 14, tone('ink'), HUB_W - 12, st);
    ctext(spec.hubLabel, HX + 72, HUB_CY + 5, 900, 14, tone('accent'), 62, st * hubLab);

    /* ── 그 끝에서만 집이 선다 ─────────────────────── */
    if (houseK > 0.02) {
      const h = HOUSE_H * houseK;
      ctx.save();
      ctx.globalAlpha = st;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      housePath(ctx, HX, HOUSE_BASE, HOUSE_W, h);
      ctx.stroke();

      const bodyW = HOUSE_W * 0.78, bodyH = h * 0.60;
      if (bodyH > 5) {
        ctx.beginPath();
        ctx.rect(HX - bodyW / 2, HOUSE_BASE - bodyH, bodyW, bodyH);
        ctx.clip();
        ctx.lineWidth = 4; ctx.globalAlpha = st * 0.34;
        const S = 15, off = (t * 22) % S;
        for (let x = HX - bodyW / 2 - bodyH - S + off; x < HX + bodyW / 2 + S; x += S) {
          ctx.beginPath();
          ctx.moveTo(x, HOUSE_BASE);
          ctx.lineTo(x + bodyH, HOUSE_BASE - bodyH);
          ctx.stroke();
        }
      }
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
