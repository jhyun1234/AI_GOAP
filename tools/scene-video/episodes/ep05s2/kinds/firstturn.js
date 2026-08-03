import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* firstturn — 첫 샷의 왕복이 여기서 한 바퀴가 된다.

   밭 · 부엌 · 집 세 지점이 삼각으로 놓이고, turnCue 에서 셋을 잇는 획이 순서대로 그어져
   마지막 획이 출발점으로 되돌아오며 고리가 닫힌다. 닫힌 뒤부터 주민 표식이 그 위를
   끝없이 돈다 — 원문 1절의 "경제 루프의 첫 번째 회전"이 이 회전이다.
   집 옆에는 '돌아갈 곳'이 붙는다(원문 1절의 표현 그대로).
   doneCue 에서 아래 일곱 칸(M3 성공 기준 S1~S7)이 왼쪽부터 채워진다.

   🔴 1편(ep05s) 첫 샷 worn 에서는 표식들이 두 점 사이를 **왕복**했다(끝이 없다). 여기서는
   같은 표식이 **한 방향으로 돌아 제자리로 온다**. 같은 재료를 뒤집어 쓴 자리이고,
   그래서 두 샷의 리듬이 서로를 설명한다.

   계속 도는 것 = 고리를 도는 주민 표식 둘. */

