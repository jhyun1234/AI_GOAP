import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* shortfall — 같은 자 위의 두 값.

   막대 둘을 나란히 세우면 "왼쪽이 크다"가 되고, 두 값이 서로 다른 것을 잰 것처럼 보인다.
   원문이 말하는 건 그게 아니다 — **같은 행동의 같은 비용**을 하나는 150으로, 하나는 9로
   봤다는 것이다. 그래서 자를 하나만 세우고 그 위에 표식 둘을 올렸다.
   9 는 자의 6% 지점이라 눈금에 붙어 있고, 그 사이가 통째로 빈다. 그 빈 구간이 50배다.

   회차의 기본 도형 안에서 이 그림의 몫은 '자 위에서 6%에 멈춘 획'이다.
   계속 도는 것 = 자를 오르내리는 값 읽기 표식. */

const CURSOR = 3.6;   // 읽기 표식이 자를 한 번 왕복하는 초

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const AX = 96;                 // 자의 x
    const y0 = h - 52, y1 = 28;    // 0 과 max 의 y
    const max = spec.max || 100;
    const yv = v => y0 - (clamp(v / max, 0, 1)) * (y0 - y1);

    const real = spec.real || {}, guess = spec.guess || {};
    const k1 = ease(cue(real.cue ?? 1, real.lead ?? 1.8, 0.62));
    const k2 = ease(cue(guess.cue ?? 2, 0.15, 0.6));
    const k3 = ease(cue(spec.ratioCue ?? 3, 0.2, 0.5));

    ctx.textBaseline = 'alphabetic';
    ctx.textAlign = 'left';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
    ctx.fillText(spec.axisLabel || '', 12, 18);

    // 자
    ctx.lineWidth = 3; ctx.lineCap = 'butt';
    ctx.strokeStyle = tone('track');
    ctx.beginPath(); ctx.moveTo(AX, y0); ctx.lineTo(AX, y1); ctx.stroke();

    const step = spec.tickStep || 50;
    ctx.font = disp(700, 10);
    for (let v = 0; v <= max + 0.001; v += step) {
      const y = yv(v);
      ctx.strokeStyle = tone('ink');
      ctx.beginPath(); ctx.moveTo(AX - 7, y); ctx.lineTo(AX + 7, y); ctx.stroke();
      ctx.textAlign = 'right';
      ctx.fillStyle = tone('sub');
      // 🔴 눈금 숫자는 AX-28 에서 끝난다. 읽기 표식이 지나는 열(AX-24~AX-9)과 겹치지 않게
      //    비워 둔 자리다 — 겹치면 50·100 을 지날 때 숫자가 흐려진다.
      ctx.fillText(String(Math.round(v)), AX - 28, y + 4);
    }

    /* ── 계속 도는 것 ─────────────────────────────
       값을 읽는 자리가 자를 따라 오르내린다. 두 표식이 놓이기 전부터 자는 이미 읽히고 있다. */
    {
      const u = frac(t / CURSOR);
      const tri = u < 0.5 ? u * 2 : 2 - u * 2;
      const cy2 = lerp(y0, y1, tri);
      ctx.globalAlpha = 0.55;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(AX - 24, cy2); ctx.lineTo(AX - 12, cy2); ctx.stroke();
      ctx.fillStyle = tone('ink');
      ctx.beginPath();
      ctx.moveTo(AX - 9, cy2); ctx.lineTo(AX - 20, cy2 - 6); ctx.lineTo(AX - 20, cy2 + 6);
      ctx.closePath(); ctx.fill();
      ctx.globalAlpha = 1;
    }

    /* ── 표식 둘 ──────────────────────────────────
       올라가는 길이가 곧 값이다. 숫자도 같이 올라간다. */
    const mark = (v, k, col, label, big) => {
      if (k <= 0.01) return yv(0);
      const cur = v * k;
      const y = yv(cur);
      ctx.globalAlpha = clamp(k * 1.4);
      ctx.lineWidth = 3; ctx.strokeStyle = col;
      if (col === tone('accent')) setShadow(ctx, GLOW, 12, 0);
      ctx.beginPath(); ctx.moveTo(AX, y); ctx.lineTo(w - 16, y); ctx.stroke();
      clearShadow(ctx);
      ctx.textAlign = 'left';
      ctx.fillStyle = col; ctx.font = disp(900, big ? 28 : 24);
      ctx.fillText(String(Math.round(cur)), AX + 16, y + 10);
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
      ctx.fillText(label || '', AX + 16, y + 26);
      ctx.globalAlpha = 1;
      return yv(v);
    };

    const yReal = mark(real.value || 0, k1, tone('ink'), real.label, true);
    const yGuess = mark(guess.value || 0, k2, tone('accent'), guess.label, false);

    /* ── 못 본 구간 ───────────────────────────────
       두 표식 사이를 묶는다. 이 회차에서 초록은 '화면이 지금 가리키는 지점'이다. */
    if (k3 > 0.02) {
      const bx = w - 16;                     // 두 표식 선이 끝나는 자리 = 묶는 자리
      const yA = yReal, yB = yGuess;
      const yMid = lerp(yB, yA, ease(k3));
      ctx.globalAlpha = clamp(k3 * 1.4);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
      ctx.beginPath();
      ctx.moveTo(bx, yB); ctx.lineTo(bx, yMid);
      ctx.stroke();
      // 위아래 끝을 안쪽으로 꺾어 구간임을 못박는다
      ctx.beginPath();
      ctx.moveTo(bx, yB); ctx.lineTo(bx - 9, yB);
      if (k3 > 0.9) { ctx.moveTo(bx, yA); ctx.lineTo(bx - 9, yA); }
      ctx.stroke();

      ctx.textAlign = 'right';
      ctx.fillStyle = tone('accent'); ctx.font = disp(900, 22);
      ctx.fillText(spec.ratio || '', bx - 14, (yA + yB) / 2 + 2);
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
      ctx.fillText(spec.ratioLabel || '', bx - 14, (yA + yB) / 2 + 18);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
