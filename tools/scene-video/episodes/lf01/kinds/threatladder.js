import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect } from '../../../engine/lib.js';

/* threatladder — 위협이 마을 규모를 밟고 올라서는 계단. 가로축은 규모 하나뿐이고,
   칸이 높아질수록 표적이 밭에서 사람으로 바뀐다.
   규모 표식은 처음부터 끝까지 계속 다시 자란다 — 이 구조는 한 번 일어난 사건이 아니라
   판마다 되풀이되는 사다리다.
   식별자 출처: 클립 카탈로그 detail (Threat_Tier1_Wolf · Threat_Tier2_Pack). */

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;
    const BASE = h - 44, L = 52, R = w - M;

    const k1 = ease(cue(1));
    const k2 = ease(cue(2));
    const k3 = ease(cue(3));

    // ── 축 ───────────────────────────────────────
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(L, BASE); ctx.lineTo(R, BASE); ctx.stroke();
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('VILLAGE SIZE', L, BASE + 18);

    const midX = L + (R - L) * 0.46;

    // ── 계단 두 칸 ────────────────────────────────
    const step = (x0, x1, hgt, k, name, target, accent) => {
      if (k <= 0.02) return;
      const top = BASE - hgt * ease(k);
      ctx.strokeStyle = accent ? tone('accent') : tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(x0, BASE); ctx.lineTo(x0, top); ctx.lineTo(x1, top);
      ctx.stroke();
      ctx.globalAlpha = clamp(k * 1.4 - 0.3);
      ctx.font = disp(800, 14); ctx.fillStyle = accent ? tone('accent') : tone('ink');
      ctx.fillText(name, x0 + 12, top + 22);
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText(target, x0 + 12, top + 38);
      ctx.globalAlpha = 1;
    };
    step(L, midX, 52, k1, 'Threat_Tier1_Wolf', 'TARGET: FARM ONLY', false);
    step(midX, R, 108, k2, 'Threat_Tier2_Pack', 'TARGET: VILLAGERS', true);

    // ── 규모 표식 — 계속 다시 자란다 ────────────────
    const P = 4.2, kk = (t % P) / P;
    const gx = lerp(L, R - 2, ease(Math.min(1, kk * 1.25)));
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(L, BASE - 6); ctx.lineTo(gx, BASE - 6); ctx.stroke();
    ctx.fillStyle = tone('ink');
    ctx.beginPath(); ctx.arc(gx, BASE - 6, 8, 0, Math.PI * 2); ctx.fill();

    // 표식이 둘째 칸에 들어서면 그 칸이 켜진다
    if (k2 > 0.5 && gx > midX) {
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(midX, BASE - 118); ctx.lineTo(Math.min(gx, R - 2), BASE - 118);
      ctx.stroke();
    }

    // ── 판정 ─────────────────────────────────────
    if (k3 > 0.02) {
      const s = '성장이 위협을 부른다';
      ctx.font = disp(900, 20);
      const sw = ctx.measureText(s).width;
      ctx.globalAlpha = clamp(k3);
      ctx.fillStyle = tone('ink');
      ctx.fillText(s, R - sw, 30);
      ctx.globalAlpha = 1;
    }
  },
};
