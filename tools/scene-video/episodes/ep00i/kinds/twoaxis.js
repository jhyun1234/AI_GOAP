import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* twoaxis — 거부는 규칙이다.

   VillagerAgent.cs 1161행이 이 그림의 전부다: "거부 판정의 유일한 규칙
   (ADR-M1-2: 욕구 2축, 랜덤 없음)". 그래서 화면에 있어야 하는 것은 **축이 정확히 둘**이라는
   사실과 **문턱이 그어져 있다**는 사실뿐이다. 주사위도, 확률 표시도 없다 — 없다는 것이 요지다.

   왼쪽(포만)은 문턱 아래라서 거부, 오른쪽(피로)은 문턱 위라서 거부. 방향이 반대인 두 축이
   같은 결론에 닿는다. leverCue 에서 웃돈이 들어오면 **게이지가 아니라 문턱선이** 움직인다
   (M6-E: reward 는 거부 문턱에 오프셋을 준다 — 주민을 배부르게 만드는 게 아니다).

   회차의 기본 도형 안에서 이 그림의 몫은 '문턱은 위에서 그어지는데 결정은 아래 게이지가 한다'.
   계속 도는 것 = 두 게이지 값의 미세한 오르내림(위치 이동).

   🔴 화면에 수치를 안 찍는다. 35·70 은 처음 보는 사람에게 뜻이 없고, 두 축과 문턱만 남으면 된다. */

const BREATH = 3.1;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const ak = ease(cue(spec.axisCue ?? 1, 0.15, 0.55));
    const lk = ease(cue(spec.leverCue ?? 2, 0.12, 0.55));

    const axes = spec.axes || [];
    ctx.textBaseline = 'alphabetic';
    ctx.textAlign = 'center';

    const gTop = 74, gBot = 244, gH = gBot - gTop, gW = 44;
    /* 🔴 게이지를 오른쪽으로 몰고 웃돈을 왼쪽에 뒀다. 처음엔 반대였는데, 그러면 웃돈에서
       포만 문턱으로 가는 획이 **피로 게이지를 가로질러** 지나가 "웃돈이 피로에 작용한다"
       로 읽혔다(스틸 28.5s). 두 축 중 하나에만 걸리는 레버라 획이 다른 축을 건드리면 안 된다. */
    const centers = [156, 244];

    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
    ctx.textAlign = 'left';
    ctx.fillText(spec.ruleLabel || '규칙', 16, 34);
    ctx.textAlign = 'center';

    axes.forEach((ax, i) => {
      const cx = centers[i];
      const x = cx - gW / 2;

      // 트랙
      ctx.fillStyle = tone('track');
      roundRect(ctx, x, gTop, gW, gH, 5); ctx.fill();

      // 값 — 계속 미세하게 오르내린다 (축마다 위상을 달리해 동기화를 피한다)
      const wob = Math.sin((frac(t / BREATH) + i * 0.37) * Math.PI * 2) * 0.022;
      const fill = clamp((ax.fill ?? 0.5) + wob, 0.03, 0.97);
      const fh = gH * fill;
      ctx.fillStyle = tone('ink');
      roundRect(ctx, x, gBot - fh, gW, fh, 5); ctx.fill();

      // 문턱선 — 왼쪽 축만 leverCue 에서 내려간다
      const gateBase = ax.gate ?? 0.5;
      const gate = (i === 0) ? lerp(gateBase, gateBase - 0.19, lk) : gateBase;
      const gy = gBot - gH * gate;
      const on = ak > 0.02;
      ctx.setLineDash([6, 5]);
      ctx.lineWidth = 3;
      ctx.strokeStyle = on ? tone('accent') : tone('sub');
      if (on) setShadow(ctx, GLOW, 10, 0);
      ctx.beginPath();
      ctx.moveTo(x - 13, gy); ctx.lineTo(x + gW + 13, gy); ctx.stroke();
      clearShadow(ctx);
      ctx.setLineDash([]);

      // 라벨
      ctx.fillStyle = tone('ink'); ctx.font = disp(900, 16);
      ctx.fillText(ax.label || '', cx, gTop - 12);
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
      ctx.fillText(ax.rule || '', cx, gBot + 20);

      // 거부 표식 — 값이 문턱의 '거부 쪽'에 있으면 켠다
      const refusing = ax.dir === 'above' ? fill > gate : fill < gate;
      if (on && refusing) {
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
        setShadow(ctx, GLOW, 10, 0);
        const my = ax.dir === 'above' ? gTop - 32 : gBot + 34;
        ctx.beginPath();
        ctx.moveTo(cx - 7, my - 7); ctx.lineTo(cx + 7, my + 7);
        ctx.moveTo(cx + 7, my - 7); ctx.lineTo(cx - 7, my + 7);
        ctx.stroke();
        clearShadow(ctx);
      }
    });

    /* ── 레버 : 웃돈 ───────────────────────────── */
    if (lk > 0.02) {
      const bw = 104, bh = 52, bx = 16, by = 116;
      const s = spring(clamp(lk * 1.5));
      ctx.save(); ctx.translate(bx + bw / 2, by + bh / 2); ctx.scale(s, s);
      setShadow(ctx, GLOW, 14, 0);
      ctx.lineWidth = 4; ctx.strokeStyle = tone('accent'); ctx.fillStyle = '#000';
      roundRect(ctx, -bw / 2, -bh / 2, bw, bh, 9);
      ctx.fill(); ctx.stroke();
      clearShadow(ctx);
      ctx.restore();

      ctx.fillStyle = tone('accent'); ctx.font = disp(900, 19);
      ctx.fillText(spec.leverLabel || '', bx + bw / 2, by + 27);
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
      ctx.fillText(spec.leverNote || '', bx + bw / 2, by + 44);

      // 레버에서 포만 문턱선 왼쪽 끝으로 향하는 획 — 문턱을 끌어내린다.
      // 포만 게이지의 바깥쪽(왼쪽) 면만 스치므로 피로 축을 지나지 않는다.
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.globalAlpha = lk;
      const tx = centers[0] - gW / 2 - 13;
      const ty = gBot - gH * ((axes[0]?.gate ?? 0.5) - 0.19);
      ctx.beginPath();
      ctx.moveTo(bx + bw, by + bh / 2);
      ctx.quadraticCurveTo(lerp(bx + bw, tx, 0.6), lerp(by + bh / 2, ty, 0.85), tx, ty);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
