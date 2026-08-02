import {
  ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, disp,
  setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* hollow — 이름이 지워진 칸을 두 코드가 아직도 조회한다.

   기본 도형의 다섯 번째 변주. 여기서 칸은 갈라지지도 막히지도 않는다.
   그냥 **비었는데 아무도 모른다.** 선은 멀쩡히 이어져 있고 조회 표식은 끝까지
   흘러가서 거기서 사라진다 — 에러도 끊김도 없다.

   🔴 ep02s orphan 과 반대 도형이다. orphan 은 두 층 사이에 닿는 선이 아예 없어서
   '연결 없음' 이 그림의 주장이었다. 여기는 연결이 완벽하고 연결된 곳이 없어졌다.
   그래서 점선은 경로가 아니라 **칸의 테두리**에만 쓴다(휴면 표시).

   🔴 이 샷에는 효과음을 두지 않았다. '조용히 죽어 있었다' 가 자막인데 소리가
   나면 자기모순이다. 화면도 같은 규칙을 지킨다 — 아무것도 터지지 않는다.

   계속 도는 것 = 두 조회 표식과 빈 칸을 훑는 띠. 둘 다 결과가 없다. */

const QUERY = 2.2;                       // 조회 표식이 한 번 내려가는 초
const SCAN = 2.9;                        // 빈 칸을 한 번 훑는 초
const CARD_Y = 12, CARD_H = 44;
const L = { x: 20, w: 146 }, R = { x: 186, w: 146 };
const BOX = { x: 108, y: 114, w: 136, h: 66 };

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const lk = ease(cue(spec.lookCue ?? 0, 0.15, 0.5));
    const sk = ease(cue(spec.storeCue ?? 1, 0.15, 0.5));
    const rk = ease(cue(spec.resultCue ?? 2, 0.15, 0.5));
    const res = spec.results || [];

    ctx.textBaseline = 'alphabetic';
    ctx.lineJoin = 'round';

    /* ── 두 카드 ────────────────────────────────── */
    [[L, spec.judge], [R, spec.pay]].forEach(([P, label], i) => {
      const a = clamp((lk - i * 0.12) * 2.2);
      ctx.globalAlpha = a;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, P.x, CARD_Y, P.w, CARD_H, 4); ctx.stroke();
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('ink'); ctx.font = disp(800, 13);
      put(ctx, label || '', P.x + P.w / 2, CARD_Y + 28, w);
      ctx.globalAlpha = 1;
    });

    /* ── 두 조회 경로 ───────────────────────────── */
    const paths = [L, R].map(P => ([
      { x: P.x + P.w / 2, y: CARD_Y + CARD_H },
      { x: P.x + P.w / 2, y: 88 },
      { x: BOX.x + BOX.w / 2, y: 88 },
      { x: BOX.x + BOX.w / 2, y: BOX.y }
    ]));

    /* 🔴 이 경로는 '끊기지 않았다' 는 것이 주장이라 확실히 보여야 한다.
       track(알파 0.12)이면 선이 있는지 없는지가 안 보여 orphan 과 구분이 안 된다. */
    ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
    ctx.globalAlpha = clamp(lk * 2) * 0.5;
    paths.forEach(p => {
      ctx.beginPath();
      p.forEach((q, i) => i ? ctx.lineTo(q.x, q.y) : ctx.moveTo(q.x, q.y));
      ctx.stroke();
    });
    // 화살촉 — 칸 위에 닿는다
    ctx.fillStyle = tone('sub');
    ctx.beginPath();
    ctx.moveTo(BOX.x + BOX.w / 2, BOX.y);
    ctx.lineTo(BOX.x + BOX.w / 2 - 7, BOX.y - 11);
    ctx.lineTo(BOX.x + BOX.w / 2 + 7, BOX.y - 11);
    ctx.closePath(); ctx.fill();
    ctx.globalAlpha = 1;

    // 계속 도는 것 — 조회가 끝까지 내려가서 아무것도 없이 사라진다
    ctx.fillStyle = tone('ink');
    paths.forEach((p, i) => {
      const k = frac(t / QUERY + i * 0.5);
      const q = walk(p, k);
      ctx.globalAlpha = clamp(lk * 2) * Math.sin(Math.PI * clamp(k * 1.06)) * 0.9;
      roundRect(ctx, q.x - 9, q.y - 7, 18, 14, 3); ctx.fill();
    });
    ctx.globalAlpha = 1;

    /* ── 텅 빈 휴면 칸 ──────────────────────────── */
    ctx.globalAlpha = clamp(sk * 2);
    ctx.setLineDash([7, 6]);
    ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
    roundRect(ctx, BOX.x, BOX.y, BOX.w, BOX.h, 5); ctx.stroke();
    ctx.setLineDash([]);

    // 빈 칸을 훑는 띠 — 훑어도 나오는 게 없다
    {
      const u = frac(t / SCAN);
      const pp = u < 0.5 ? u * 2 : (1 - u) * 2;
      const bx = lerp(BOX.x + 5, BOX.x + BOX.w - 17, ease(pp));
      ctx.globalAlpha = clamp(sk * 2) * 0.5;
      ctx.fillStyle = tone('sub');
      roundRect(ctx, bx, BOX.y + 5, 12, BOX.h - 10, 3); ctx.fill();
    }

    ctx.globalAlpha = clamp(sk * 2);
    ctx.textAlign = 'center';
    ctx.fillStyle = tone('ink'); ctx.font = disp(900, 34);
    put(ctx, spec.balance || '', BOX.x + BOX.w / 2, BOX.y + 46, w);
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
    put(ctx, spec.store || '', BOX.x + BOX.w / 2, BOX.y + BOX.h + 16, w);
    ctx.globalAlpha = 1;

    /* ── 결과 두 줄 ─────────────────────────────── */
    res.forEach((r, i) => {
      const y = 212 + i * 34;
      const a = clamp((rk - i * 0.16) * 2.2);
      if (a <= 0.02) return;
      ctx.globalAlpha = a;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, 20, y, w - 40, 28, 3); ctx.stroke();
      ctx.textAlign = 'left';
      ctx.fillStyle = tone('ink'); ctx.font = disp(800, 12);
      ctx.fillText(r.who || '', 34, y + 19);
      ctx.textAlign = 'right';
      ctx.fillStyle = tone('accent'); ctx.font = disp(800, 12);
      right(ctx, r.what || '', w - 34, y + 19, w);
      ctx.globalAlpha = 1;
    });

    ctx.textAlign = 'left';
    ctx.lineJoin = 'miter';
  }
};

function walk(pts, k) {
  const seg = []; let total = 0;
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
function right(ctx, s, x, y, cw, pad = 12) {
  if (!s) return;
  const tw = ctx.measureText(s).width;
  ctx.fillText(s, tw < cw - pad * 2 ? clamp(x, pad + tw, cw - pad) : cw - pad, y);
}
