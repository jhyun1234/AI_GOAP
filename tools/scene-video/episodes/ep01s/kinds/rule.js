import {
  disp,
  ease, easeOut, clamp, lerp, spring, setShadow, clearShadow, GLOW,
  fitCanvas, mkCanvas, tone, roundRect
} from '../../../engine/lib.js';

/* rule — 막혀 있는 길 하나. 아주 짧은 샷(자막 2줄)용이다.

   쇼츠에서 리듬은 긴 샷을 잘 만드는 것보다 짧은 샷을 끼워 넣는 데서 나온다.
   앞뒤가 15초짜리 샷이라 여기 5초짜리가 하나 들어가면 그 자체가 박자가 된다.

   그림: 예산을 두 배로 올리는 화살표가 그려지고, 그 위로 굵은 선이 그어진다.
   "규칙으로 막았다"를 말로만 하면 남는 게 없다 — 막힌 길이 눈에 보여야 한다. */

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, cue, nLines }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const cy = h * 0.44;
    const boxH = 62, boxW = w * 0.40;
    const show = ease(cue(spec.showCue ?? 0, 0.25, 0.5));
    const strike = easeOut(clamp(cue(spec.strikeCue ?? nLines - 1, 0.2, 0.5)));

    const boxes = [
      { x: 0, label: spec.from || '지금', on: true },
      { x: w - boxW, label: spec.to || '두 배', on: false }
    ];

    ctx.textAlign = 'center';
    boxes.forEach((b, i) => {
      const k = i === 0 ? 1 : show;
      ctx.globalAlpha = 0.2 + 0.8 * k;
      ctx.lineWidth = 3;
      ctx.strokeStyle = strike > 0.4 && i === 1 ? tone('track') : tone('ink');
      ctx.fillStyle = '#000000';
      roundRect(ctx, b.x + 1.5, cy - boxH / 2, boxW - 3, boxH, 4);
      ctx.fill(); ctx.stroke();
      ctx.fillStyle = strike > 0.4 && i === 1 ? tone('sub') : tone('ink');
      ctx.font = disp(900, 20);
      ctx.fillText(b.label, b.x + boxW / 2, cy + 8);
      ctx.globalAlpha = 1;
    });

    /* 화살표 — 두 상자 사이 */
    const ax0 = boxW + 10, ax1 = w - boxW - 10;
    const ak = easeOut(clamp(show * 1.2));
    ctx.globalAlpha = (0.15 + 0.85 * ak) * (1 - strike * 0.55);
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineCap = 'round';
    const tip = lerp(ax0, ax1, ak);
    ctx.beginPath(); ctx.moveTo(ax0, cy); ctx.lineTo(tip, cy); ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(tip - 8, cy - 6); ctx.lineTo(tip, cy); ctx.lineTo(tip - 8, cy + 6);
    ctx.stroke();
    ctx.globalAlpha = 1;

    /* 금지 — 화살표를 가로지른다. 색이 아니라 획으로 막는다 */
    if (strike > 0.02) {
      // 스프링으로 박힌다 — 금지 표시는 '나타나는' 것보다 '찍히는' 게 맞다
      const r = 21 * spring(strike), mx = (ax0 + ax1) / 2;
      ctx.globalAlpha = strike;
      setShadow(ctx, GLOW, 24, 0);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 5;
      ctx.beginPath(); ctx.arc(mx, cy, r, 0, 7); ctx.stroke();
      clearShadow(ctx);
      const d = 21 * 0.66 * easeOut(strike);
      ctx.beginPath();
      ctx.moveTo(mx - d, cy - d); ctx.lineTo(mx + d, cy + d);
      ctx.moveTo(mx + d, cy - d); ctx.lineTo(mx - d, cy + d);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* 규칙 문장 — 화면 아래. 그림이 먼저 서고 문장이 이름표를 단다 */
    if (spec.label) {
      const lk = ease(cue(spec.strikeCue ?? nLines - 1, 0.05, 0.75));
      ctx.globalAlpha = lk;
      ctx.fillStyle = lk > 0.4 ? tone('accent') : tone('sub');
      ctx.font = disp(800, 15);
      ctx.fillText(spec.label, w / 2, h - 12);
      ctx.globalAlpha = 1;
    }
    ctx.textAlign = 'left';
  }
};