const RING = 6.2;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, scene, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));

    const tk = ease(cue(spec.turnCue ?? 0, 0.15, 0.68));
    const dk = ease(cue(spec.doneCue ?? 1, 0.15, 0.62));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const names = spec.ring || ['밭', '부엌', '집'];
    const P = [{ x: 86, y: 96 }, { x: 266, y: 96 }, { x: 176, y: 182 }];
    const NW = 84, NH = 32;

    /* ── 제목 ─────────────────────────────────────── */
    if (tk > 0.02 && spec.turnLabel) {
      ctx.globalAlpha = clamp(tk * 2);
      ctx.textAlign = 'center';
      const fs = fit(spec.turnLabel, 800, 13, w - 40, 9);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.turnLabel, w / 2, 34);
      ctx.globalAlpha = 1;
    }

    /* ── 고리 획 셋 — 마지막 획이 tk 0.833 에서 닫힌다 ── */
    const segK = i => clamp(tk * 4.2 - 0.5 - i);
    for (let i = 0; i < 3; i++) {
      const k = segK(i);
      if (k < 0.02) continue;
      const a = P[i], b = P[(i + 1) % 3];
      ctx.globalAlpha = clamp(k * 1.8);
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(a.x, a.y);
      ctx.lineTo(lerp(a.x, b.x, ease(k)), lerp(a.y, b.y, ease(k)));
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* 고리가 닫히면 전체가 강조색으로 굳는다 */
    const closed = clamp((tk - 0.86) / 0.14);
    if (closed > 0.02) {
      ctx.globalAlpha = closed;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      setShadow(ctx, GLOW, 12, 0);
      ctx.beginPath();
      ctx.moveTo(P[0].x, P[0].y);
      ctx.lineTo(P[1].x, P[1].y);
      ctx.lineTo(P[2].x, P[2].y);
      ctx.closePath();
      ctx.stroke();
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    /* 계속 도는 것 — 고리를 도는 표식 둘 */
    if (closed > 0.3) {
      const segs = [[P[0], P[1]], [P[1], P[2]], [P[2], P[0]]];
      const len = segs.map(([a, b]) => Math.hypot(b.x - a.x, b.y - a.y));
      const total = len[0] + len[1] + len[2];
      for (let m = 0; m < 2; m++) {
        let d = frac(t / RING + m / 2) * total;
        let si = 0;
        while (si < 2 && d > len[si]) { d -= len[si]; si++; }
        const [a, b] = segs[si];
        const u = clamp(d / len[si]);
        ctx.globalAlpha = closed;
        setShadow(ctx, GLOW, 10, 0);
        ctx.fillStyle = tone('accent');
        ctx.beginPath();
        ctx.arc(lerp(a.x, b.x, u), lerp(a.y, b.y, u), 5, 0, Math.PI * 2);
        ctx.fill();
        clearShadow(ctx);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 세 지점 ──────────────────────────────────── */
    names.forEach((name, i) => {
      const born = clamp(tk * 4.6 - i * 0.6);
      if (born < 0.02) return;
      const p = P[i];
      const s = Math.min(1, spring(clamp(born)));
      const last = i === names.length - 1;
      ctx.globalAlpha = clamp(born * 1.8);
      ctx.lineWidth = 3;
      ctx.strokeStyle = last && closed > 0.3 ? tone('accent') : tone('ink');
      roundRect(ctx, p.x - NW * s / 2, p.y - NH * s / 2, NW * s, NH * s, 3);
      ctx.stroke();
      ctx.textAlign = 'center';
      const fs = fit(name, 900, 16, NW - 14, 11);
      ctx.font = disp(900, fs * s);
      ctx.fillStyle = last && closed > 0.3 ? tone('accent') : tone('ink');
      ctx.fillText(name, p.x, p.y + 6);
      ctx.globalAlpha = 1;
    });

    if (closed > 0.35 && spec.returnLabel) {
      const k = clamp((closed - 0.35) / 0.5);
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.returnLabel, 800, 12.5, 140, 9);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.returnLabel, P[2].x, P[2].y + 36);
      ctx.globalAlpha = 1;
    }

    /* ── 성공 기준 일곱 칸 ────────────────────────── */
    const cells = spec.criteria || [];
    if (dk > 0.02 && cells.length) {
      const cw = 42, gap = 5;
      const total = cells.length * cw + (cells.length - 1) * gap;
      const sx = (w - total) / 2;
      const cy = 252, chh = 26;

      ctx.globalAlpha = clamp(dk * 2);
      ctx.textAlign = 'left';
      const lfs = fit(spec.criteriaLabel || '', 700, 10, 160, 8);
      ctx.font = disp(700, lfs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.criteriaLabel || '', sx, cy - 10);
      ctx.globalAlpha = 1;

      cells.forEach((c, i) => {
        const born = clamp(dk * (cells.length + 1.2) - i);
        const x = sx + i * (cw + gap);
        ctx.globalAlpha = clamp(dk * 2) * 0.6;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        roundRect(ctx, x, cy, cw, chh, 3); ctx.stroke();
        ctx.globalAlpha = 1;
        if (born < 0.02) return;
        const s = Math.min(1, spring(clamp(born)));
        ctx.globalAlpha = clamp(born * 1.8);
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        roundRect(ctx, x + (cw - cw * s) / 2, cy + (chh - chh * s) / 2, cw * s, chh * s, 3);
        ctx.stroke();
        ctx.textAlign = 'center';
        ctx.font = mono(700, 11); ctx.fillStyle = tone('accent');
        ctx.fillText(c, x + cw / 2, cy + 18);
        ctx.globalAlpha = 1;
      });

      if (dk > 0.75 && spec.criteriaNote) {
        const k = clamp((dk - 0.75) / 0.25);
        ctx.globalAlpha = k;
        ctx.textAlign = 'right';
        const fs = fit(spec.criteriaNote, 900, 12, 120, 9);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
        ctx.fillText(spec.criteriaNote, sx + total, cy - 10);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 남는 한 줄 ───────────────────────────────── */
    /* 🔴 훅을 그리는 코드는 1차본에서 outofstep(예고 샷)에만 있었다. 2026-08-03 분할로 이 편이
       예고 샷 없이 firstturn 로 끝나게 되어 같은 블록을 여기로 옮겼다. 안 옮기면 ep01s 부터 이어 온
       마지막 한 줄이 이 편에서만 사라진다. 이 kind 는 바닥이 비어 있어 좌표를 건드릴 필요가 없었다. */
    const hook = spec.hook || scene?.hook;
    if (hook && dk > 0.6) {
      const k2 = clamp((dk - 0.6) / 0.4);
      const s2 = Math.min(1, spring(k2));
      ctx.globalAlpha = k2;
      ctx.textAlign = 'center';
      const fsH = fit(String(hook), 900, 15, w - 30, 10);
      ctx.font = disp(900, fsH * s2); ctx.fillStyle = tone('ink');
      ctx.fillText(String(hook), w / 2, 299);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
