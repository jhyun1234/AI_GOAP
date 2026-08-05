import {
  disp, ease, clamp, frac,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* howwhat — 성격이 닿은 축 하나, 안 닿은 축 하나.

   원문의 진단은 한 문장이다: "성격은 '어떻게 일하느냐'만 바꿨지, '무슨 일을 하느냐'는
   바꾸지 않았던 겁니다." 그래서 이 그림은 **선을 하나만 긋는다.**

   🔴 안 닿은 축을 취소선이나 X 로 그리지 않는다. 지워진 게 아니라 **애초에 없는** 것이라,
   WHAT 축 왼쪽 끝에는 아무것도 안 꽂힌 빈 고리만 남는다. 무엇이 없는지를 그리는 가장
   정직한 방법은 빈 자리를 보여주는 것이다.

   🔴 그렇다고 WHAT 축을 통째로 정지시키면 뜻은 맞지만 정적 구간이 된다. 그래서 축의
   점선은 계속 흐르되 **아무것도 새로 생기지 않는다** — 돌아가고는 있는데 개입이 없다는
   것이 원문이 말한 상태 그대로다.

   🔴 등장은 전부 cue 에 물렸다. t 로 도는 것은 HOW 축 표식 셋의 흔들림과 두 축의 dash
   흐름뿐이고 문턱을 가지지 않는다. */

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

    const axes = spec.axes ?? ['HOW', 'WHAT'];
    const reached = spec.reached ?? 0;
    const hk = ease(cue(spec.howCue ?? 0, 0.15, 0.7));
    const gk = ease(cue(spec.gapCue ?? 1, 0.15, 0.75));

    const KX = 48, KY = 140, KR = 17;      // 성격 노브
    const AX0 = 96, AX1 = w - 22;
    const ayOf = i => i === 0 ? 92 : 190;

    /* ── 성격 노브 ─────────────────────────────────── */
    const nk = clamp(hk * 3);
    if (nk > 0.02) {
      ctx.globalAlpha = nk;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(KX, KY, KR, 0, Math.PI * 2); ctx.stroke();

      // 노브 안 눈금 하나가 계속 돈다 — 성격은 살아 있다
      const a = frac(t / 4.4) * Math.PI * 2;
      ctx.beginPath();
      ctx.moveTo(KX, KY);
      ctx.lineTo(KX + Math.cos(a) * (KR - 5), KY + Math.sin(a) * (KR - 5));
      ctx.stroke();

      ctx.textAlign = 'center';
      ctx.font = disp(800, 12); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.traitLabel ?? 'TRAIT', KX, KY + KR + 20);
      ctx.globalAlpha = 1;
    }

    /* ── 축 둘 ─────────────────────────────────────── */
    for (let i = 0; i < axes.length; i++) {
      const y = ayOf(i);
      const on = i === reached;
      const a = on ? clamp(hk * 2.4) : clamp(Math.max(hk, gk) * 2.4);
      if (a <= 0.02) continue;

      ctx.globalAlpha = a;
      ctx.setLineDash([12, 9]);
      ctx.lineDashOffset = -(t * 11) % 21;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(AX0, y); ctx.lineTo(AX1, y); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;

      ctx.textAlign = 'left';
      ctx.font = disp(900, 15);
      ctx.fillStyle = on ? tone('accent') : tone('sub');
      ctx.fillText(axes[i], AX0 + 2, y - 14);
      ctx.globalAlpha = 1;
    }

    /* ── 닿은 축 — 노브에서 선이 뻗고 표식 셋이 흔들린다 ── */
    const ry = ayOf(reached);
    const lk = ease(clamp((hk - 0.2) / 0.6));
    if (lk > 0.02) {
      ctx.globalAlpha = lk;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.moveTo(KX + KR, KY);
      ctx.quadraticCurveTo(AX0 - 22, KY - 30, AX0, ry);
      ctx.stroke();
      ctx.globalAlpha = 1;

      // 흔들리는 표식 셋 — 성격마다 다른 폭
      const AMP = [17, 26, 11], PER = [3.1, 3.9, 2.6];
      const base = [0.28, 0.55, 0.82];
      for (let j = 0; j < 3; j++) {
        const k = clamp(lk * 3 - j * 0.6);
        if (k <= 0.02) continue;
        const bx = AX0 + (AX1 - AX0) * base[j];
        const x = bx + Math.sin(frac(t / PER[j]) * Math.PI * 2) * AMP[j];
        ctx.globalAlpha = k;
        setShadow(ctx, GLOW, 7, 0);
        ctx.fillStyle = tone('accent');
        ctx.beginPath(); ctx.arc(x, ry, 6, 0, Math.PI * 2); ctx.fill();
        clearShadow(ctx);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 안 닿은 축 — 아무것도 안 꽂힌 빈 고리 ────────── */
    const gy = ayOf(reached === 0 ? 1 : 0);
    if (gk > 0.02) {
      ctx.globalAlpha = gk;
      ctx.setLineDash([4, 5]);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(AX0, gy, 8, 0, Math.PI * 2); ctx.stroke();
      ctx.setLineDash([]);

      // 노브에서 여기까지 갈 수 있었던 자리 — 선은 없고 끝점만 있다
      ctx.beginPath(); ctx.arc(KX + KR + 6, KY + 12, 3.5, 0, Math.PI * 2);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 아래 한 줄 ────────────────────────────────── */
    if (spec.note) {
      const k = clamp((gk - 0.6) / 0.4);
      if (k > 0.02) {
        ctx.globalAlpha = k;
        ctx.textAlign = 'center';
        const fs = fit(spec.note, 900, 14, w - 30, 10);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('ink');
        ctx.fillText(spec.note, w / 2, 268);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
