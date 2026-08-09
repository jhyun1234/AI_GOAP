import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect } from '../../../engine/lib.js';

/* bondcut — 관계가 양방향으로 지워지는 자리. 왼쪽이 사라질 때 오른쪽 기억에서도
   같은 줄이 사라진다. 정리 함수 이름은 원문에 없으므로 쓰지 않고 CLEANUP 으로 둔다.
   관계가 살아 있는 동안에는 신호가 두 사람 사이를 왕복하고, 지워진 뒤에는
   빈 기억 칸을 스캔 밴드가 왕복한다 — 찾는데 없다는 것이 이 마디의 뜻이다.
   출처: M13 글 17행. */

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    const kPair = ease(cue(0));
    const kCut = ease(cue(1));
    const kGone = ease(cue(2));

    const ax = 106, bx = 300, cy = 96, R = 32;

    // ── 관계선 ───────────────────────────────────
    const alive = 1 - clamp(kGone);
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath();
    const half = (bx - ax) / 2;
    ctx.moveTo(ax + R + 4 + half * (1 - alive) * 0.9, cy);
    ctx.lineTo(bx - R - 4 - half * (1 - alive) * 0.9, cy);
    ctx.stroke();

    // 살아 있는 동안 신호가 왕복한다
    if (kPair > 0.3 && kGone < 0.6) {
      const P = 1.8, k = (t % P) / P;
      const tri = k < 0.5 ? k * 2 : 2 - k * 2;
      const px = lerp(ax + R + 8, bx - R - 8, ease(tri));
      ctx.fillStyle = tone('accent');
      ctx.beginPath(); ctx.arc(px, cy, 7, 0, Math.PI * 2); ctx.fill();
    }

    // ── 두 사람 ──────────────────────────────────
    const person = (x, label, alpha, accent) => {
      ctx.globalAlpha = clamp(alpha);
      ctx.strokeStyle = accent ? tone('accent') : tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(x, cy, R, 0, Math.PI * 2); ctx.stroke();
      ctx.font = disp(800, 14); ctx.fillStyle = accent ? tone('accent') : tone('ink');
      const fw = ctx.measureText(label).width;
      ctx.fillText(label, x - fw / 2, cy + R + 26);
      ctx.globalAlpha = 1;
    };
    person(ax, '단짝', kPair * (1 - clamp(kGone * 1.15)), false);
    person(bx, '남은 사람', clamp(0.2 + 0.8 * kPair), kGone > 0.5);

    // ── 정리 함수 ─────────────────────────────────
    if (kCut > 0.05) {
      ctx.globalAlpha = clamp(kCut);
      ctx.font = mono(700, 11); ctx.fillStyle = tone('sub');
      ctx.fillText('CLEANUP()  - both directions', M, h - 16);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      const y = h - 34;
      ctx.beginPath();
      ctx.moveTo(ax + 20, y); ctx.lineTo(ax + 4, y);
      ctx.moveTo(bx - 20, y); ctx.lineTo(bx - 4, y);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    // ── 남은 사람의 기억 ───────────────────────────
    const mx = 408, mw = w - M - mx, my = 34, mh = h - 34 - 44;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, mx, my, mw, mh, 4); ctx.stroke();
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('MEMORY OF THE SURVIVOR', mx, my - 10);

    const slots = ['일한 기억', '단짝', '겨울 기억'];
    for (let i = 0; i < 3; i++) {
      const sy = my + 16 + i * 40;
      const wiped = i === 1 && kGone > 0.4;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, mx + 14, sy, mw - 28, 30, 3); ctx.stroke();
      if (!wiped) {
        ctx.font = disp(800, 14); ctx.fillStyle = tone('ink');
        ctx.fillText(slots[i], mx + 26, sy + 21);
      }
    }

    // 지워진 칸을 스캔 밴드가 왕복한다
    if (kGone > 0.5) {
      const sy = my + 16 + 40;
      const P = 2.2, k = (t % P) / P;
      const tri = k < 0.5 ? k * 2 : 2 - k * 2;
      const bxx = lerp(mx + 17, mx + mw - 17 - 48, ease(tri));
      ctx.fillStyle = tone('track');
      ctx.fillRect(bxx, sy + 3, 48, 24);
    }
  },
};
