import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect } from '../../../engine/lib.js';

/* siege — 이 회차에서 가장 큰 반전. 벽을 세우면 위협이 벽을 부수는 게 아니라
   **아예 안 나타난다.** 그래서 이 그림의 주인공은 벽이 아니라 스폰 자리다 —
   출몰 시도가 계속 반복되고, 어떤 국면에서는 그 자리에서 그대로 취소된다.
   출처: Docs/M22_방어건설_실행명세서.md 1절 2항. */

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 18;
    const SX = 62, CY = 116, WALLX = 342;
    const VX = 420, VW = w - M - VX, VY = 58, VH = 116;

    const kWall = ease(cue(1));
    const kSkip = ease(cue(2));
    const kInv = ease(cue(3));
    const kSiege = ease(cue(4));

    const phase = kSiege > 0.35 ? 2 : (kSkip > 0.35 ? 1 : 0);

    // ── 마을 ─────────────────────────────────────
    ctx.strokeStyle = kInv > 0.4 && phase === 1 ? tone('accent') : tone('ink');
    ctx.lineWidth = 3;
    roundRect(ctx, VX, VY, VW, VH, 4); ctx.stroke();
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('VILLAGE', VX, VY - 12);

    // ── 경로 ─────────────────────────────────────
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(SX + 24, CY); ctx.lineTo(VX, CY); ctx.stroke();

    // ── 스폰 자리 ─────────────────────────────────
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.arc(SX, CY, 22, 0, Math.PI * 2); ctx.stroke();
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('SPAWN', SX - 20, CY + 44);

    // ── 벽 ──────────────────────────────────────
    if (kWall > 0.05) {
      const kw = clamp(kWall);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      const wh = 74 * ease(kw);
      ctx.beginPath();
      ctx.moveTo(WALLX, CY - wh / 2); ctx.lineTo(WALLX, CY + wh / 2);
      ctx.stroke();
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText('FENCE', WALLX - 16, CY - 48);
    }

    // ── 출몰 시도 — 계속 반복된다 ───────────────────
    const P = 2.4, k = (t % P) / P;
    if (phase === 0) {
      const goal = kWall > 0.5 ? WALLX - 16 : VX + 20;
      const x = lerp(SX, goal, ease(k));
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(x, CY, 11, 0, Math.PI * 2); ctx.fill();
    } else if (phase === 1) {
      // 경로가 막혔으므로 그 자리에서 취소된다
      if (k < 0.42) {
        ctx.globalAlpha = clamp(1 - k / 0.42);
        ctx.fillStyle = tone('ink');
        ctx.beginPath(); ctx.arc(SX, CY, 11 + k * 18, 0, Math.PI * 2); ctx.fill();
        ctx.globalAlpha = 1;
      }
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(SX - 15, CY - 15); ctx.lineTo(SX + 15, CY + 15);
      ctx.moveTo(SX + 15, CY - 15); ctx.lineTo(SX - 15, CY + 15);
      ctx.stroke();
      ctx.font = mono(700, 11); ctx.fillStyle = tone('accent');
      ctx.fillText('SPAWN SKIPPED', SX - 22, CY - 40);
    } else {
      // 공성 — 벽까지 와서 두드린다
      const x = k < 0.7 ? lerp(SX, WALLX - 16, ease(k / 0.7)) : WALLX - 16;
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(x, CY, 11, 0, Math.PI * 2); ctx.fill();
      if (k >= 0.7) {
        const hit = (k - 0.7) / 0.3;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.arc(WALLX, CY, 14 + hit * 22, -0.9, 0.9);
        ctx.stroke();
      }
    }

    // ── 무적 도장 ────────────────────────────────
    if (kInv > 0.05 && phase === 1) {
      const s = '완전 무적';
      ctx.font = disp(900, 26);
      const sw = ctx.measureText(s).width;
      ctx.globalAlpha = clamp(kInv);
      ctx.fillStyle = tone('accent');
      ctx.fillText(s, VX + VW / 2 - sw / 2, VY + VH / 2 + 9);
      ctx.globalAlpha = 1;
    }
    if (phase === 2) {
      ctx.font = disp(900, 20); ctx.fillStyle = tone('accent');
      const s = '공성 추가';
      ctx.fillText(s, VX + 16, VY + VH / 2 + 7);
    }

    // ── 아래 설명 띠 ──────────────────────────────
    ctx.font = mono(700, 11); ctx.fillStyle = tone('sub');
    const note = phase === 2 ? 'BLOCKED -> BREAK IT' : (phase === 1 ? 'BLOCKED -> SKIP THE WHOLE SPAWN' : 'PATH OPEN -> ARRIVES');
    ctx.fillText(note, M, h - 16);
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(M, h - 34); ctx.lineTo(w - M, h - 34); ctx.stroke();
  },
};
