import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* worn — 다니는 길만 닳고, 안 가는 자리는 빈 채로 남는다.

   이 편(5-1)의 **두 번째 샷**이다. 첫 샷에서 숫자를 먼저 보여 준 뒤, 그 숫자가
   왜 필요했는지를 여기서 갚는다 — 마을에 할 일이 둘뿐이었다.
   열매와 돌 사이에만 발자국이 겹겹이 나 있고 주민 표식 셋이 그 길만 왕복한다.
   아래 밭·집 자리는 점선 빈 칸으로 남아 한 번도 밟히지 않는다.
   원문 2절 "GOAP 엔진은 준비됐는데 '재료'가 없는 상태"를 그림으로 옮긴 것이다.

   🔴 여기서 그리는 것은 '멈춤'이 아니다. 엔진은 돌고 주민도 쉬지 않고 움직인다 —
   다만 갈 곳이 둘뿐이다. 그래서 멈춤을 뜻하는 못·✕·정지 표식을 하나도 쓰지 않았다.
   문제는 정지가 아니라 단조로움이고, 그 단조로움을 깨는 것이 첫 샷의 숫자 1 이다.

   🔴 「GOAP 엔진 — 준비됨」 배지는 **자막 1** 에 물려 있다(emptyCue). 그 자막이
   "엔진은 다 됐는데"로 시작하기 때문이다. 배지를 자막 0 으로 올리면 아무도 안 부르는
   문자열이 3초 넘게 떠 있게 된다 — 이 시리즈가 실제로 겪은 결함이다.

   계속 도는 것 = 두 노드 사이를 왕복하는 주민 표식 셋(위상이 1/3 씩 어긋나 있다),
   빈 자리 테두리의 점선 흐름. 둘 다 t 의 함수라 마지막 자막까지 멈추지 않는다. */

const SHUTTLE = 3.8;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));

    const pk = ease(cue(spec.pathCue ?? 0, 0.15, 0.60));   // 닳은 길 · 노드 · 왕복
    const ek = ease(cue(spec.emptyCue ?? 1, 0.15, 0.60));  // 엔진 배지 · 빈 자리
    const lk = ease(cue(spec.lackCue ?? 2, 0.15, 0.62));   // '재료'가 없는 상태

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    /* ── 두 노드와 닳은 길 ────────────────────────── */
    const cy = 104, r = 27;
    const lx = 76, rx = w - 76;
    const px0 = lx + r + 6, px1 = rx - r - 6;
    const names = spec.nodes || ['열매', '돌'];

    if (pk > 0.02) {
      const k = clamp(pk * 1.6);
      ctx.globalAlpha = k;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 14;
      ctx.beginPath(); ctx.moveTo(px0, cy); ctx.lineTo(px1, cy); ctx.stroke();
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(px0, cy); ctx.lineTo(px1, cy); ctx.stroke();

      // 발자국 — 가운데로 갈수록 촘촘하고 진하다(오래 다닌 길)
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

    [[lx, names[0]], [rx, names[1]]].forEach(([cx, name], i) => {
      const born = clamp(pk * 2.4 - i * 0.35);
      if (born < 0.02) return;
      const s = Math.min(1, spring(clamp(born)));
      ctx.globalAlpha = clamp(born * 1.8);
      ctx.lineWidth = 4;
      ctx.strokeStyle = tone('ink');
      ctx.beginPath(); ctx.arc(cx, cy, r * s, 0, Math.PI * 2); ctx.stroke();
      ctx.textAlign = 'center';
      const fs = fit(name, 900, 15, r * 1.7, 9);
      ctx.font = disp(900, fs * s);
      ctx.fillStyle = tone('ink');
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

    if (pk > 0.5 && spec.onlyLabel) {
      const k = clamp((pk - 0.5) / 0.5);
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.onlyLabel, 800, 13, w - 60, 9);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.onlyLabel, w / 2, 156);
      ctx.globalAlpha = 1;
    }

    /* ── 엔진은 준비됐다 (자막 1 이 부른다) ───────── */
    if (ek > 0.02 && spec.engineLabel) {
      const k = clamp(ek * 2.6);
      const bw = 182;
      ctx.globalAlpha = k;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
      roundRect(ctx, 10, 10, bw, 25, 3); ctx.stroke();
      ctx.textAlign = 'left';
      const fs = fit(spec.engineLabel, 800, 11.5, bw - 20, 8);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.engineLabel, 20, 28);
      ctx.globalAlpha = 1;
    }

    /* ── 빈 자리 둘 — 밭과 집 ─────────────────────── */
    const plots = spec.plots || [];
    if (ek > 0.1 && plots.length) {
      const pw = 136, gap = 26;
      const total = plots.length * pw + (plots.length - 1) * gap;
      const sx = (w - total) / 2;
      const py = 178, ph = 60;

      ctx.setLineDash([9, 7]);
      ctx.lineDashOffset = -(t * 13) % 16;      // 계속 도는 것 — 빈 자리의 점선 흐름
      plots.forEach((label, i) => {
        const born = clamp((ek - 0.1) * 2.4 - i * 0.4);
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

    if (ek > 0.55 && spec.plotNote) {
      const k = clamp((ek - 0.55) / 0.45);
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.plotNote, 700, 11, w - 40, 8);
      ctx.font = disp(700, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.plotNote, w / 2, 258);
      ctx.globalAlpha = 1;
    }

    /* ── '재료'가 없는 상태 (자막 2) ──────────────── */
    if (lk > 0.02 && spec.lackLabel) {
      const k = clamp(lk * 1.6);
      const s = Math.min(1, spring(clamp(lk)));
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.lackLabel, 900, 18, w - 34, 11);
      ctx.font = disp(900, fs * s);
      setShadow(ctx, GLOW, 10, 0);
      ctx.fillStyle = tone('accent');
      ctx.fillText(spec.lackLabel, w / 2, 290);
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
