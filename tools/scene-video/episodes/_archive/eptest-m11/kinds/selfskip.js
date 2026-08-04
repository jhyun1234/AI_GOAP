import {
  ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, disp,
  setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* selfskip — 칸 하나만 끝내 비어 있는데, 그 칸을 채울 경로가 자기를 건너뛴다.

   기본 도형의 일곱 번째 변주. 둘레 넷의 집은 전부 채워져 있고 가운데 목수의 집
   자리만 점선으로 비어 있다. 부탁은 둘레에서 가운데로만 흐른다 — 반대 방향이
   없다는 것이 그림의 전부다.

   🔴 원문에 주민 이름이 하나도 없다. 그래서 둘레 넷은 전부 '주민' 이고 얼굴도
   이름도 없다. 이 샷에서 이름을 가진 것은 '목수' 하나뿐이고, 그게 정확히 원문이
   말하는 조건이다('상대가 목수 직업이기를 요구합니다').

   🔴 효과음을 두지 않았다. 스캔이 한 바퀴 돌고 아무도 안 남는 것은 부딪힘이
   아니라 '애초에 없음' 이라, 막힘의 소리와 뜻이 어긋난다.

   계속 도는 것 = 둘레 넷에서 가운데로 흐르는 부탁 표식. 부탁은 계속 들어온다. */

const ASK = 2.6;                         // 부탁 표식이 한 번 들어오는 초
const CX = 176, CY = 158, R_IN = 32;
const SPOT = [[-104, -58], [104, -58], [-104, 58], [104, 58]];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const hk = ease(cue(spec.holeCue ?? 0, 0.15, 0.5));
    const sk = ease(cue(spec.scanCue ?? 1, 0.15, 0.6));
    const nk = ease(cue(spec.noneCue ?? 2, 0.15, 0.5));
    const rules = spec.rules || [];
    const n = Math.min(spec.others || 4, SPOT.length);

    ctx.textBaseline = 'alphabetic';

    /* ── 규칙 두 줄 ─────────────────────────────── */
    ctx.textAlign = 'left';
    ctx.font = disp(700, 10);
    rules.forEach((r, i) => {
      const a = clamp((sk - i * 0.14) * 2.2);
      ctx.globalAlpha = a;
      ctx.fillStyle = tone('accent');
      roundRect(ctx, 14, 10 + i * 18, 4, 11, 2); ctx.fill();
      ctx.fillStyle = tone('sub');
      ctx.fillText(r, 25, 20 + i * 18);
      ctx.globalAlpha = 1;
    });

    /* ── 부탁 경로 ──────────────────────────────── */
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    for (let i = 0; i < n; i++) {
      const [dx, dy] = SPOT[i];
      const d = Math.hypot(dx, dy);
      const ux = dx / d, uy = dy / d;
      ctx.beginPath();
      ctx.moveTo(CX + ux * (d - 22), CY + uy * (d - 22));
      ctx.lineTo(CX + ux * (R_IN + 6), CY + uy * (R_IN + 6));
      ctx.stroke();
    }
    ctx.textAlign = 'center';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 9);
    ctx.fillText(spec.askLabel || '', CX, CY - R_IN - 44);

    // 계속 도는 것 — 부탁 표식이 안쪽으로 들어온다
    ctx.fillStyle = tone('ink');
    for (let i = 0; i < n; i++) {
      const [dx, dy] = SPOT[i];
      const d = Math.hypot(dx, dy);
      const ux = dx / d, uy = dy / d;
      const k = frac(t / ASK + i * 0.25);
      const rr = lerp(d - 22, R_IN + 6, k);
      ctx.globalAlpha = Math.sin(Math.PI * clamp(k * 1.06)) * 0.9;
      roundRect(ctx, CX + ux * rr - 8, CY + uy * rr - 7, 16, 14, 3); ctx.fill();
    }
    ctx.globalAlpha = 1;

    /* ── 둘레 넷 ────────────────────────────────── */
    for (let i = 0; i < n; i++) {
      const [dx, dy] = SPOT[i];
      const cx = CX + dx, cy = CY + dy;
      house(ctx, cx, cy - 22, 1, tone('ink'), false);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(cx, cy, 20, 0, Math.PI * 2); ctx.stroke();
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 9);
      ctx.fillText(spec.otherLabel || '', cx, cy + 34);

      // 스캔이 지나가면 탈락한다 — 목수가 아니기 때문에
      const passed = clamp((sk - 0.15 - i * 0.16) * 5);
      if (passed > 0.02) {
        ctx.globalAlpha = passed;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(cx - 9, cy - 9); ctx.lineTo(cx + 9, cy + 9);
        ctx.moveTo(cx + 9, cy - 9); ctx.lineTo(cx - 9, cy + 9);
        ctx.stroke();
        ctx.globalAlpha = 1;
      }
    }

    /* ── 가운데 : 목수 ──────────────────────────── */
    // 빈 집 자리
    ctx.globalAlpha = clamp(hk * 2);
    house(ctx, CX, CY - R_IN - 6, 1.15, tone('accent'), true);
    ctx.textAlign = 'left';
    ctx.fillStyle = tone('accent'); ctx.font = disp(700, 9.5);
    ctx.fillText(spec.lack || '', CX + 24, CY - R_IN - 16);
    ctx.globalAlpha = 1;

    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
    setShadow(ctx, GLOW, 10, 0);
    ctx.beginPath(); ctx.arc(CX, CY, R_IN, 0, Math.PI * 2); ctx.stroke();
    clearShadow(ctx);
    ctx.textAlign = 'center';
    ctx.fillStyle = tone('accent'); ctx.font = disp(800, 14);
    ctx.fillText(spec.center || '', CX, CY + 5);

    /* ── 스캔 호 : 한 바퀴 돌고 아무도 안 남는다 ── */
    if (sk > 0.02 && sk < 0.999) {
      const a0 = -Math.PI / 2;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.globalAlpha = clamp((1 - sk) * 2.4);
      ctx.beginPath();
      ctx.arc(CX, CY, R_IN + 26, a0, a0 + Math.PI * 2 * clamp(sk * 1.05));
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 남는 상대 ──────────────────────────────── */
    if (nk > 0.02) {
      ctx.globalAlpha = clamp(nk * 1.8);
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('accent'); ctx.font = disp(900, 16);
      setShadow(ctx, GLOW, 10, 0);
      put(ctx, spec.result || '', w / 2, 278, w);
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};

/* 집 — 지붕 삼각 + 몸통. baseY 가 바닥. empty 면 점선(아직 없는 집). */
function house(ctx, cx, baseY, s, col, empty) {
  const bw = 24 * s, bh = 15 * s, rh = 11 * s;
  ctx.strokeStyle = col; ctx.lineWidth = 3;
  if (empty) ctx.setLineDash([5, 4]);
  ctx.beginPath();
  ctx.moveTo(cx - bw / 2, baseY);
  ctx.lineTo(cx - bw / 2, baseY - bh);
  ctx.lineTo(cx, baseY - bh - rh);
  ctx.lineTo(cx + bw / 2, baseY - bh);
  ctx.lineTo(cx + bw / 2, baseY);
  ctx.closePath();
  ctx.stroke();
  ctx.setLineDash([]);
  if (!empty) {
    ctx.fillStyle = col;
    ctx.globalAlpha *= 0.9;
    ctx.fillRect(cx - bw / 2 + 3, baseY - bh + 3, bw - 6, bh - 4);
    ctx.globalAlpha /= 0.9;
  }
}

function put(ctx, s, cx, y, cw, pad = 14) {
  if (!s) return;
  const tw = ctx.measureText(s).width;
  const x = tw < cw - pad * 2 ? clamp(cx, pad + tw / 2, cw - pad - tw / 2) : cw / 2;
  ctx.fillText(s, x, y);
}
