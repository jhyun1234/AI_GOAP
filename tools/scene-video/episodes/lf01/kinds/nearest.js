import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect } from '../../../engine/lib.js';

/* nearest — 표적 고르는 규칙이 바뀌면 사고가 이야기가 된다.
   거리 링은 도착 지점에서 쉬지 않고 퍼진다. 규칙이 거리이기 때문에 이 그림의
   주인공은 사람이 아니라 링이다 — 흩어지지 않은 한 명을 링이 먼저 만난다.
   거리 수치는 원문에 없으므로 숫자를 적지 않고 규칙 이름만 바꿔 단다.
   출처: M10 글 42·58행. */

const V = [
  { x: 108, y: 92, stub: true },
  { x: 196, y: 78 },
  { x: 250, y: 150 },
  { x: 320, y: 104 },
  { x: 172, y: 168 },
  { x: 96, y: 160 },
];
const WX = 150, WY = 120;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    const kRand = ease(cue(0));
    const kRule = ease(cue(2));
    const kWhy = ease(cue(3));
    const kFlee = ease(cue(4));
    const kBite = ease(cue(5));

    // ── 밭 ───────────────────────────────────────
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    roundRect(ctx, 56, 46, 330, 150, 4); ctx.stroke();
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('FARM', 56, 38);

    // ── 도착 지점에서 퍼지는 거리 링 ────────────────
    const P = 2.2, rk = (t % P) / P;
    ctx.globalAlpha = clamp(1 - rk);
    ctx.strokeStyle = kRule > 0.4 ? tone('accent') : tone('track');
    ctx.lineWidth = 3;
    // 반지름 상한 108 — 이 이상 키우면 링이 캔버스 아래 가장자리에 닿는다(잘림 검사)
    ctx.beginPath(); ctx.arc(WX, WY, 8 + rk * 100, 0, Math.PI * 2); ctx.stroke();
    ctx.globalAlpha = 1;

    // 늑대 도착점
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(WX - 10, WY - 10); ctx.lineTo(WX + 10, WY + 10);
    ctx.moveTo(WX + 10, WY - 10); ctx.lineTo(WX - 10, WY + 10);
    ctx.stroke();

    // ── 주민 ─────────────────────────────────────
    for (let i = 0; i < V.length; i++) {
      const v = V[i];
      let x = v.x, y = v.y;
      if (!v.stub && kFlee > 0.02) {
        const dx = v.x - WX, dy = v.y - WY;
        const len = Math.max(1, Math.hypot(dx, dy));
        const push = 92 * ease(kFlee);
        x = clamp(v.x + dx / len * push, 30, 400);
        y = clamp(v.y + dy / len * push, 26, h - 26);
      }
      const hit = v.stub && kBite > 0.4;
      ctx.strokeStyle = hit ? tone('accent') : tone('ink');
      ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(x, y, 11, 0, Math.PI * 2); ctx.stroke();
      if (v.stub) {
        ctx.font = disp(800, 12);
        ctx.fillStyle = hit ? tone('accent') : tone('ink');
        const s = kFlee > 0.5 ? '고집쟁이' : '';
        if (s) ctx.fillText(s, x - ctx.measureText(s).width / 2, y - 18);
      }
    }

    // 무작위로 고르던 시절 — 아무 데나 튀는 표식
    if (kRand > 0.05 && kRule < 0.4) {
      const j = Math.floor((t / 0.42) % V.length);
      const v = V[j];
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(v.x, v.y, 18, 0, Math.PI * 2); ctx.stroke();
    }

    // ── 규칙 패널 ────────────────────────────────
    const px = 410, pw = w - M - px;
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('PICK RULE', px, 38);
    ctx.strokeStyle = kRule > 0.4 ? tone('accent') : tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, px, 46, pw, 42, 4); ctx.stroke();
    ctx.font = disp(800, 15);
    ctx.fillStyle = kRule > 0.4 ? tone('accent') : tone('ink');
    ctx.fillText(kRule > 0.4 ? 'NEAREST FIRST' : 'RANDOM', px + 14, 73);

    // ── 이유가 바뀐다 ─────────────────────────────
    if (kWhy > 0.02) {
      ctx.globalAlpha = clamp(kWhy);
      ctx.font = disp(800, 15); ctx.fillStyle = tone('ink');
      ctx.fillText('가까이 있었기 때문', px, 126);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      const ay = 142;
      ctx.beginPath();
      ctx.moveTo(px + 6, ay); ctx.lineTo(px + 6, ay + 18);
      ctx.moveTo(px, ay + 10); ctx.lineTo(px + 6, ay + 18); ctx.lineTo(px + 12, ay + 10);
      ctx.stroke();
      ctx.globalAlpha = clamp((kWhy - 0.4) / 0.5);
      ctx.font = disp(900, 16); ctx.fillStyle = tone('accent');
      ctx.fillText('도망가지 않았기 때문', px, 184);
      ctx.globalAlpha = 1;
    }
  },
};
