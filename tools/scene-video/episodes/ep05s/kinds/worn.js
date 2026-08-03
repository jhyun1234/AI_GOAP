import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* worn — 다니는 길만 닳고, 안 가는 자리는 빈 채로 남는다.

   이 편의 첫 화면은 '멈춤'이 아니다. 엔진은 돌고 주민도 계속 움직인다 — 다만 늘 같은
   두 곳(열매·돌) 사이만 왕복한다. 그래서 그 구간에는 발자국이 겹겹이 나 있고,
   아래 밭·집 자리는 점선 빈 칸으로 남아 한 번도 밟히지 않는다.
   원문 2절 "GOAP 엔진은 준비됐는데 '재료'가 없는 상태"를 그림으로 옮긴 것이다.

   🔴 직전 편 첫 샷은 주민이 굳는 그림이었다. 여기는 정반대로 **쉬지 않고 움직이는데
   갈 곳이 둘뿐**인 그림이라, 멈춘 표식·못·✕ 를 하나도 쓰지 않았다.

   계속 도는 것 = 두 노드 사이를 왕복하는 주민 표식 셋(각자 다른 위상), 그리고
   빈 자리의 점선이 흐르는 것. */

const SHUTTLE = 3.8;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));

    const pk = ease(cue(spec.pathCue ?? 0, 0.15, 0.6));
    const ok = ease(cue(spec.onlyCue ?? 1, 0.15, 0.6));
    const ek = ease(cue(spec.emptyCue ?? 2, 0.15, 0.62));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    /* ── 엔진은 준비됐다 ──────────────────────────── */
    {
      const k = clamp(pk * 2.2);
      const bw = 182;
      ctx.globalAlpha = k;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
      roundRect(ctx, 10, 10, bw, 25, 3); ctx.stroke();
      ctx.textAlign = 'left';
      const fs = fit(spec.engineLabel || '', 800, 11.5, bw - 20, 8);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.engineLabel || '', 20, 28);
      ctx.globalAlpha = 1;
    }

    /* ── 두 노드 ──────────────────────────────────── */
    const cy = 96, r = 27;
    const lx = 76, rx = w - 76;
    const px0 = lx + r + 6, px1 = rx - r - 6;
    const names = spec.nodes || ['열매', '돌'];

    /* 닳은 길 — 발자국이 겹겹이 나 있다 */
    if (pk > 0.02) {
      const k = clamp(pk * 1.6);
      ctx.globalAlpha = k;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 14;
      ctx.beginPath(); ctx.moveTo(px0, cy); ctx.lineTo(px1, cy); ctx.stroke();
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(px0, cy); ctx.lineTo(px1, cy); ctx.stroke();

      // 발자국 — 가운데로 갈수록 촘촘하고 진하다
      for (let i = 0; i < 22; i++) {
        const u = (i + 0.5) / 22;
        const x = lerp(px0, px1, u);
        const dy = (i % 2 ? 1 : -1) * 6;
        ctx.globalAlpha = k * (0.30 + 0.45 * Math.sin(Math.PI * u));
        ctx.fillStyle = tone('ink');
        ctx.fillRect(x - 2.5, cy + dy - 2, 5, 4);
      }
      ctx.globalAlpha = 1;
    }

    /* 노드 두 개 */
    [[lx, names[0]], [rx, names[1]]].forEach(([cx, name], i) => {
      const born = clamp(pk * 2.4 - i * 0.35);
      if (born < 0.02) return;
      const s = Math.min(1, spring(clamp(born)));
      ctx.globalAlpha = clamp(born * 1.8);
      ctx.lineWidth = 4;
      ctx.strokeStyle = ok > 0.35 ? tone('accent') : tone('ink');
      if (ok > 0.35) setShadow(ctx, GLOW, 12, 0);
      ctx.beginPath(); ctx.arc(cx, cy, r * s, 0, Math.PI * 2); ctx.stroke();
      clearShadow(ctx);
      ctx.textAlign = 'center';
      const fs = fit(name, 900, 14, r * 1.7, 9);
      ctx.font = disp(900, fs * s);
      ctx.fillStyle = ok > 0.35 ? tone('accent') : tone('ink');
      ctx.fillText(name, cx, cy + 5);
      ctx.globalAlpha = 1;
    });

    /* 계속 도는 것 — 주민 표식 셋이 두 노드 사이를 왕복한다 */
    if (pk > 0.25) {
      const k = clamp((pk - 0.25) / 0.4);
      for (let i = 0; i < 3; i++) {
        const u = 0.5 - 0.5 * Math.cos(frac(t / SHUTTLE + i / 3) * Math.PI * 2);
        const x = lerp(px0 + 6, px1 - 6, u);
        ctx.globalAlpha = k * 0.95;
        ctx.fillStyle = tone('ink');
        roundRect(ctx, x - 5, cy - 16 + (i % 2) * 22, 10, 12, 2); ctx.fill();
        ctx.globalAlpha = 1;
      }
    }

    /* ── 하는 건 이 둘뿐 ──────────────────────────── */
    if (ok > 0.05 && spec.onlyLabel) {
      const k = clamp(ok * 1.6);
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.onlyLabel, 800, 13, w - 60, 9);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.onlyLabel, w / 2, 150);
      ctx.globalAlpha = 1;
    }

    /* ── 빈 자리 둘 — 밭과 집 ─────────────────────── */
    const plots = spec.plots || [];
    if (ek > 0.02 && plots.length) {
      const pw = 136, gap = 26;
      const total = plots.length * pw + (plots.length - 1) * gap;
      const sx = (w - total) / 2;
      const py = 172, ph = 62;

      ctx.setLineDash([9, 7]);
      ctx.lineDashOffset = -(t * 13) % 16;
      plots.forEach((label, i) => {
        const born = clamp(ek * 2.2 - i * 0.4);
        if (born < 0.02) return;
        const x = sx + i * (pw + gap);
        ctx.globalAlpha = clamp(born * 1.6) * 0.9;
        ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
        roundRect(ctx, x, py, pw, ph, 4); ctx.stroke();
        ctx.textAlign = 'center';
        const fs = fit(label, 900, 20, pw - 22, 12);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('sub');
        ctx.fillText(label, x + pw / 2, py + ph / 2 + 7);
        ctx.globalAlpha = 1;
      });
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
    }

    if (ek > 0.4 && spec.plotNote) {
      const k = clamp((ek - 0.4) / 0.4);
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.plotNote, 700, 11, w - 40, 8);
      ctx.font = disp(700, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.plotNote, w / 2, 254);
      ctx.globalAlpha = 1;
    }

    if (ek > 0.55 && spec.lackLabel) {
      const k = clamp((ek - 0.55) / 0.45);
      const s = Math.min(1, spring(k));
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.lackLabel, 900, 17, w - 34, 11);
      ctx.font = disp(900, fs * s); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.lackLabel, w / 2, 288);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
