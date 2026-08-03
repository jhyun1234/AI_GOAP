import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* mealspot — 수치로만 오르던 일이 자리로 옮겨 간다.

   왼쪽이 예전이다. 주민은 제자리에 있고 머리 위로 숫자만 계속 올라간다 — 점선으로 그린다.
   보이지 않는 행위라는 뜻이다(hideCue). 오른쪽이 지금이다. 모닥불이 앵커가 되고 주민들이
   그리로 이동한다(moveCue). 도중에 넷이 한 타일에 겹쳐 예약 충돌이 나고, 반경 안으로
   흩어지면서 풀린다 — 문제와 해결이 한 번의 이동 안에서 연속으로 일어난다.

   🔴 '보이는 행위'가 이 문단의 요지라, 대비를 색이나 굵기가 아니라 **자리**로 만들었다.
   왼쪽 주민은 화면에서 한 번도 움직이지 않고 오른쪽 주민은 화면을 가로질러 간다.

   계속 도는 것 = 모닥불의 불꽃 세 줄기와, 왼쪽에서 계속 떠오르는 숫자. */

const FLAME = 1.6;
const RISE = 2.4;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));

    const hk = ease(cue(spec.hideCue ?? 0, 0.15, 0.6));
    const mk = ease(cue(spec.moveCue ?? 1, 0.15, 0.72));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const midX = 172;

    /* ── 가름선 ───────────────────────────────────── */
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.setLineDash([5, 7]);
    ctx.beginPath(); ctx.moveTo(midX, 24); ctx.lineTo(midX, 252); ctx.stroke();
    ctx.setLineDash([]);

    /* ── 왼쪽: 예전 ───────────────────────────────── */
    {
      const k = clamp(hk * 1.8);
      ctx.globalAlpha = k * 0.85;
      ctx.textAlign = 'left';
      const s = spec.beforeLabel || '';
      const fs = fit(s, 800, 11, midX - 20, 8);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(s, 8, 32);

      const cx = 78, cy = 168;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('sub');
      ctx.beginPath(); ctx.arc(cx, cy, 15, 0, Math.PI * 2); ctx.stroke();
      // 제자리 — 발밑 고정
      ctx.beginPath(); ctx.moveTo(cx - 14, cy + 24); ctx.lineTo(cx + 14, cy + 24); ctx.stroke();
      ctx.globalAlpha = 1;

      /* 계속 도는 것 — 숫자만 떠오른다. 점선 테두리 = 보이지 않는 행위 */
      for (let i = 0; i < 3; i++) {
        const u = frac(t / RISE + i / 3);
        const y = lerp(cy - 26, cy - 78, u);
        ctx.globalAlpha = k * 0.8 * Math.sin(Math.PI * u);
        ctx.textAlign = 'center';
        ctx.font = mono(700, 13); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.tick || '+', cx, y);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 오른쪽: 모닥불 곁 ────────────────────────── */
    const hx = 262, hy = 158;
    {
      const k = clamp(mk * 1.8);
      ctx.globalAlpha = k;
      ctx.textAlign = 'left';
      const s = spec.afterLabel || '';
      const fs = fit(s, 800, 11, w - midX - 20, 8);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('accent');
      ctx.fillText(s, midX + 12, 32);
      ctx.globalAlpha = 1;

      // 반경
      if (mk > 0.5) {
        const rk = clamp((mk - 0.5) / 0.5);
        ctx.globalAlpha = rk * 0.9;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.setLineDash([6, 6]);
        ctx.beginPath(); ctx.arc(hx, hy, 50 * rk, 0, Math.PI * 2); ctx.stroke();
        ctx.setLineDash([]);
        ctx.globalAlpha = 1;
      }

      // 모닥불 — 계속 흔들리는 세 줄기
      ctx.globalAlpha = clamp(mk * 2);
      setShadow(ctx, GLOW, 12, 0);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      for (let i = 0; i < 3; i++) {
        const ph = frac(t / FLAME + i / 3) * Math.PI * 2;
        const bx = hx - 8 + i * 8;
        ctx.beginPath();
        ctx.moveTo(bx, hy + 10);
        ctx.quadraticCurveTo(bx + Math.sin(ph) * 5, hy, bx + Math.sin(ph) * 3, hy - 12 - i % 2 * 4);
        ctx.stroke();
      }
      clearShadow(ctx);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(hx - 15, hy + 13); ctx.lineTo(hx + 15, hy + 13); ctx.stroke();
      ctx.globalAlpha = 1;

      if (mk > 0.2) {
        ctx.globalAlpha = clamp((mk - 0.2) * 3);
        ctx.textAlign = 'center';
        ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.anchor || '모닥불', hx, hy + 30);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 주민 넷 — 몰렸다가 흩어진다 ──────────────── */
    const nV = spec.villagers || 4;
    const p1 = ease(clamp(mk / 0.55));
    const p2 = ease(clamp((mk - 0.55) / 0.45));
    for (let i = 0; i < nV; i++) {
      const a = -Math.PI / 2 + (i / nV) * Math.PI * 2 + 0.4;
      const sx = midX + 16 + (i % 2) * 18, sy = 210 + Math.floor(i / 2) * 26;
      const tx = hx + 2, ty = hy + 34;                                  // 같은 타일
      const rx = hx + Math.cos(a) * 42, ry = hy + Math.sin(a) * 42 * 0.86;

      const ax = lerp(lerp(sx, tx, p1), rx, p2);
      const ay = lerp(lerp(sy, ty, p1), ry, p2);

      ctx.globalAlpha = clamp(mk * 2.2);
      ctx.lineWidth = 3;
      ctx.strokeStyle = p2 > 0.4 ? tone('ink') : tone('sub');
      ctx.beginPath(); ctx.arc(ax, ay, 9, 0, Math.PI * 2); ctx.stroke();
      ctx.globalAlpha = 1;
    }

    // 예약 충돌 — 한 타일에 몰린 동안만
    {
      const clash = clamp(p1 * 2 - 1) * (1 - clamp(p2 * 2.2));
      if (clash > 0.03) {
        const cx = hx + 2, cy = hy + 34, r = 11;
        ctx.globalAlpha = clash;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
        ctx.beginPath();
        ctx.moveTo(cx - r, cy - r); ctx.lineTo(cx + r, cy + r);
        ctx.moveTo(cx + r, cy - r); ctx.lineTo(cx - r, cy + r);
        ctx.stroke();
        ctx.textAlign = 'center';
        const s = spec.clashLabel || '';
        const fs = fit(s, 800, 11, w - midX - 16, 8);
        ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
        ctx.fillText(s, (midX + w) / 2, 246);
        ctx.globalAlpha = 1;
      }
    }

    // 산개
    if (p2 > 0.35 && spec.fixLabel) {
      const k = clamp((p2 - 0.35) / 0.5);
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const s = spec.fixLabel;
      const fs = fit(s, 900, 13, w - midX - 16, 9);
      ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
      ctx.fillText(s, (midX + w) / 2, 246);
      ctx.globalAlpha = 1;
    }

    /* ── 아래 한 줄 ───────────────────────────────── */
    if (mk > 0.7 && spec.pointLabel) {
      const k = clamp((mk - 0.7) / 0.3);
      ctx.globalAlpha = k;
      ctx.textAlign = 'left';
      const fs = fit(spec.pointLabel, 800, 12.5, w - 16, 9);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(spec.pointLabel, 8, 282);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
