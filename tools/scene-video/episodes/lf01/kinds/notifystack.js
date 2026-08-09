import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect } from '../../../engine/lib.js';

/* notifystack — 알림 칸이 하나뿐이라 일곱 건이 서로를 지운 자리.
   칸에 어떤 문장이 떠 있었는지는 원문에 없으므로 번호만 쓴다(지어내지 않는다).
   사건 카드가 차례로 칸을 점거하는 순환은 자막과 무관하게 계속 돈다 —
   덮어쓰기는 한 번 일어난 사건이 아니라 그때 계속 돌던 구조였다.
   출처: M13 글 11행. */

const N = 7;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16, FW = w - M * 2;

    const kDeaths = ease(cue(0));
    const kSlot = ease(cue(1));
    const kOver = ease(cue(2));

    // ── 하나뿐인 알림 칸 ───────────────────────────
    const by = 40, bh = 44;
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('ALERT SLOT  (capacity 1)', M, by - 10);
    ctx.strokeStyle = kSlot > 0.3 ? tone('ink') : tone('track');
    ctx.lineWidth = 3;
    roundRect(ctx, M, by, FW, bh, 4); ctx.stroke();

    // ── 사건 카드 7장 ─────────────────────────────
    const cw = (FW - (N - 1) * 8) / N, cy = 124, ch = 62;
    const shown = kDeaths * N;
    const P = 0.62;
    // 🔴 순환은 「한 줄만 떠 있었다」를 부르는 자막(cue 1)부터 돈다.
    //    cue(2) 에 걸면 그 앞 자막 구간 약 3.5초가 통째로 멈춘다(정적 검사 문턱 초과).
    const active = kSlot > 0.35 ? Math.floor((t / P) % N) : -1;
    const frac = (t / P) % 1;

    for (let i = 0; i < N; i++) {
      const k = clamp(shown - i);
      if (k <= 0.02) continue;
      const x = M + i * (cw + 8);
      const live = i === active;
      ctx.globalAlpha = k;
      ctx.strokeStyle = live ? tone('accent') : (kOver > 0.5 ? tone('track') : tone('ink'));
      ctx.lineWidth = 3;
      roundRect(ctx, x, cy, cw, ch, 4); ctx.stroke();
      ctx.font = disp(900, 20);
      ctx.fillStyle = live ? tone('accent') : (kOver > 0.5 ? tone('sub') : tone('ink'));
      const s = '#' + (i + 1);
      const sw = ctx.measureText(s).width;
      ctx.fillText(s, x + cw / 2 - sw / 2, cy + 30);
      ctx.font = mono(700, 9);
      ctx.fillStyle = tone('sub');
      const t2 = 'DEATH';
      ctx.fillText(t2, x + cw / 2 - ctx.measureText(t2).width / 2, cy + 48);
      ctx.globalAlpha = 1;
    }

    // ── 칸을 차지하러 올라가는 토큰 ──────────────────
    if (active >= 0) {
      const x = M + active * (cw + 8) + cw / 2;
      const y = lerp(cy - 6, by + bh + 6, ease(clamp(frac * 1.6)));
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(x, cy - 6); ctx.lineTo(x, by + bh + 6); ctx.stroke();
      ctx.fillStyle = tone('accent');
      ctx.beginPath(); ctx.arc(x, y, 8, 0, Math.PI * 2); ctx.fill();

      // 칸 안에는 언제나 하나뿐
      ctx.font = disp(900, 22); ctx.fillStyle = tone('accent');
      const s = '#' + (active + 1);
      ctx.fillText(s, M + 16, by + 31);
      if (kOver > 0.25) {
        ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
        const ov = 'OVERWRITTEN 6 / 7';
        ctx.fillText(ov, w - M - 16 - ctx.measureText(ov).width, by + 28);
      }
    } else if (kSlot > 0.3) {
      ctx.font = disp(900, 22); ctx.fillStyle = tone('ink');
      ctx.fillText('#7', M + 16, by + 31);
    }

    // ── Day 45 ────────────────────────────────────
    ctx.font = mono(700, 11); ctx.fillStyle = tone('sub');
    ctx.fillText('Day 45', M, h - 12);
    if (kDeaths > 0.4) {
      ctx.font = disp(900, 16); ctx.fillStyle = tone('ink');
      ctx.fillText('7 deaths', M + 62, h - 10);
    }
  },
};
