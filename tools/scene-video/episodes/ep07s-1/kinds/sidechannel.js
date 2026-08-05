import {
  disp, ease, clamp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* sidechannel — 갈래는 둘인데 하나는 안 갔다.

   원문: "이 일과를 마을 전체가 공유하는 목표 목록에 넣으면, 농부가 아닌 주민까지 전부
   밭을 서성이게 됩니다. 그래서 일과는 공용 목록에 넣지 않고, 각 주민이 자기 판단에만
   몰래 끼워 넣는 방식으로 처리했습니다. 촌장이 특정 주민에게만 명령을 꽂아 넣는 것과
   똑같은 통로를 재사용했습니다."

   🔴 **이 문장은 가정법이다.** 전부 밭을 서성인 일은 실제로 일어나지 않았다. 화면이
   그것을 사실처럼 그리면 대본이 원문을 어긴다 — 그래서 갈래 둘을 **다른 물질로** 그린다.

     공용 목록 갈래  : 처음부터 끝까지 track 색 점선. 끝에 앉는 밭 표식도 점선 테두리에
                       채움이 없다. 위에 「안 한 쪽」이 붙는다. 흐르는 표식이 하나도 없다.
     촌장 통로 갈래  : 실선 · 강조색 · 표식이 흐른다. 도착한 한 사람만 채워진다.

   한 화면에서 **실선과 점선이 곧 했다와 안 했다**를 뜻하고, 그 규칙이 이 그림의 문법이다.

   🔴 두 갈래의 흐름 방향도 반대다(실선은 아래로, 점선은 위로). 안 간 쪽이 되감기는 것처럼
   보여야 '가지 않았다'가 정지가 아니라 성질로 읽힌다 — 정적 구간도 그 김에 막는다.

   🔴 사람 셋에는 이름을 안 붙였다. 원문이 준 수가 아니라서(여덟 중 셋이 아니다) 세는
   순간 새 주장이 된다. '전부'라는 뜻만 지면 되는 자리다.

   🔴 등장은 전부 cue 에 물렸다. t 로 도는 것은 두 갈래의 dash 와 통로 안 표식 흐름뿐이다. */

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

    const N = spec.people ?? 3;
    const target = spec.target ?? 1;
    const sk = ease(cue(spec.sharedCue ?? 0, 0.15, 0.7));
    const pk = ease(cue(spec.pipeCue ?? 1, 0.15, 0.8));

    /* ── 일과 카드 ─────────────────────────────────── */
    const RW = 108, RH = 36, RX = w / 2 - RW / 2, RY = 34;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, RX, RY, RW, RH, 7); ctx.stroke();
    ctx.textAlign = 'center';
    let fs = fit(spec.routineLabel ?? 'ROUTINE', 900, 15, RW - 14, 10);
    ctx.font = disp(900, fs); ctx.fillStyle = tone('ink');
    ctx.fillText(spec.routineLabel ?? 'ROUTINE', w / 2, RY + RH / 2 + fs * 0.36);

    /* ── 사람 셋 ───────────────────────────────────── */
    const PY = 234;
    const px = i => w / 2 + (i - (N - 1) / 2) * 74;

    /* ── 왼쪽 갈래 — 안 한 쪽(가정) ────────────────── */
    const BW = 112, BH = 34, BX = 14, BY = 122;
    if (sk > 0.02) {
      ctx.globalAlpha = sk;
      ctx.setLineDash([7, 7]);
      ctx.lineDashOffset = (t * 9) % 14;                 // 위로 되감긴다
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;

      ctx.beginPath();
      ctx.moveTo(RX + 8, RY + RH);
      ctx.quadraticCurveTo(BX + BW / 2, RY + RH + 20, BX + BW / 2, BY);
      ctx.stroke();

      roundRect(ctx, BX, BY, BW, BH, 6); ctx.stroke();

      // 셋 모두에게 번진다 — 점선이라 '그랬을 것'이다
      for (let i = 0; i < N; i++) {
        const a = clamp(sk * 2.4 - i * 0.5);
        if (a <= 0.02) continue;
        ctx.globalAlpha = sk * a;
        ctx.beginPath();
        ctx.moveTo(BX + BW / 2, BY + BH);
        ctx.quadraticCurveTo(BX + BW / 2, PY - 40, px(i), PY - 14);
        ctx.stroke();
        // 채워지지 않은 밭 표식
        ctx.beginPath(); ctx.arc(px(i), PY + 24, 8, 0, Math.PI * 2); ctx.stroke();
      }
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;

      ctx.textAlign = 'center';
      ctx.globalAlpha = sk;
      fs = fit(spec.sharedLabel ?? 'SHARED GOALS', 800, 13, BW - 10, 8);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.sharedLabel ?? 'SHARED GOALS', BX + BW / 2, BY + BH / 2 + fs * 0.36);

      if (spec.whatIf) {
        fs = fit(spec.whatIf, 900, 13, BW + 12, 9);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('ink');
        ctx.fillText(spec.whatIf, BX + BW / 2, BY - 12);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 오른쪽 갈래 — 실제로 쓴 통로 ──────────────── */
    const PW = 116, PH = 34, PX = w - PW - 14, PBY = 122;
    if (pk > 0.02) {
      ctx.globalAlpha = pk;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;

      ctx.beginPath();
      ctx.moveTo(RX + RW - 8, RY + RH);
      ctx.quadraticCurveTo(PX + PW / 2, RY + RH + 20, PX + PW / 2, PBY);
      ctx.stroke();

      roundRect(ctx, PX, PBY, PW, PH, 6); ctx.stroke();

      // 한 사람에게만 내려간다
      const dk = ease(clamp((pk - 0.28) / 0.6));
      if (dk > 0.02) {
        ctx.globalAlpha = pk;
        ctx.beginPath();
        ctx.moveTo(PX + PW / 2, PBY + PH);
        ctx.quadraticCurveTo(PX + PW / 2, PY - 44, px(target), PY - 46 + (PY - 14 - (PY - 46)) * dk);
        ctx.stroke();

        // 통로 안을 흐르는 표식 셋
        for (let s = 0; s < 3; s++) {
          const u = frac(t / 1.6 + s / 3);
          const bx = PX + PW / 2, by = PBY + PH;
          const ex = px(target), ey = PY - 14;
          const cx0 = PX + PW / 2, cy0 = PY - 44;
          const mx = (1 - u) * (1 - u) * bx + 2 * (1 - u) * u * cx0 + u * u * ex;
          const my = (1 - u) * (1 - u) * by + 2 * (1 - u) * u * cy0 + u * u * ey;
          ctx.globalAlpha = pk * dk * (1 - Math.abs(u - 0.5) * 0.6);
          ctx.fillStyle = tone('accent');
          ctx.beginPath(); ctx.arc(mx, my, 4.5, 0, Math.PI * 2); ctx.fill();
        }
      }
      ctx.globalAlpha = 1;

      ctx.textAlign = 'center';
      ctx.globalAlpha = pk;
      fs = fit(spec.chiefLabel ?? 'CHIEF ORDER', 800, 13, PW - 10, 8);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.chiefLabel ?? 'CHIEF ORDER', PX + PW / 2, PBY + PH / 2 + fs * 0.36);
      ctx.globalAlpha = 1;
    }

    /* ── 사람 표식 — 통로가 닿은 하나만 채워진다 ────── */
    for (let i = 0; i < N; i++) {
      const hit = i === target && pk > 0.75;
      if (hit) {
        setShadow(ctx, GLOW, 8, 0);
        ctx.fillStyle = tone('accent');
        ctx.beginPath(); ctx.arc(px(i), PY, 7.5, 0, Math.PI * 2); ctx.fill();
        clearShadow(ctx);
      } else {
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.arc(px(i), PY, 7, 0, Math.PI * 2); ctx.stroke();
      }
    }

    /* ── 아래 한 줄 ────────────────────────────────── */
    if (spec.reuse) {
      const k = clamp((pk - 0.65) / 0.35);
      if (k > 0.02) {
        ctx.globalAlpha = k;
        ctx.textAlign = 'center';
        fs = fit(spec.reuse, 900, 13.5, w - 24, 9);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('ink');
        ctx.fillText(spec.reuse, w / 2, 286);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
