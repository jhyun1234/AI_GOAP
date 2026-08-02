import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* ladder — 아래 칸이 안정된 걸 보고 그 위에 쌓는다.

   W0 부터 W8 까지 아홉 칸이 아래에서 위로 하나씩 켜진다(climbCue). 그다음 stackCue 에서
   같은 순서로 각 칸 오른쪽에 안정 표식이 찍힌다 — 자막 두 줄이 각각 '오른다'와 '확인하고
   쌓는다'를 말하므로, 화면도 두 번 올라간다.

   🔴 두 번 올라가게 만든 이유. 한 번에 칸과 표식을 같이 켜면 두 번째 자막이 화면에서
   아무 일도 일으키지 못한다. 원문 31행이 말하는 것은 '순서'가 아니라 '확인'이라, 확인이
   따로 한 번 더 올라가는 편이 문장에 맞는다.

   회차의 기본 도형 안에서 이 그림의 몫은 '아래가 굳어야 위가 선다'.
   계속 도는 것 = 두 세로 레일을 타고 계속 위로 흐르는 빛 띠. */

const RISE = 3.8;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const steps = spec.steps || [];
    const n = steps.length || 9;

    const ck = ease(cue(spec.climbCue ?? 0, 0.15, 0.75));
    const sk = ease(cue(spec.stackCue ?? 1, 0.15, 0.75));

    ctx.textBaseline = 'alphabetic';

    const railL = 42, railR = w - 40;
    const rungH = 22, pitch = 30, bottom = 288;
    const topY = bottom - (n - 1) * pitch - rungH;

    // 레일
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(railL - 4, bottom); ctx.lineTo(railL - 4, topY - 6);
    ctx.moveTo(railR + 4, bottom); ctx.lineTo(railR + 4, topY - 6);
    ctx.stroke();

    /* 계속 도는 것 — 레일을 타고 위로 흐르는 띠 */
    {
      const u = frac(t / RISE);
      const y = lerp(bottom, topY - 6, u);
      ctx.globalAlpha = 0.65 * Math.sin(Math.PI * u);
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.moveTo(railL - 4, y); ctx.lineTo(railL - 4, y - 22);
      ctx.moveTo(railR + 4, y); ctx.lineTo(railR + 4, y - 22);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    for (let i = 0; i < n; i++) {
      const y = bottom - i * pitch - rungH;
      const vis = clamp(ck * (n + 1) - i);
      if (vis < 0.02) continue;
      const ok = clamp(sk * (n + 1) - i);

      // 칸
      const s = Math.min(1, spring(vis));
      const bw = (railR - railL) * s, bh = rungH * s;
      const bxx = railL + ((railR - railL) - bw) / 2, byy = y + (rungH - bh) / 2;
      ctx.globalAlpha = clamp(vis * 1.6);
      if (ok > 0.5) setShadow(ctx, GLOW, 10, 0);
      ctx.lineWidth = 3;
      ctx.strokeStyle = ok > 0.5 ? tone('accent') : tone('ink');
      roundRect(ctx, bxx, byy, bw, bh, 3); ctx.stroke();
      clearShadow(ctx);

      // 단계 이름
      ctx.textAlign = 'left';
      ctx.fillStyle = ok > 0.5 ? tone('accent') : tone('sub');
      ctx.font = mono(700, 12);
      ctx.fillText(steps[i] || '', 8, y + rungH / 2 + 5);

      // 안정 표식
      if (ok > 0.06) {
        const k = clamp(ok * 1.4);
        const mx = railR + 12, my = y + rungH / 2;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        setShadow(ctx, GLOW, 8, 0);
        ctx.beginPath();
        ctx.moveTo(mx, my);
        ctx.lineTo(mx + 4 * Math.min(1, k * 2), my + 4 * Math.min(1, k * 2));
        if (k > 0.5) { ctx.lineTo(mx + 4 + 8 * clamp((k - 0.5) * 2), my + 4 - 8 * clamp((k - 0.5) * 2)); }
        ctx.stroke();
        clearShadow(ctx);
      }
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
