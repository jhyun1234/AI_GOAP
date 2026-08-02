import {
  ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, disp,
  setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* splitbin — 한 칸이 두 칸이 되자, 그 사이의 틈이 계획을 끊는다.

   기본 도형의 세 번째 변주. 앞의 두 그림이 '칸이 갈라진다' 였다면 여기서는
   **갈라진 뒤 생긴 간격**이 주인공이다. 합쳐 놓으면 들판에서 뻗은 계획선이
   칸까지 그대로 이어져 '집에 둔 곡식을 들판에서 꺼내 먹는' 계획이 성립해 버리고,
   갈라 놓으면 그 선이 두 칸 사이 이음매에서 끊긴다.

   🔴 화면에 X 를 그리는 것이 이 샷의 결말이다 — 끊기는 것이 고장이 아니라 수정이다.
   그래서 끊기는 자리에만 강조색이 붙고 계획선 자체는 흰색이다.

   계속 도는 것 = 들판과 칸 사이를 오가는 수확 표식 둘. 갈라지기 전에도 후에도
   수확은 계속 일어난다 — 달라지는 건 그 표식이 어디까지 갈 수 있느냐다. */

const WALK = 3.0;                        // 수확 표식이 경로를 한 번 지나가는 초
const BIN_Y = 30, BIN_H = 82;
const MERGE_X = 92, MERGE_W = 168;
const A_X = 12, B_X = 184, PART_W = 156;
const SEAM = 176;                        // 두 칸 사이 이음매
const FIELD = { x: 12, y: 232, w: 98, h: 40 };
const RAIL_Y = 180;                      // 경로가 가로로 꺾이는 높이

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const mk = ease(cue(spec.mergeCue ?? 0, 0.15, 0.5));
    const pk = ease(cue(spec.planCue ?? 1, 0.15, 0.5));
    const bk = ease(cue(spec.badCue ?? 2, 0.15, 0.5));
    const sk = ease(cue(spec.splitCue ?? 3, 0.15, 0.55));
    const parts = spec.parts || [];

    ctx.textBaseline = 'alphabetic';

    const ax = lerp(MERGE_X, A_X, sk), aw = lerp(MERGE_W, PART_W, sk);
    const bx = lerp(MERGE_X, B_X, sk), bw = lerp(MERGE_W, PART_W, sk);
    const cut = sk > 0.5;                 // 이음매가 벌어져 계획선이 끊기는 지점

    /* ── 경로 (항상 있다) ───────────────────────── */
    const target = bx + bw / 2;
    const pts = [
      { x: FIELD.x + FIELD.w / 2, y: FIELD.y },
      { x: FIELD.x + FIELD.w / 2, y: RAIL_Y },
      { x: target, y: RAIL_Y },
      { x: target, y: BIN_Y + BIN_H }
    ];
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.lineJoin = 'round';
    ctx.beginPath();
    pts.forEach((p, i) => i ? ctx.lineTo(p.x, p.y) : ctx.moveTo(p.x, p.y));
    ctx.stroke();

    /* ── 칸(들) ─────────────────────────────────── */
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.globalAlpha = clamp(mk * 2);
    roundRect(ctx, ax, BIN_Y, aw, BIN_H, 5); ctx.stroke();
    if (sk > 0.02) { roundRect(ctx, bx, BIN_Y, bw, BIN_H, 5); ctx.stroke(); }
    ctx.globalAlpha = 1;

    // 칸 안의 곡식 — 갈라져도 총량은 같다
    ctx.fillStyle = tone('accent');
    ctx.globalAlpha = clamp(mk * 2) * 0.9;
    for (let i = 0; i < 6; i++) {
      const col = i % 3, row = (i / 3) | 0;
      const bxx = i < 3 ? ax : bx, bww = i < 3 ? aw : bw;
      const useX = sk > 0.02 ? bxx : ax;
      const useW = sk > 0.02 ? bww : aw;
      const gx = useX + useW / 2 + (col - 1) * 26 - 9;
      const gy = BIN_Y + 34 + row * 24;
      roundRect(ctx, gx, gy, 18, 15, 3); ctx.fill();
    }
    ctx.globalAlpha = 1;

    // 라벨 — 합쳐 있을 땐 하나, 갈라지면 둘
    ctx.textAlign = 'center';
    ctx.font = disp(700, 11);
    ctx.fillStyle = tone('sub');
    ctx.globalAlpha = clamp(1 - sk * 2.2);
    put(ctx, spec.binLabel || '', MERGE_X + MERGE_W / 2, BIN_Y - 9, w);
    ctx.globalAlpha = clamp((sk - 0.4) / 0.35);
    ctx.fillStyle = tone('ink'); ctx.font = disp(800, 11.5);
    put(ctx, parts[0] || '', ax + aw / 2, BIN_Y - 9, w);
    put(ctx, parts[1] || '', bx + bw / 2, BIN_Y - 9, w);
    ctx.globalAlpha = 1;

    /* ── 계획선 ─────────────────────────────────── */
    if (pk > 0.02) {
      const grow = clamp(pk * 1.15);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
      ctx.globalAlpha = clamp(pk * 2);
      ctx.beginPath();
      // 들판 → 위로 → 가로 → 칸 아래. 끊기면 이음매에서 멈춘다.
      const railTo = cut ? SEAM - 12 : lerp(pts[1].x, pts[2].x, grow);
      ctx.moveTo(pts[0].x, pts[0].y);
      ctx.lineTo(pts[1].x, lerp(pts[0].y, pts[1].y, clamp(grow * 3)));
      if (grow > 0.34) { ctx.moveTo(pts[1].x, RAIL_Y); ctx.lineTo(railTo, RAIL_Y); }
      if (!cut && grow > 0.85) {
        ctx.moveTo(target, RAIL_Y);
        ctx.lineTo(target, lerp(RAIL_Y, BIN_Y + BIN_H, clamp((grow - 0.85) / 0.15)));
      }
      ctx.stroke();
      ctx.globalAlpha = 1;

      // 끊긴 자리 — 이 샷의 결말
      if (cut) {
        const ck = clamp((sk - 0.5) * 4);
        ctx.globalAlpha = ck;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
        setShadow(ctx, GLOW, 10, 0);
        ctx.beginPath();
        ctx.moveTo(SEAM - 9, RAIL_Y - 9); ctx.lineTo(SEAM + 9, RAIL_Y + 9);
        ctx.moveTo(SEAM + 9, RAIL_Y - 9); ctx.lineTo(SEAM - 9, RAIL_Y + 9);
        ctx.stroke();
        clearShadow(ctx);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 계속 도는 것 : 수확 표식 ───────────────── */
    for (let i = 0; i < 2; i++) {
      const k = frac(t / WALK + i * 0.5);
      const p = walk(pts, k);
      // 끊긴 뒤에는 이음매를 못 넘는다
      const blocked = cut && p.x > SEAM - 10;
      ctx.globalAlpha = (blocked ? 0 : 1) * Math.sin(Math.PI * clamp(k * 1.1)) * 0.95;
      ctx.fillStyle = tone('accent');
      roundRect(ctx, p.x - 10, p.y - 8, 20, 16, 3); ctx.fill();
    }
    ctx.globalAlpha = 1;

    /* ── 들판 ───────────────────────────────────── */
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, FIELD.x, FIELD.y, FIELD.w, FIELD.h, 4); ctx.stroke();
    ctx.textAlign = 'center';
    ctx.fillStyle = tone('ink'); ctx.font = disp(800, 12);
    put(ctx, spec.field || '', FIELD.x + FIELD.w / 2, FIELD.y + 25, w);

    /* ── 불가능한 계획 문구 ─────────────────────── */
    /* 🔴 문구를 계획선 **아래**(레일 180 과 들판 232 사이)에 둔다. 처음엔 두 칸 사이
       빈 자리(y 120~164)에 놓고 글자 뒤에 검정을 깔았는데, 그 검정이 같은 자리를
       지나는 수확 표식을 가려 버렸다 — 정적 구간 대책을 스스로 지운 셈이다.
       배경으로 덮는 대신 자리를 옮겼다. 들판의 세로 구간(x 61)과 8px 떨어져 있다. */
    if (bk > 0.02) {
      ctx.globalAlpha = clamp(bk * 1.6);
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 9.5);
      put(ctx, spec.planLabel || '', w / 2, 196, w);
      ctx.fillStyle = tone('accent'); ctx.font = disp(800, 12.5);
      setShadow(ctx, GLOW, 10, 0);
      put(ctx, spec.badPlan || '', w / 2, 217, w, 16);
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
    ctx.lineJoin = 'miter';
  }
};

/* 꺾인 경로 위의 0~1 지점 */
function walk(pts, k) {
  let total = 0;
  const seg = [];
  for (let i = 1; i < pts.length; i++) {
    const d = Math.hypot(pts[i].x - pts[i - 1].x, pts[i].y - pts[i - 1].y);
    seg.push(d); total += d;
  }
  let want = k * total;
  for (let i = 0; i < seg.length; i++) {
    if (want <= seg[i] || i === seg.length - 1) {
      const u = seg[i] ? clamp(want / seg[i]) : 0;
      return { x: lerp(pts[i].x, pts[i + 1].x, u), y: lerp(pts[i].y, pts[i + 1].y, u) };
    }
    want -= seg[i];
  }
  return pts[0];
}

function put(ctx, s, cx, y, cw, pad = 12) {
  if (!s) return;
  const tw = ctx.measureText(s).width;
  const x = tw < cw - pad * 2 ? clamp(cx, pad + tw / 2, cw - pad - tw / 2) : cw / 2;
  ctx.fillText(s, x, y);
}
