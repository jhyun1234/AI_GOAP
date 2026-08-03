import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* hairline — 손톱만 한 차이 하나가 아주 무거운 것을 혼자 떠받친다.

   날 것 3 · 조리 2 를 블록 탑 둘로 세운다. 높이가 곧 값이라 두 탑의 차이는 정확히 한 칸이고,
   그 한 칸에서 가느다란 기둥이 솟아 위에서 내려오는 넓은 판을 받는다. 판에 적힌 것이
   원문 3절의 "왜 요리를 해야 하는가 — 게임 루프 전체의 동기"다.

   🔴 앞선 회차에 '두 값의 간격'을 그린 샷이 있지만 저기는 **벌어진 간격**이 문제였다.
   여기는 반대로 **간격이 손톱만 한데 그 위에 얹힌 것이 화면에서 제일 크다** — 그래서
   판을 화면 폭 가득 그리고 기둥은 12px 로 뒀다. 눈이 그 불균형을 먼저 읽어야 한다.

   계속 도는 것 = 쌓인 칸 안을 위로 흐르는 빗금(자막 0부터), 차이 한 칸의 테두리를 도는
   표식(자막 1부터), 판이 앉은 뒤의 미세한 눌림(자막 2부터). */

const ORBIT = 3.2;
const PRESS = 2.4;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));

    const rk = ease(cue(spec.rawCue ?? 0, 0.15, 0.6));
    const ck = ease(cue(spec.cookCue ?? 1, 0.15, 0.62));
    const sk = ease(cue(spec.slabCue ?? 2, 0.15, 0.55));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const BASE = 268, BH = 30, BW = 56;
    const lx = 111, rx = 185;
    const lcx = lx + BW / 2, rcx = rx + BW / 2;
    const raw = spec.raw || { label: '날 것', v: 3 };
    const cooked = spec.cooked || { label: '조리', v: 2 };

    /* ── 축 이름 ──────────────────────────────────── */
    if (rk > 0.02 && spec.axisLabel) {
      ctx.globalAlpha = clamp(rk * 2);
      ctx.textAlign = 'left';
      ctx.font = disp(800, 10.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.axisLabel, 14, 26);
      ctx.globalAlpha = 1;
    }

    /* ── 블록 탑 둘 ───────────────────────────────── */
    const tower = (x, n, prog, strong, label, value) => {
      for (let i = 0; i < n; i++) {
        const born = clamp(prog * (n + 0.6) - i);
        if (born < 0.02) continue;
        const y = BASE - (i + 1) * BH;
        const s = Math.min(1, spring(clamp(born * 1.2)));
        ctx.globalAlpha = clamp(born * 1.8);
        ctx.lineWidth = 3;
        ctx.strokeStyle = strong && i === n - 1 ? tone('accent') : tone('ink');
        roundRect(ctx, x + (BW - BW * s) / 2, y + (BH - BH * s) / 2 + 2,
                  BW * s, (BH - 4) * s, 3);
        ctx.stroke();
        ctx.globalAlpha = 1;
      }
      /* 🔴 계속 도는 것 — 쌓인 칸 안을 위로 흐르는 빗금 (2026-08-04 검수 2차 반려 R-2).
         전에는 이 kind 의 도는 것이 둘 다 뒤쪽 자막에 있었다 — 테두리 표식은 ck(자막 1),
         판 눌림은 sk(자막 2). 그래서 자막 0 구간이 통째로 멈췄고, 16행 머리말은 그 둘을
         "계속 도는 것"이라 선언해 화면과 어긋나 있었다(ep04s2 bar() 와 같은 결함).
         소비되는 양을 그리는 탑이라 위로 흐르는 빗금이 뜻과도 맞는다. */
      const top = BASE - n * BH;
      if (prog > 0.06) {
        ctx.save();
        ctx.beginPath();
        ctx.rect(x + 2, top + 2, BW - 4, BASE - top - 4);
        ctx.clip();
        ctx.strokeStyle = strong ? tone('accent') : tone('sub');
        ctx.lineWidth = 3;
        ctx.globalAlpha = clamp(prog * 1.8) * 0.3;
        const hoff = frac(t / 2.1) * 20;
        for (let hy = BASE + 20 - hoff; hy > top - 20; hy -= 20) {
          ctx.beginPath();
          ctx.moveTo(x - 4, hy); ctx.lineTo(x + BW + 4, hy - 12);
          ctx.stroke();
        }
        ctx.restore();
        ctx.globalAlpha = 1;
      }
      if (prog > 0.55) {
        const k = clamp((prog - 0.55) / 0.45);
        ctx.globalAlpha = k;
        ctx.textAlign = 'center';
        ctx.font = mono(700, 17);
        ctx.fillStyle = strong ? tone('accent') : tone('ink');
        ctx.fillText(String(value), x + BW / 2, top + BH / 2 + 6);
        const fs = fit(label, 800, 12.5, BW + 22, 9);
        ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
        ctx.fillText(label, x + BW / 2, 290);
        ctx.globalAlpha = 1;
      }
      return top;
    };

    const ltop = tower(lx, raw.v ?? 3, rk, true, raw.label, raw.v ?? 3);
    const rtop = tower(rx, cooked.v ?? 2, ck, false, cooked.label, cooked.v ?? 2);

    /* ── 차이 한 칸 ───────────────────────────────── */
    const diffTop = ltop, diffBot = rtop;
    if (ck > 0.3) {
      const k = clamp((ck - 0.3) / 0.5);
      const bx = 252;
      ctx.globalAlpha = k;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(bx - 8, diffTop); ctx.lineTo(bx, diffTop);
      ctx.lineTo(bx, diffBot); ctx.lineTo(bx - 8, diffBot);
      ctx.stroke();
      ctx.textAlign = 'left';
      const fs = fit(spec.gapLabel || '차이 1', 900, 14, w - bx - 14, 10);
      ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.gapLabel || '차이 1', bx + 7, (diffTop + diffBot) / 2 + 5);
      ctx.globalAlpha = 1;

      /* 계속 도는 것 — 차이 한 칸의 테두리를 도는 표식 */
      const pw = BW, ph = BH - 4, px = lx, py = diffTop + 2;
      const per = 2 * (pw + ph);
      let d = frac(t / ORBIT) * per, mx = px, my = py;
      if (d < pw) { mx = px + d; my = py; }
      else if (d < pw + ph) { mx = px + pw; my = py + (d - pw); }
      else if (d < 2 * pw + ph) { mx = px + pw - (d - pw - ph); my = py + ph; }
      else { mx = px; my = py + ph - (d - 2 * pw - ph); }
      ctx.globalAlpha = k;
      setShadow(ctx, GLOW, 10, 0);
      ctx.fillStyle = tone('accent');
      ctx.beginPath(); ctx.arc(mx, my, 4, 0, Math.PI * 2); ctx.fill();
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    /* ── 기둥 — 그 한 칸에서 솟는다 ───────────────── */
    const SLAB_Y = 78, SLAB_H = 52;
    const bob = 1.4 * Math.sin(frac(t / PRESS) * Math.PI * 2);
    const landed = clamp((sk - 0.55) / 0.25);
    const slabY = lerp(SLAB_Y - 34, SLAB_Y, ease(clamp(sk / 0.55))) + bob * landed;

    if (ck > 0.5) {
      const k = clamp((ck - 0.5) / 0.5);
      const top = lerp(diffTop, slabY + SLAB_H, ease(k));
      ctx.globalAlpha = clamp(k * 1.6);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      setShadow(ctx, GLOW, 10, 0);
      ctx.beginPath();
      ctx.moveTo(lcx, diffTop); ctx.lineTo(lcx, top);
      ctx.stroke();
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    /* ── 판이 얹힌다 ──────────────────────────────── */
    if (sk > 0.02) {
      const bx = 16, bw = w - 32;
      ctx.globalAlpha = clamp(sk * 2);
      ctx.lineWidth = 4; ctx.strokeStyle = tone('ink');
      roundRect(ctx, bx, slabY, bw, SLAB_H, 4); ctx.stroke();

      ctx.textAlign = 'center';
      const s1 = spec.slabLabel || '';
      const fs = fit(s1, 900, 18, bw - 26, 12);
      ctx.font = disp(900, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(s1, w / 2, slabY + 28);

      if (spec.slabNote) {
        const nfs = fit(spec.slabNote, 700, 11, bw - 26, 8);
        ctx.font = disp(700, nfs); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.slabNote, w / 2, slabY + 45);
      }
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
