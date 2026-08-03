import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* idlespin — 따로 도는 고리 셋이 한 뿌리에서 나온다.

   공회전은 '앞으로 안 가면서 계속 도는 것'이다. 그래서 세 버그를 막대나 목록이 아니라
   닫힌 고리 셋으로 그린다. 고리마다 주민 표식이 돌고 있고, 한 바퀴마다 같은 자리의 ✕ 를
   지난다 — 실패하고, 다시 달리고, 또 실패한다(spinCue·nameCue).

   rootCue 에서 아래에서 뿌리 선이 올라와 셋을 잇는다. 원문이 "이 세 버그는 독립적인 것처럼
   보이지만 뿌리는 같다"고 말하는 자리다. 동시에 표식들이 고리를 **벗어나** 뿌리 선을 타고
   오른쪽으로 흘러 나간다 — 도는 것이 흐르는 것으로 바뀌는 게 이 문단의 결말이다.

   🔴 정지 화면을 만들지 않으려고 고치는 장면을 '멈춤'으로 그리지 않았다. 고리가 멈추면
   그 순간부터 화면이 죽는다. 대신 같은 표식이 계속 움직이되 궤적이 닫힌 고리에서 열린
   직선으로 바뀐다 — 고쳐졌다는 뜻이 움직임의 모양으로 나온다.

   계속 도는 것 = 표식 셋(고치기 전에는 고리를 돌고, 고친 뒤에는 뿌리 선을 흐른다). */

const SPIN = [2.2, 2.9, 1.8];
const RUN = 3.6;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));

    const sk = ease(cue(spec.spinCue ?? 0, 0.15, 0.6));
    const nk = ease(cue(spec.nameCue ?? 1, 0.15, 0.65));
    const rk = ease(cue(spec.rootCue ?? 2, 0.15, 0.6));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const loops = spec.loops || [];
    const n = Math.max(1, loops.length);
    const cxs = [62, 176, 290];
    const cy = 74, r = 32;
    const rootY = 206, rootL = 20, rootR = w - 20;
    const failAng = -Math.PI * 0.62;

    for (let i = 0; i < n; i++) {
      const cx = cxs[i] ?? (62 + i * 114);
      const born = clamp(sk * (n + 1) - i);
      if (born < 0.02) continue;
      const s = Math.min(1, spring(born));

      /* 고리 */
      ctx.globalAlpha = clamp(born * 1.7);
      ctx.lineWidth = 3;
      ctx.strokeStyle = rk > 0.45 ? tone('track') : tone('ink');
      ctx.beginPath(); ctx.arc(cx, cy, r * s, 0, Math.PI * 2); ctx.stroke();

      /* 실패 지점 ✕ */
      const fk = clamp(nk * (n + 1) - i);
      if (fk > 0.05) {
        const k = clamp(fk * 1.5);
        const fx = cx + Math.cos(failAng) * r, fy = cy + Math.sin(failAng) * r;
        const rr = 5 * k;
        ctx.globalAlpha = clamp(born * 1.7) * k;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(fx - rr, fy - rr); ctx.lineTo(fx + rr, fy + rr);
        ctx.moveTo(fx + rr, fy - rr); ctx.lineTo(fx - rr, fy + rr);
        ctx.stroke();
      }

      /* 쿨다운 걸쇠 — 실패 지점 뒤에 걸린다 */
      if (rk > 0.06) {
        const k = clamp(rk * 1.6 - i * 0.12);
        if (k > 0.02) {
          const a = failAng + 0.42;
          ctx.globalAlpha = k;
          setShadow(ctx, GLOW, 9, 0);
          ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
          ctx.beginPath();
          ctx.moveTo(cx + Math.cos(a) * (r - 9 * k), cy + Math.sin(a) * (r - 9 * k));
          ctx.lineTo(cx + Math.cos(a) * (r + 9 * k), cy + Math.sin(a) * (r + 9 * k));
          ctx.stroke();
          clearShadow(ctx);
        }
      }
      ctx.globalAlpha = 1;

      /* 이름 */
      if (fk > 0.05) {
        ctx.globalAlpha = clamp(fk * 1.5);
        ctx.textAlign = 'center';
        const label = loops[i] || '';
        const fs = fit(label, 800, 11.5, 108, 8);
        ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
        ctx.fillText(label, cx, 126);
        ctx.globalAlpha = 1;
      }

      /* 뿌리로 내려가는 줄기 */
      if (rk > 0.02) {
        const k = ease(clamp(rk / 0.7));
        ctx.globalAlpha = clamp(rk * 2);
        ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(cx, rootY); ctx.lineTo(cx, lerp(rootY, 136, k));
        ctx.stroke();
        ctx.globalAlpha = 1;
      }

      /* 계속 도는 것 — 고리를 돌던 표식이 뿌리 선으로 나간다 */
      {
        const out = clamp((rk - 0.45) / 0.35);
        ctx.globalAlpha = clamp(born * 1.7);
        setShadow(ctx, GLOW, 8, 0);
        ctx.fillStyle = tone('accent');
        if (out < 0.5) {
          const u = frac(t / SPIN[i % 3]) * Math.PI * 2 + failAng;
          ctx.beginPath();
          ctx.arc(cx + Math.cos(u) * r, cy + Math.sin(u) * r, 4.5, 0, Math.PI * 2);
          ctx.fill();
        } else {
          const u = frac(t / RUN + i / n);
          const mx = lerp(rootL + 6, rootR - 6, u);
          ctx.globalAlpha = clamp(born * 1.7) * (1 - Math.abs(u - 0.5) * 1.2);
          ctx.beginPath();
          ctx.arc(mx, rootY, 4.5, 0, Math.PI * 2);
          ctx.fill();
        }
        clearShadow(ctx);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 뿌리 ─────────────────────────────────────── */
    if (rk > 0.02) {
      const k = ease(clamp(rk / 0.6));
      ctx.globalAlpha = clamp(rk * 2);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.moveTo(rootL, rootY);
      ctx.lineTo(lerp(rootL, rootR, k), rootY);
      ctx.stroke();
      ctx.globalAlpha = 1;

      if (rk > 0.35 && spec.root) {
        const kk = clamp((rk - 0.35) / 0.45);
        ctx.globalAlpha = kk;
        ctx.textAlign = 'center';
        const fs = fit(spec.root, 900, 16, w - 40, 11);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
        ctx.fillText(spec.root, w / 2, rootY + 30);
        ctx.globalAlpha = 1;
      }
      if (rk > 0.6 && spec.fixNote) {
        const kk = clamp((rk - 0.6) / 0.4);
        ctx.globalAlpha = kk * 0.95;
        ctx.textAlign = 'center';
        const fs = fit(spec.fixNote, 700, 11, w - 24, 8);
        ctx.font = disp(700, fs); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.fixNote, w / 2, rootY + 52);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
