import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect } from '../../../engine/lib.js';

/* repairrate — 위는 속도, 아래는 갈림길.
   두 수리 막대는 같은 시간에 같이 출발해 계속 다시 자란다. 1:5 는 숫자가 아니라
   '한 번 도는 동안 어디까지 가는가'로 읽혀야 해서 루프로 그린다.
   아래는 같은 자리가 어떻게 없어졌느냐에 따라 갈리는 두 갈래다.
   출처: devlog/sessions/2026-08-09.md [00:30]·[00:51]. */

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 18;

    const kRate = ease(cue(0));
    const kRemove = ease(cue(1));
    const kFork = ease(cue(2));

    // ── 수리 속도 ─────────────────────────────────
    const L = 128, R = w - M - 44, FW = R - L;
    const P = 2.6, k = (t % P) / P;
    const rows = [
      { tag: 'VILLAGER', mult: 0.2, accent: false, n: '1' },
      { tag: 'CARPENTER', mult: 1.0, accent: true, n: '5' },
    ];
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('REPAIR SPEED', M, 30);
    rows.forEach((r, i) => {
      const y = 42 + i * 42;
      ctx.font = mono(700, 11); ctx.fillStyle = tone('sub');
      ctx.fillText(r.tag, M, y + 21);
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, L, y, FW, 28, 3); ctx.stroke();
      const bw = (FW - 6) * Math.min(1, k * r.mult) * clamp(kRate * 1.3);
      if (bw > 2) {
        ctx.fillStyle = r.accent ? tone('accent') : tone('ink');
        ctx.fillRect(L + 3, y + 3, bw, 22);
      }
      if (kRate > 0.4) {
        ctx.font = disp(900, 20);
        ctx.fillStyle = r.accent ? tone('accent') : tone('ink');
        ctx.fillText(r.n, R + 12, y + 22);
      }
    });

    // ── 갈림길 ───────────────────────────────────
    if (kRemove > 0.02) {
      ctx.globalAlpha = clamp(kRemove);
      const nx = M + 76, ny = 172;
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText('A TILE IS GONE', M, 148);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(nx, ny, 16, 0, Math.PI * 2); ctx.stroke();
      ctx.globalAlpha = 1;

      const branch = (yy, label, note, accent, kk) => {
        if (kk <= 0.02) return;
        ctx.globalAlpha = clamp(kk);
        ctx.strokeStyle = accent ? tone('accent') : tone('track'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(nx + 18, ny); ctx.lineTo(nx + 52, yy + 16); ctx.lineTo(nx + 120, yy + 16);
        ctx.stroke();
        ctx.font = disp(800, 14); ctx.fillStyle = accent ? tone('accent') : tone('ink');
        ctx.fillText(label, nx + 130, yy + 12);
        ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
        ctx.fillText(note, nx + 130, yy + 28);
        ctx.globalAlpha = 1;
      };
      branch(146, '위협이 부쉈다', 'BACK TO PLAN - REBUILT', true, clamp(kFork * 1.4));
      branch(196, '플레이어가 철거했다', 'STAYS GONE', false, clamp((kFork - 0.25) / 0.6));
    }
  },
};
