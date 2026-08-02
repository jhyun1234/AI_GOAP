import {
  ease, clamp, lerp,
  fitCanvas, mkCanvas, tone, disp,
  setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* orbits — 칸이 놓이는 자리가 성격마다 다른 반지름이다.

   기본 도형의 아홉 번째 변주. 지금까지 칸은 크기와 개수와 여유였는데, 여기서는
   **어디에 놓이느냐**다. 원문이 준 양 끝 두 값(순둥이 0.1 · 떠돌이 0.95)을 그대로
   반지름으로 옮겼다 — 열 배 차이는 설명하지 않고 그냥 보이게 한다.

   🔴 ep00i rings 와 다른 그림이다. 저기는 겹쳐 도는 고리 여럿으로 '주기' 를 그리고
   값이 각속도로 실물화된다. 여기는 고리가 돌지 않고 **반지름 길이 자체가 값**이며,
   도는 것은 고리가 아니라 그 거리 대역 위의 후보 자리들이다(원문: "그 거리 근처의
   후보들 중에서 고르게 했습니다").

   🔴 성격은 여섯 종인데 원문이 이름을 준 것은 양 끝 둘뿐이라 둘만 그린다.

   계속 도는 것 = 두 고리 위 후보 눈금 열여덟 개의 회전. */

const CX = 176, CY = 152, R = 130;
const SPIN = 0.2;                        // 후보 눈금이 도는 각속도(rad/s)

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const rk = ease(cue(spec.ringCue ?? 0, 0.15, 0.5));
    const fk = ease(cue(spec.farCue ?? 1, 0.15, 0.75));
    const near = spec.near || {}, far = spec.far || {};
    const rn = (near.r || 0) * R, rf = (far.r || 0) * R;
    const landed = fk > 0.9;             // 🔑 이 샷의 이산 분기

    ctx.textBaseline = 'alphabetic';

    /* ── 범례 ───────────────────────────────────── */
    ctx.textAlign = 'left';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 9.5);
    ctx.globalAlpha = clamp(rk * 2);
    ctx.fillText(spec.unit || '', 14, 18);
    ctx.globalAlpha = 1;

    /* ── 두 고리 ────────────────────────────────── */
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    [[rn, clamp(rk * 2)], [rf, clamp(rk * 1.4)]].forEach(([r, a]) => {
      if (r < 2) return;
      ctx.globalAlpha = a;
      ctx.beginPath(); ctx.arc(CX, CY, r, 0, Math.PI * 2); ctx.stroke();
    });
    ctx.globalAlpha = 1;

    /* ── 계속 도는 것 : 그 거리 대역의 후보 자리 ── */
    const phase = t * SPIN;
    ctx.strokeStyle = tone('sub'); ctx.lineWidth = 4;
    ctx.globalAlpha = clamp(rk * 2) * 0.75;
    tickRing(ctx, rn, 6, 8, phase * 2.1);
    tickRing(ctx, rf, 12, 14, -phase);
    ctx.globalAlpha = 1;

    /* ── 중심 ───────────────────────────────────── */
    ctx.fillStyle = tone('ink');
    ctx.beginPath(); ctx.arc(CX, CY, 4.5, 0, Math.PI * 2); ctx.fill();
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(CX, CY + 8); ctx.lineTo(CX, 192); ctx.stroke();
    ctx.textAlign = 'center';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 9.5);
    put(ctx, spec.centerLabel || '', CX, 204, w);

    /* ── 반지름 자 : 중심에서 바깥까지 ──────────── */
    if (fk > 0.02) {
      ctx.globalAlpha = clamp(fk * 2);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(CX, CY); ctx.lineTo(CX + rf * clamp(fk * 1.1), CY);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 두 집 ──────────────────────────────────── */
    // 순둥이 — 고리에 붙어 산다
    if (rk > 0.02) {
      ctx.globalAlpha = clamp(rk * 2);
      house(ctx, CX + rn, CY + 13, 0.82, tone('ink'));
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('ink'); ctx.font = disp(800, 11.5);
      put(ctx, `${near.name || ''} ${fmt(near.r)}`, CX + rn, 186, w);
      ctx.globalAlpha = 1;
    }
    // 떠돌이 — 반지름을 타고 밖으로 나가 경계 근처에 앉는다
    if (fk > 0.02) {
      const x = CX + rf * clamp(fk * 1.1);
      const col = landed ? tone('accent') : tone('ink');
      ctx.globalAlpha = clamp(fk * 2);
      if (landed) setShadow(ctx, GLOW, 12, 0);
      house(ctx, x, CY + 13, 0.92, col);
      clearShadow(ctx);
      ctx.textAlign = 'center';
      ctx.fillStyle = col; ctx.font = disp(800, 11.5);
      put(ctx, `${far.name || ''} ${fmt(far.r)}`, x, 186, w);
      ctx.globalAlpha = 1;
    }

    /* ── 열 배 ──────────────────────────────────── */
    if (landed) {
      ctx.globalAlpha = clamp((fk - 0.9) * 10);
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('accent'); ctx.font = disp(900, 15);
      put(ctx, spec.gapNote || '', CX + rf / 2, 232, w);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};

function tickRing(ctx, r, n, len, ang0) {
  if (r < 2) return;
  for (let i = 0; i < n; i++) {
    const a = ang0 + (i / n) * Math.PI * 2;
    const c = Math.cos(a), s = Math.sin(a);
    ctx.beginPath();
    ctx.moveTo(CX + c * (r - len / 2), CY + s * (r - len / 2));
    ctx.lineTo(CX + c * (r + len / 2), CY + s * (r + len / 2));
    ctx.stroke();
  }
}

function house(ctx, cx, baseY, s, col) {
  const bw = 24 * s, bh = 15 * s, rh = 11 * s;
  ctx.strokeStyle = col; ctx.lineWidth = 3;
  ctx.beginPath();
  ctx.moveTo(cx - bw / 2, baseY);
  ctx.lineTo(cx - bw / 2, baseY - bh);
  ctx.lineTo(cx, baseY - bh - rh);
  ctx.lineTo(cx + bw / 2, baseY - bh);
  ctx.lineTo(cx + bw / 2, baseY);
  ctx.closePath(); ctx.stroke();
  ctx.fillStyle = col;
  ctx.globalAlpha *= 0.85;
  ctx.fillRect(cx - bw / 2 + 3, baseY - bh + 3, bw - 6, bh - 4);
  ctx.globalAlpha /= 0.85;
}

/* 0.1 · 0.95 를 원문 표기 그대로 찍는다(0.10 이 되면 안 된다) */
function fmt(v) { return String(v ?? ''); }

function put(ctx, s, cx, y, cw, pad = 12) {
  if (!s) return;
  const tw = ctx.measureText(s).width;
  const x = tw < cw - pad * 2 ? clamp(cx, pad + tw / 2, cw - pad - tw / 2) : cw / 2;
  ctx.fillText(s, x, y);
}
