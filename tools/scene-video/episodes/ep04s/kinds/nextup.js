import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* nextup — 막은 그대로고, 그 위에 칸이 하나 더 얹힌다.

   이번 편 도입에서 막이 바뀌었으므로 예고에서는 막을 바꾸지 않는다. 맨 위 띠는 처음부터
   끝까지 '3막 · 다시 짓기' 하나이고 자라지도 끝나지도 않는다(sameActCue). 대신 그 아래에
   이번 편 칸 위로 다음 편 칸이 내려와 얹힌다(stackCue) — 구성안이 3막을 두고 적어 둔
   '한 편에 기능 하나씩 쌓이는 구조'를 예고의 형태 자체로 쓴다.

   🔴 지난 편의 예고는 막의 가로 띠가 오른쪽으로 자라는 그림이었다. 여기서 띠는 못 박혀
   있고 움직이는 것은 그 아래에 쌓이는 칸이다. 같은 재료를 반대로 쓴 셈이다.

   아래 두 줄은 다음 편의 내용이다 — 밭에서 부엌으로 이어지는 생산 체인(chainCue)과,
   모닥불이 식당으로 올라가는 앵커 승격(mealCue). 예고가 '다음 편도 기대해 주세요'가 되지
   않으려면 무엇이 오는지 화면이 이름으로 말해야 한다.

   계속 도는 것 = 3막 띠 안을 오른쪽으로 계속 지나가는 표식(막이 끝나지 않는다는 뜻)과,
   다음 편 칸에서 피어오르는 김 세 줄기. */

