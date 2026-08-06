import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* tighten — 앞 샷에서 남아돌던 여유를 값 셋으로 조인다.

   원문: "그래서 겨울을 3일에서 4일로 늘리고, 배고픔 감쇠를 1.5배에서 1.75배로 올리고,
   시작 식량을 줄이고, 주민 수를 5명에서 10명으로 늘렸습니다." /
   "「방치했을 때 안락하면 그 밸런스는 실패로 본다」는 기준을 프로젝트 규칙에 새로 못박은 것입니다."

   🔴 위쪽 띠는 앞 샷(surplus)의 남는 절반이 그대로 넘어온 것이다. 값이 하나씩 앉을 때마다
   띠가 한 단씩 줄고, 셋이 다 앉으면 아슬아슬하게 남는다. **줄어든 띠에는 숫자를 안 붙였다** —
   조정 뒤의 수지는 원문에 없다. 길이는 '조여졌다'는 방향만 뜻하지 값을 주장하지 않는다.

   🔴 '시작 식량을 줄이고' 는 행에 없다. 원문에 **전후 값이 없어서**다. 셋만 그린 이유가
   그것이고, 없는 값을 채워 네 줄을 맞추지 않았다.

   🔴 아래 규칙 한 줄은 화면에만 있다. 나레이션은 그 문장을 한 번도 읽지 않는다 —
   같은 말을 두 층으로 하면 화면이 자막을 옮겨 적는 것이 된다. */

const HATCH = 16;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const fit = (txt, weight, start, max, min = 8) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const tk = ease(cue(spec.tightenCue ?? 0, 0.15, 0.75));
    const rk = ease(cue(spec.ruleCue ?? 1, 0.15, 0.55));

    const X0 = 16, X1 = w - 16, FULL = X1 - X0;
    const BY = 34, BH = 30;
    const bandW = FULL * lerp(1, 0.20, ease(tk));

    /* ── 남아 있는 여유 ─────────────────────────── */
    ctx.globalAlpha = 1;
    ctx.textAlign = 'left';
    ctx.font = disp(800, 11.5); ctx.fillStyle = tone('accent');
    ctx.fillText(spec.marginLabel ?? '', X0, 26);

    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
    roundRect(ctx, X0, BY, Math.max(10, bandW), BH, 5); ctx.stroke();

    ctx.save();
    roundRect(ctx, X0 + 1, BY + 1, Math.max(8, bandW - 2), BH - 2, 4); ctx.clip();
    ctx.globalAlpha = 0.85;
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
    const off = (t * 14) % HATCH;
    for (let g = -BH; g < bandW + BH; g += HATCH) {
      ctx.beginPath();
      ctx.moveTo(X0 + g + off, BY + BH); ctx.lineTo(X0 + g + off + BH, BY);
      ctx.stroke();
    }
    ctx.restore();
    ctx.globalAlpha = 1;

    /* 조여진 뒤 — 띠 끝에서 표식이 계속 흔들린다(아슬아슬한 상태) */
    if (tk > 0.55) {
      const wob = Math.sin(t * Math.PI * 2 / 1.7) * 4;
      ctx.globalAlpha = clamp((tk - 0.55) * 3);
      ctx.fillStyle = tone('ink');
      ctx.beginPath();
      ctx.arc(X0 + bandW + 14 + wob, BY + BH / 2, 5.5, 0, Math.PI * 2);
      ctx.fill();
      ctx.globalAlpha = 1;
    }

    /* ── 조인 값 셋 ─────────────────────────────── */
    const rows = spec.rows ?? [];
    for (let i = 0; i < rows.length; i++) {
      const y = 112 + i * 46;
      const k = ease(clamp((tk - i * 0.18) / 0.46));
      const a = clamp(tk * 3 - i * 0.5);
      if (a <= 0.02) continue;

      ctx.globalAlpha = a * 0.55;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(X0, y + 9); ctx.lineTo(X1, y + 9); ctx.stroke();
      ctx.globalAlpha = 1;

      ctx.globalAlpha = a;
      ctx.textAlign = 'left';
      const lf = fit(rows[i].label ?? '', 800, 11.5, 150, 8);
      ctx.font = disp(800, lf); ctx.fillStyle = tone('sub');
      ctx.fillText(rows[i].label ?? '', X0, y);

      ctx.textAlign = 'right';
      ctx.font = disp(900, 17); ctx.fillStyle = tone('ink');
      ctx.globalAlpha = a * lerp(1, 0.5, k);
      ctx.fillText(String(rows[i].from ?? ''), 212, y);
      ctx.globalAlpha = a;

      /* 화살표가 k 만큼 자란다 */
      const ax0 = 220, ax1 = 250, ay = y - 6;
      const ax = lerp(ax0, ax1, k);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(ax0, ay); ctx.lineTo(ax, ay); ctx.stroke();
      if (k > 0.75) {
        ctx.fillStyle = tone('accent');
        ctx.beginPath();
        ctx.moveTo(ax1 + 3, ay); ctx.lineTo(ax1 - 6, ay - 5); ctx.lineTo(ax1 - 6, ay + 5);
        ctx.closePath(); ctx.fill();
      }

      /* 새 값이 앉는다 */
      if (k > 0.6) {
        const s = clamp((k - 0.6) / 0.4);
        ctx.globalAlpha = a * s;
        ctx.textAlign = 'left';
        setShadow(ctx, GLOW, 9, 0);
        ctx.font = disp(900, 20); ctx.fillStyle = tone('accent');
        ctx.fillText(String(rows[i].to ?? ''), 260, y + (1 - s) * -7);
        clearShadow(ctx);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 새로 못박은 기준 ───────────────────────── */
    if (rk > 0.02) {
      const RY = 248;
      ctx.globalAlpha = clamp(rk * 2.4);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.setLineDash([9, 7]);
      ctx.lineDashOffset = -(t * 13) % 16;
      ctx.beginPath(); ctx.moveTo(X0, RY); ctx.lineTo(X0 + FULL * ease(rk), RY); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;

      const txt = spec.ruleText ?? '';
      ctx.textAlign = 'center';
      const fs = fit(txt, 900, 17, w - 40, 10);
      ctx.font = disp(900, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(txt, w / 2, RY + 26 + (1 - ease(rk)) * -6);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
