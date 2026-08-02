import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* godfile — 파일 하나가 전부를 혼자 안다.

   가운데 세로 기둥 하나가 파일이고, 왼쪽에서 다섯 갈래(원문 57행이 이름을 대는 액션 계열)가
   전부 이 기둥 하나로 들어온다. countCue 에서 기둥 안이 아래에서 위로 차오르고 오른쪽
   눈금이 0 에서 3,476 까지 굴러 올라간다 — 자막이 숫자를 부르는 동안 화면은 그 숫자가
   '쌓이는' 것을 보여준다. shakeCue 에서 기둥 한 곳을 건드리자 기둥도 다섯 갈래도 같이 흔들린다.

   회차의 기본 도형 안에서 이 그림의 몫은 '한 덩이가 전부를 진다'.
   계속 도는 것 = 기둥 안을 위로 흘러가는 코드 줄. */

const FLOW = 4.2;

function comma(v) {
  const s = String(v); let out = '';
  for (let i = 0; i < s.length; i++) {
    if (i > 0 && (s.length - i) % 3 === 0) out += ',';
    out += s[i];
  }
  return out;
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const arms = spec.arms || [];
    const target = spec.lines ?? 3476;

    const ak = ease(cue(spec.armCue ?? 0, 0.15, 0.7));
    const nk = ease(cue(spec.countCue ?? 1, 0.15, 0.6));
    const sk = ease(cue(spec.shakeCue ?? 2, 0.15, 0.45));

    ctx.textBaseline = 'alphabetic';

    const barW = 54, barX = 92, barTop = 60, barBot = 246;
    const fillY = lerp(barBot - 6, barTop + 6, ease(nk));

    // 흔들림 — 손대면 전체가 흔들린다
    const sway = Math.sin(frac(t / 0.42) * Math.PI * 2) * 3.4 * sk;

    /* ── 들어오는 갈래 ─────────────────────────────── */
    ctx.textAlign = 'right';
    arms.forEach((label, i) => {
      const vis = clamp(ak * (arms.length + 1) - i);
      if (vis < 0.02) return;
      const y = 84 + i * 33;
      ctx.globalAlpha = clamp(vis * 1.6) * 0.9;
      ctx.fillStyle = tone('sub'); ctx.font = disp(800, 11);
      ctx.fillText(label, 58, y + 4);
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      const hx = lerp(64, barX + sway, clamp(vis * 1.4));
      ctx.beginPath(); ctx.moveTo(64, y); ctx.lineTo(hx, y); ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(hx, y); ctx.lineTo(hx - 6, y - 4); ctx.moveTo(hx, y); ctx.lineTo(hx - 6, y + 4);
      ctx.stroke();
      ctx.globalAlpha = 1;
    });

    /* ── 파일 기둥 ─────────────────────────────────── */
    ctx.save();
    ctx.translate(sway, 0);

    ctx.globalAlpha = clamp(ak * 2.5);
    ctx.fillStyle = '#000';
    roundRect(ctx, barX, barTop, barW, barBot - barTop, 4); ctx.fill();
    ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
    roundRect(ctx, barX, barTop, barW, barBot - barTop, 4); ctx.stroke();

    /* 계속 도는 것 — 차 있는 부분 안을 위로 흐르는 코드 줄 */
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    const step = 11;
    const off = frac(t / FLOW) * step;
    for (let y = barBot - 8 - off; y > fillY; y -= step) {
      if (y > barBot - 6) continue;
      ctx.beginPath();
      ctx.moveTo(barX + 8, y);
      ctx.lineTo(barX + 8 + 12 + ((Math.round(y) * 37) % 23), y);
      ctx.stroke();
    }

    // 차오른 높이의 윗면
    if (nk > 0.02) {
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(barX + 3, fillY); ctx.lineTo(barX + barW - 3, fillY); ctx.stroke();
    }

    // 손댄 자리 — shakeCue 에서 기둥 가운데를 가로지르는 표식
    if (sk > 0.05) {
      const ty = lerp(barBot, barTop, 0.55);
      ctx.globalAlpha = clamp(sk * 2);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      setShadow(ctx, GLOW, 12, 0);
      ctx.beginPath(); ctx.moveTo(barX - 7, ty); ctx.lineTo(barX + barW + 7, ty); ctx.stroke();
      clearShadow(ctx);
    }
    ctx.globalAlpha = 1;
    ctx.restore();

    // 파일 이름
    ctx.globalAlpha = clamp(ak * 2.5);
    ctx.textAlign = 'center';
    ctx.fillStyle = tone('ink'); ctx.font = mono(700, 12);
    ctx.fillText(spec.name || '', barX + barW / 2 + sway, barTop - 12);
    ctx.globalAlpha = 1;

    /* ── 줄 수 눈금 ────────────────────────────────── */
    {
      const v = Math.round(target * ease(nk));
      ctx.textAlign = 'left';
      ctx.globalAlpha = clamp(nk * 2.5);
      ctx.fillStyle = tone('sub'); ctx.font = disp(800, 10.5);
      ctx.fillText(spec.countLabel || '파일 하나에', 176, 132);
      setShadow(ctx, GLOW, 14, 0);
      ctx.fillStyle = tone('accent'); ctx.font = disp(900, 36);
      ctx.fillText(comma(v), 176, 166);
      clearShadow(ctx);
      const nwid = ctx.measureText(comma(v)).width;
      ctx.fillStyle = tone('sub'); ctx.font = disp(800, 13);
      ctx.fillText(spec.unit || '줄', 180 + nwid, 166);
      ctx.globalAlpha = 1;
    }

    /* ── 용어 ─────────────────────────────────────── */
    if (sk > 0.1) {
      const k = clamp((sk - 0.1) / 0.5);
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const term = spec.term || '';
      let fs = 13; ctx.font = disp(900, fs);
      while (fs > 9 && ctx.measureText(term).width > w - 56) { fs -= 0.5; ctx.font = disp(900, fs); }
      const tw = ctx.measureText(term).width;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      roundRect(ctx, w / 2 - tw / 2 - 13, 268, tw + 26, 28, 3); ctx.stroke();
      ctx.fillStyle = tone('ink');
      ctx.fillText(term, w / 2, 287);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
