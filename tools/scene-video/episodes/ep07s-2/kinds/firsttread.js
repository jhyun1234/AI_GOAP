import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* firsttread — 밟힌 자리가 넓어지고, 그 끝에서 사건이 난다.

   원문: "주민들이 직업 따라 마을 구석구석으로 흩어지기 시작하니까, 예전엔 아무도 안
   지나가던 대각선 좁은 길까지 실제로 밟게 됐고, 그제서야 숨어 있던 버그가 드러난 겁니다."

   🔴 앞편(ep07s-1)의 eachday 와 겹치지 않게 **세는 대상을 바꿨다.** 저 편은 사람 여덟이
   저마다 다른 자리로 가는 그림이었고, 여기서 번지는 것은 **밟힌 자리(커버리지)** 다.
   사건도 번짐 자체가 아니라 번짐이 **구석에 처음 닿는 순간**이다 — 그 한 걸음에 tick 이 붙는다.

   spreadCue 에서 가운데 작은 원이던 커버가 화면 구석까지 자라고, treadCue 에서 오른쪽 아래
   구석의 대각선 길에 표식이 닿으면서 그 자리가 켜진다.

   🔴 발자국 표식에는 이름도 번호도 안 붙였다. 개수가 원문에 없는 수라, 세어야 뜻이 생기면
   지어낸 수치가 된다. */

const MARKS = [
  [0.22, 0.30, 2.4], [0.52, 0.22, 3.0], [0.78, 0.34, 3.6],
  [0.18, 0.62, 4.0], [0.46, 0.58, 2.8], [0.70, 0.66, 4.6], [0.34, 0.80, 3.3]
];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const fit = (txt, weight, start, max, min = 8) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const sk = ease(cue(spec.spreadCue ?? 0, 0.15, 0.7));
    const tk = ease(cue(spec.treadCue ?? 1, 0.15, 0.8));

    const MX0 = 26, MY0 = 52, MX1 = w - 26, MY1 = 236;
    const CX = (MX0 + MX1) / 2, CY = (MY0 + MY1) / 2;

    /* ── 마을 테두리 ───────────────────────────────── */
    ctx.globalAlpha = 0.9;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.rect(MX0, MY0, MX1 - MX0, MY1 - MY0); ctx.stroke();
    ctx.globalAlpha = 1;

    ctx.textAlign = 'left';
    ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
    if (spec.mapLabel) ctx.fillText(spec.mapLabel, 8, 28);

    /* ── 밟힌 자리 — 가운데 원에서 구석까지 번진다 ──── */
    const rx = lerp(38, (MX1 - MX0) / 2 - 4, ease(sk));
    const ry = lerp(30, (MY1 - MY0) / 2 - 4, ease(sk));
    ctx.globalAlpha = 0.85;
    ctx.setLineDash([9, 7]);
    ctx.lineDashOffset = -(t * 12) % 16;
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.ellipse(CX, CY, rx, ry, 0, 0, Math.PI * 2); ctx.stroke();
    ctx.setLineDash([]); ctx.lineDashOffset = 0;
    ctx.globalAlpha = 1;

    /* ── 발자국 표식 — 각자 자기 자리에서 계속 돈다 ── */
    for (let i = 0; i < MARKS.length; i++) {
      const [fx, fy, per] = MARKS[i];
      const hx = MX0 + fx * (MX1 - MX0), hy = MY0 + fy * (MY1 - MY0);
      // 커버가 자기 자리까지 와야 보인다
      const dx = (hx - CX) / Math.max(1, rx), dy = (hy - CY) / Math.max(1, ry);
      const inside = clamp((1.06 - Math.sqrt(dx * dx + dy * dy)) * 3);
      if (inside <= 0.02) continue;

      const ang = frac(t / per) * Math.PI * 2 + i;
      const px = hx + Math.cos(ang) * 11, py = hy + Math.sin(ang) * 8;

      ctx.globalAlpha = inside;
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(px, py, 4.5, 0, Math.PI * 2); ctx.fill();
      ctx.globalAlpha = 1;
    }

    /* ── 구석의 대각선 좁은 길 ─────────────────────── */
    const DX0 = MX1 - 76, DY0 = MY1 - 26, DX1 = MX1 - 20, DY1 = MY1 - 78;
    const shown = clamp(sk * 1.6 - 0.3);
    if (shown > 0.02) {
      ctx.globalAlpha = shown;
      ctx.setLineDash([7, 6]);
      ctx.lineDashOffset = -(t * 11) % 13;
      ctx.strokeStyle = tk > 0.6 ? tone('accent') : tone('track');
      ctx.lineWidth = tk > 0.6 ? 4 : 3;
      ctx.beginPath(); ctx.moveTo(DX0, DY0); ctx.lineTo(DX1, DY1); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;
    }

    /* 표식이 그 길에 처음 닿는다 */
    const walk = ease(clamp(tk / 0.75));
    if (tk > 0.05) {
      const px = lerp(CX, DX0, walk), py = lerp(CY, DY0, walk);
      ctx.globalAlpha = clamp(tk * 3);
      setShadow(ctx, GLOW, 8, 0);
      ctx.fillStyle = tone('accent');
      ctx.beginPath(); ctx.arc(px, py, 6.5, 0, Math.PI * 2); ctx.fill();
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    /* ── 라벨 둘 ───────────────────────────────────── */
    if (spec.beforeLabel) {
      const k = clamp(1 - sk * 1.8);
      if (k > 0.02) {
        ctx.globalAlpha = k;
        ctx.textAlign = 'center';
        const fs = fit(spec.beforeLabel, 800, 12, w - 60, 9);
        ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.beforeLabel, CX, MY1 + 26);
        ctx.globalAlpha = 1;
      }
    }
    if (spec.cornerLabel) {
      const k = clamp(tk * 2 - 0.5);
      if (k > 0.02) {
        ctx.globalAlpha = k;
        ctx.textAlign = 'right';
        ctx.font = disp(900, 13); ctx.fillStyle = tone('accent');
        ctx.fillText(spec.cornerLabel, MX1, Math.min(h - 12, MY1 + 26));
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
