import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect } from '../../../engine/lib.js';

/* fenceline — 플레이어가 그은 줄이 실물이 되는 자리. 점선이 조각으로 하나씩 앉고,
   가운데 한 칸만 문이다. 주민은 그 문으로만 계속 드나든다 — 이 그림에서 멈추지
   않는 것은 통행이고, 그게 곧 '문으로만 다닌다'의 그림이다.
   출처: Docs/M22_방어건설_실행명세서.md 한 줄 목표. */

const X0 = 66, X1 = 404, Y0 = 62, Y1 = 184;
const GA = 214, GB = 256;           // 문이 뚫린 구간 (위쪽 변)
const SEG = 20, GAP = 5;

function segments() {
  const out = [];
  for (let x = X0; x + SEG <= X1; x += SEG + GAP) {
    if (!(x + SEG < GA || x > GB)) continue;
    out.push({ x, y: Y0, hz: true });
    out.push({ x, y: Y1, hz: true });
  }
  for (let y = Y0 + SEG + GAP; y + SEG <= Y1; y += SEG + GAP) {
    out.push({ x: X0, y, hz: false });
    out.push({ x: X1, y, hz: false });
  }
  return out;
}
const SEGS = segments();

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 18;

    const kDraw = ease(cue(0));
    const kBuilt = ease(cue(1));
    const kUse = ease(cue(2));
    const kDead = ease(cue(3));

    // ── 플레이어가 그은 줄 (점선) ────────────────────
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.rect(X0, Y0, X1 - X0, Y1 - Y0);
    ctx.stroke();
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('DRAWN ZONE', X0, Y0 - 12);

    // ── 실물 조각 ─────────────────────────────────
    const put = Math.round(SEGS.length * clamp(kDraw * 0.25 + kBuilt * 0.75));
    for (let i = 0; i < SEGS.length; i++) {
      if (i >= put) continue;
      const s = SEGS[i];
      ctx.fillStyle = kDead > 0.5 ? tone('sub') : tone('ink');
      if (s.hz) ctx.fillRect(s.x, s.y - 4, SEG, 8);
      else ctx.fillRect(s.x - 4, s.y, 8, SEG);
    }

    // ── 문 ──────────────────────────────────────
    if (kBuilt > 0.4) {
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(GA, Y0 - 14); ctx.lineTo(GA, Y0 + 6);
      ctx.moveTo(GB, Y0 - 14); ctx.lineTo(GB, Y0 + 6);
      ctx.stroke();
      ctx.font = mono(700, 10); ctx.fillStyle = tone('accent');
      ctx.fillText('GATE', GA - 2, Y0 - 20);
    }

    // ── 주민은 문으로만 다닌다 (계속) ────────────────
    if (kUse > 0.1) {
      const P = 3.2, k = (t % P) / P;
      const tri = k < 0.5 ? k * 2 : 2 - k * 2;
      const cxp = (GA + GB) / 2;
      const y = lerp(Y0 - 30, Y1 - 34, ease(tri));
      ctx.fillStyle = tone('accent');
      ctx.beginPath(); ctx.arc(cxp, y, 10, 0, Math.PI * 2); ctx.fill();
    }

    // ── 오른쪽 계기 ───────────────────────────────
    const px = 444, pw = w - M - px;
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('DURABILITY', px, 52);
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, px, 60, pw, 24, 3); ctx.stroke();
    // 위협이 계속 깎는다 (자막 2번째부터)
    // 톱니 = 깎였다가 수리되는 루프. 목수가 죽으면 회복 폭이 줄어든다.
    const worn = kUse > 0.3 ? (t * 0.32) % 1 : 0.05;
    const left = clamp(1 - worn * (kDead > 0.5 ? 0.9 : 0.6));
    ctx.fillStyle = tone('ink');
    ctx.fillRect(px + 4, 64, (pw - 8) * left, 16);

    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('CARPENTER', px, 122);
    ctx.strokeStyle = kDead > 0.5 ? tone('track') : tone('accent'); ctx.lineWidth = 3;
    roundRect(ctx, px, 130, pw, 34, 3); ctx.stroke();
    ctx.font = disp(800, 14);
    ctx.fillStyle = kDead > 0.5 ? tone('sub') : tone('accent');
    ctx.fillText(kDead > 0.5 ? 'DOWN' : 'ALIVE', px + 12, 152);

    if (kDead > 0.5) {
      ctx.font = disp(900, 16); ctx.fillStyle = tone('ink');
      ctx.fillText('방어도 같이 무너진다', px - 6, 196);
    }
  },
};