const RUN = 4.4;
const STEAM = 2.6;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, scene, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));

    const sk = ease(cue(spec.sameActCue ?? 0, 0.15, 0.6));
    const tk = ease(cue(spec.stackCue ?? 1, 0.15, 0.55));
    const ck = ease(cue(spec.chainCue ?? 2, 0.15, 0.6));
    const mk = ease(cue(spec.mealCue ?? 3, 0.15, 0.55));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    /* ── 3막 띠 — 바뀌지 않는다 ───────────────────── */
    {
      const bx = 8, by = 12, bw = w - 16, bh = 30;
      ctx.globalAlpha = clamp(sk * 2);
      ctx.lineWidth = 4; ctx.strokeStyle = tone('accent');
      setShadow(ctx, GLOW, 12, 0);
      roundRect(ctx, bx, by, bw, bh, 3); ctx.stroke();
      clearShadow(ctx);
      ctx.textAlign = 'left';
      const s = spec.act || '';
      const fs = fit(s, 900, 15, bw - 90, 10);
      ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
      ctx.fillText(s, bx + 12, by + 21);

      if (spec.actNote) {
        ctx.textAlign = 'right';
        ctx.font = disp(800, 11); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.actNote, bx + bw - 12, by + 21);
      }

      /* 계속 도는 것 — 띠 안을 지나가는 표식 */
      ctx.save();
      ctx.beginPath(); ctx.rect(bx + 3, by + 3, bw - 6, bh - 6); ctx.clip();
      for (let i = 0; i < 3; i++) {
        const u = frac(t / RUN + i / 3);
        const x = lerp(bx, bx + bw, u);
        ctx.globalAlpha = clamp(sk * 2) * 0.28 * Math.sin(Math.PI * u);
        ctx.fillStyle = tone('accent');
        ctx.fillRect(x - 9, by + 3, 18, bh - 6);
      }
      ctx.restore();
      ctx.globalAlpha = 1;
    }

    /* ── 이번 편 칸 ───────────────────────────────── */
    {
      const bx = 8, by = 54, bw = w - 16, bh = 32;
      ctx.globalAlpha = clamp(sk * 2) * 0.6;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('sub');
      roundRect(ctx, bx, by, bw, bh, 3); ctx.stroke();
      ctx.textAlign = 'left';
      const s = spec.thisEp || '';
      const fs = fit(s, 800, 12.5, bw - 44, 9);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(s, bx + 12, by + 21);
      // 끝난 칸
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      const mx = bx + bw - 24, my = by + bh / 2;
      ctx.beginPath();
      ctx.moveTo(mx - 5, my); ctx.lineTo(mx - 1, my + 4); ctx.lineTo(mx + 6, my - 5);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 다음 편 칸이 얹힌다 ──────────────────────── */
    const nbY = 98, nbH = 54;
    if (tk > 0.02) {
      const bx = 8, bw = w - 16;
      const by = lerp(nbY - 24, nbY, ease(clamp(tk / 0.85)));
      const s = Math.min(1, spring(tk));
      ctx.globalAlpha = clamp(tk * 1.8);
      setShadow(ctx, GLOW, 16, 0);
      ctx.lineWidth = 4; ctx.strokeStyle = tone('accent');
      roundRect(ctx, bx + (bw - bw * s) / 2, by + (nbH - nbH * s) / 2, bw * s, nbH * s, 5);
      ctx.stroke();
      clearShadow(ctx);

      ctx.textAlign = 'left';
      ctx.font = disp(800, 9.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.nextLabel || '다음 편', bx + 12, by + 17);

      ctx.textAlign = 'center';
      const s1 = spec.topic || '';
      const fs = fit(s1, 900, 18, bw - 26, 11);
      ctx.font = disp(900, fs * s); ctx.fillStyle = tone('accent');
      ctx.fillText(s1, bx + bw / 2, by + 41);
      ctx.globalAlpha = 1;

      /* 계속 도는 것 — 다음 편 칸에서 피어오르는 김 */
      if (mk > 0.25) {
        const k = clamp((mk - 0.25) / 0.5);
        for (let i = 0; i < 3; i++) {
          const u = frac(t / STEAM + i / 3);
          const sx = bx + bw - 40 + i * 11;
          const sy = lerp(by + 6, by - 16, u);
          ctx.globalAlpha = k * 0.8 * Math.sin(Math.PI * u);
          ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
          ctx.beginPath();
          ctx.moveTo(sx, sy);
          ctx.quadraticCurveTo(sx + 4, sy - 5, sx, sy - 10);
          ctx.stroke();
          ctx.globalAlpha = 1;
        }
      }
    }

    /* ── 생산 체인 ────────────────────────────────── */
    const chain = spec.chain || [];
    if (ck > 0.02 && chain.length) {
      const cw = 90, gap = 24;
      const total = chain.length * cw + (chain.length - 1) * gap;
      const startX = (w - total) / 2;
      const y = 168, bh = 32;

      chain.forEach((label, i) => {
        const born = clamp(ck * (chain.length + 1) - i);
        if (born < 0.02) return;
        const x = startX + i * (cw + gap);
        ctx.globalAlpha = clamp(born * 1.6);
        ctx.lineWidth = 3;
        ctx.strokeStyle = i === chain.length - 1 ? tone('accent') : tone('ink');
        roundRect(ctx, x, y, cw, bh, 3); ctx.stroke();
        ctx.textAlign = 'center';
        const fs = fit(label, 800, 13, cw - 12, 9);
        ctx.font = disp(800, fs);
        ctx.fillStyle = i === chain.length - 1 ? tone('accent') : tone('ink');
        ctx.fillText(label, x + cw / 2, y + 21);
        ctx.globalAlpha = 1;

        if (i < chain.length - 1) {
          const ak = clamp(ck * (chain.length + 1) - i - 0.5);
          if (ak > 0.02) {
            const ax = x + cw + 4, ay = y + bh / 2;
            ctx.globalAlpha = clamp(ak * 1.6);
            ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
            ctx.beginPath();
            ctx.moveTo(ax, ay); ctx.lineTo(ax + 16 * ak, ay);
            ctx.moveTo(ax + 16 * ak, ay); ctx.lineTo(ax + 16 * ak - 5, ay - 4);
            ctx.moveTo(ax + 16 * ak, ay); ctx.lineTo(ax + 16 * ak - 5, ay + 4);
            ctx.stroke();
            ctx.globalAlpha = 1;
          }
        }
      });
    }

    /* ── 앵커 승격 ────────────────────────────────── */
    if (mk > 0.02 && spec.anchorFrom) {
      const cw = 100, gap = 38, y = 212, bh = 30;
      const total = cw * 2 + gap;
      const startX = (w - total) / 2;
      const k = clamp(mk * 1.6);

      [spec.anchorFrom, spec.anchorTo].forEach((label, i) => {
        const born = i === 0 ? k : clamp(mk * 1.8 - 0.5);
        if (born < 0.02) return;
        const x = startX + i * (cw + gap);
        ctx.globalAlpha = clamp(born * 1.6);
        ctx.lineWidth = 3;
        ctx.strokeStyle = i === 1 ? tone('accent') : tone('sub');
        roundRect(ctx, x, y, cw, bh, 3); ctx.stroke();
        ctx.textAlign = 'center';
        const fs = fit(label, 800, 13, cw - 12, 9);
        ctx.font = disp(800, fs);
        ctx.fillStyle = i === 1 ? tone('accent') : tone('sub');
        ctx.fillText(label, x + cw / 2, y + 20);
        ctx.globalAlpha = 1;
      });

      const ak = clamp(mk * 1.8 - 0.35);
      if (ak > 0.02) {
        const ax = startX + cw + 6, ay = y + bh / 2;
        ctx.globalAlpha = clamp(ak * 1.6);
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
        ctx.beginPath();
        ctx.moveTo(ax, ay); ctx.lineTo(ax + 26 * ak, ay);
        ctx.moveTo(ax + 26 * ak, ay); ctx.lineTo(ax + 26 * ak - 7, ay - 5);
        ctx.moveTo(ax + 26 * ak, ay); ctx.lineTo(ax + 26 * ak - 7, ay + 5);
        ctx.stroke();
        ctx.globalAlpha = 1;
      }
    }

    /* ── 남는 한 줄 ───────────────────────────────── */
    const hook = spec.hook || scene?.hook;
    if (hook && mk > 0.35) {
      const k = clamp((mk - 0.35) / 0.5);
      const s = Math.min(1, spring(k));
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(String(hook), 900, 15, w - 30, 10);
      ctx.font = disp(900, fs * s); ctx.fillStyle = tone('ink');
      ctx.fillText(String(hook), w / 2, 276);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
