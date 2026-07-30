import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, depthGrad, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* budget — 4,096 은 개수가 아니라 예산이다.

   원문은 이 숫자를 두 번 부른다. 한 번은 `Nodes=4096`, 한 번은 "예산 4096을 다 써버렸습니다".
   즉 이 글에서 4,096 은 세는 값이 아니라 **한도**다. 한도가 100% 소진되는 그림은 고리다 —
   가이드 결정 트리가 '비율/달성률 → 도넛'이라고 못박아 둔 자리이고, 이 시리즈가 아직
   한 번도 안 쓴 패턴이다. 격자를 채우면 "많다"만 남지만 고리를 채우면 **다 썼다**가 남는다.

   회차의 기본 도형 안에서 이 그림의 몫은 '한 바퀴를 다 돌고도 못 닿는 획'이다.
   그래서 탐색 머리는 고리를 다 채운 뒤에도 멈추지 않고 계속 돈다 — 예산은 끝났는데 답은 없다. */

const HEAD_PERIOD = 2.3;   // 탐색 머리가 고리를 한 바퀴 도는 초

const comma = n => String(n).replace(/\B(?=(\d{3})+(?!\d))/g, ',');

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const cx = w / 2, cy = 138;
    const R = 96, TH = 24;
    const total = spec.total || 100;

    const k = ease(cue(spec.fillCue ?? 1, spec.fillLead ?? 1.7, 0.62));
    const vk = ease(cue(spec.verdictCue ?? 2, 0.15, 0.45));

    ctx.textBaseline = 'alphabetic';

    // 무엇의 예산인가 — 원문 로그의 목표 이름
    if (spec.goalLabel) {
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('sub'); ctx.font = mono(700, 11);
      ctx.fillText(spec.goalLabel, cx, 18);
    }

    // 빈 고리
    ctx.lineCap = 'butt'; ctx.lineWidth = TH;
    ctx.strokeStyle = tone('track');
    ctx.beginPath(); ctx.arc(cx, cy, R, 0, Math.PI * 2); ctx.stroke();

    // 소진된 만큼
    const A0 = -Math.PI / 2;
    if (k > 0.002) {
      setShadow(ctx, GLOW, 16, 0);
      ctx.strokeStyle = depthGrad(ctx, cx - R, cy - R, cx + R, cy + R, 'accent');
      ctx.beginPath(); ctx.arc(cx, cy, R, A0, A0 + Math.PI * 2 * k); ctx.stroke();
      clearShadow(ctx);
    }

    /* ── 계속 도는 것 ─────────────────────────────
       탐색 머리. 예산을 다 쓴 뒤에도 멎지 않는다 — 이 샷의 요지 그 자체다. */
    const ha = A0 + Math.PI * 2 * frac(t / HEAD_PERIOD);
    ctx.fillStyle = k > 0.995 ? tone('ink') : tone('accent');
    setShadow(ctx, k > 0.995 ? 'rgba(255,255,255,0.25)' : GLOW, 14, 0);
    ctx.beginPath(); ctx.arc(cx + Math.cos(ha) * R, cy + Math.sin(ha) * R, 9, 0, Math.PI * 2); ctx.fill();
    clearShadow(ctx);

    // 가운데 — 지금까지 몇 개나 열었나
    const shown = Math.round(total * k);
    ctx.textAlign = 'center';
    ctx.fillStyle = tone('ink'); ctx.font = disp(900, 42);
    ctx.fillText(comma(shown), cx, cy + 8);
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 12);
    ctx.fillText(spec.label || '', cx, cy + 30);

    // 다 썼다
    if (k > 0.995 && spec.fullNote) {
      ctx.fillStyle = tone('accent'); ctx.font = disp(800, 12);
      ctx.fillText(spec.fullNote, cx, cy + 50);
    }

    /* ── 결과 ─────────────────────────────────────
       예산이 끝난 뒤에야 찍힌다. 원문 로그의 Result= 를 그대로 쓴다. */
    if (vk > 0.02) {
      const label = `${spec.verdictLabel || 'Result'}=${spec.verdict || ''}`;
      const s = spring(vk);
      ctx.font = mono(700, 14);
      // 🔴 기준 폭을 0.95 로 줄여 두고 스프링을 곱한다 — 1.05 배에서 가장자리를 넘지 않게
      const bw = Math.min(ctx.measureText(label).width + 30, (w - 24) * 0.95) * s;
      const bh = 32 * s;
      const bx = cx - bw / 2, by = 264 - bh / 2;
      ctx.globalAlpha = clamp(vk * 2);
      ctx.fillStyle = '#000';
      roundRect(ctx, bx, by, bw, bh, 5); ctx.fill();
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      roundRect(ctx, bx, by, bw, bh, 5); ctx.stroke();
      ctx.fillStyle = tone('ink');
      ctx.textBaseline = 'middle';
      ctx.font = mono(700, 14 * Math.min(1, s));
      ctx.fillText(label, cx, by + bh / 2 + 1);
      ctx.textBaseline = 'alphabetic';
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
