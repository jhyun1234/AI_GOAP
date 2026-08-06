import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* weightknob — 같은 신호인데, 가운데 손잡이가 어디 있느냐에 따라 결과가 문턱을 넘기도
   하고 못 넘기도 한다.

   원문: "그래서 주민마다 '겨울 대비를 얼마나 중요하게 여기는지'에 성격별 가중치를
   얹었습니다."

   🔴 이 샷이 그리는 것은 **가중치의 자리**다. 성격이 행동을 직접 고르는 것이 아니라
   신호와 결과 **사이**에 끼어 크기를 바꾼다는 것이 원문의 구조라, 왼쪽 신호(WINTER)는
   길이가 고정이고 오른쪽 결과(WINTER PREP)만 변한다. 행동을 고르는 스위치를 그리면
   원문과 다른 말이 된다.

   🔴 **손잡이는 t 로 계속 오르내린다.** 이건 장식이 아니라 뜻이다 — 가중치는 성격마다
   다르므로 한 값에 고정되면 안 된다. 눈금도 숫자도 안 붙였다: 원문에 값이 없고,
   눈금을 그리는 순간 지어낸 수치를 주장하게 된다.

   🔴 **주기(SWING)는 샷보다 짧아야 한다.** 1차본은 4.6초였는데 이 샷이 4.009초라,
   결과 막대가 등장하는 t≈1.70초가 하필 사인의 내리막과 겹쳐 **문턱을 한 프레임도 못 넘었다**
   (최대 높이 75.7 / 필요 104 / 초과 프레임 0 — 1차 검수 R2). reads 가 "넘기도 하고 못
   넘기도 한다"인데 화면은 "영영 못 닿는다"였다. 2.2초로 줄이면 게이트가 열린 뒤에
   마루(t≈2.85, 높이 163.2)와 골(t≈3.85, 높이 56.6)이 **둘 다** 샷 안에 들어온다.
   🔑 주기를 고를 때는 **게이트가 열리는 시각**(ok 가 0을 벗어나는 t)과 사인의 위상을 함께
   봐야 한다. 등장 게이트가 사인의 내리막에 걸리면 진폭이 아무리 커도 화면에는 안 나온다.

   🔴 문턱선은 앞뒤 샷(oneshove·apart)의 그 선이다. 여기서는 라벨을 안 붙였다 —
   결과 막대가 최대 163 까지 자라 라벨을 반드시 덮기 때문이고, 선의 뜻은 앞 샷이
   이미 가르쳤다. 대신 왼쪽 끝에 표식 하나를 둬 같은 선임을 보인다.

   🔴 등장은 전부 cue 에 물렸다(신호 → 손잡이 → 결과 순). */

const SWING = 2.2;   // 손잡이 한 바퀴(초). 샷(4.009초)보다 짧아야 마루와 골이 둘 다 보인다

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

    const BASE = 244, LINE = 140;
    const INX = 40, INW = 36, INH = 122;
    const TX = 176, TY0 = 104, TY1 = 248;
    const KW = 46, KH = 15;
    const OUTX = 276, OUTW = 36;

    const c0 = cue(spec.knobCue ?? 0, 0.15, 0.8);
    const ik = ease(clamp(c0 / 0.35));
    const kk = ease(clamp((c0 - 0.30) / 0.40));
    const ok = ease(clamp((c0 - 0.60) / 0.40));

    /* 손잡이 자리 — 성격마다 다르므로 한 값에 서지 않는다.
       🔴 SWING 은 샷 길이(4.009초)보다 짧아야 한다. R2 참조. */
    const kp = 0.5 + 0.42 * Math.sin(frac(t / SWING) * Math.PI * 2);
    const flow = -(t * 14) % 13;

    const hatch = (x, y, bwid, bh, color, alpha = 0.5) => {
      if (bh < 4) return;
      ctx.save();
      ctx.beginPath(); ctx.rect(x, y, bwid, bh); ctx.clip();
      ctx.strokeStyle = color; ctx.lineWidth = 3; ctx.globalAlpha = alpha;
      for (let k = -bh - 13; k < bwid + 13; k += 13) {
        const o = k + flow;
        ctx.beginPath();
        ctx.moveTo(x + o, y + bh); ctx.lineTo(x + o + bh, y);
        ctx.stroke();
      }
      ctx.restore();
    };

    /* ── 문턱선(먼저) ───────────────────────────────── */
    if (ok > 0.02) {
      ctx.globalAlpha = clamp(ok * 2) * 0.85;
      ctx.setLineDash([10, 8]);
      ctx.lineDashOffset = -(t * 12) % 18;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(250, LINE); ctx.lineTo(w - 16, LINE); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;

      ctx.fillStyle = tone('ink');
      ctx.beginPath();
      ctx.moveTo(250, LINE - 6); ctx.lineTo(259, LINE); ctx.lineTo(250, LINE + 6);
      ctx.closePath(); ctx.fill();
      ctx.globalAlpha = 1;
    }

    /* ── 왼쪽 — 겨울 신호. 크기가 고정이다 ──────────── */
    if (ik > 0.02) {
      const h = INH * ik, y = BASE - h;
      ctx.globalAlpha = clamp(ik * 2);
      ctx.fillStyle = tone('bg'); ctx.fillRect(INX, y, INW, h);
      hatch(INX, y, INW, h, tone('ink'), 0.42);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.rect(INX, y, INW, h); ctx.stroke();

      if (spec.inLabel) {
        ctx.textAlign = 'center';
        const fs = fit(spec.inLabel, 900, 12.5, 76, 9);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('ink');
        ctx.fillText(spec.inLabel, INX + INW / 2, BASE - INH - 14);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 가운데 — 성격이 끼어드는 자리 ──────────────── */
    if (kk > 0.02) {
      const a = clamp(kk * 2);
      ctx.globalAlpha = a;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(TX, TY0); ctx.lineTo(TX, TY1); ctx.stroke();

      ctx.textAlign = 'center';
      ctx.font = disp(800, 11); ctx.fillStyle = tone('sub');
      if (spec.highLabel) ctx.fillText(spec.highLabel, TX, TY0 - 8);
      if (spec.lowLabel) ctx.fillText(spec.lowLabel, TX, TY1 + 20);
      ctx.globalAlpha = 1;

      // 손잡이 — 계속 오르내린다
      const ky = lerp(TY1, TY0, kp) * kk + TY1 * (1 - kk);
      ctx.globalAlpha = a;
      ctx.fillStyle = tone('bg');
      roundRect(ctx, TX - KW / 2, ky - KH / 2, KW, KH, 5); ctx.fill();
      setShadow(ctx, GLOW, 9, 0);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, TX - KW / 2, ky - KH / 2, KW, KH, 5); ctx.stroke();
      clearShadow(ctx);

      if (spec.knobLabel) {
        ctx.font = disp(900, 15); ctx.fillStyle = tone('accent');
        ctx.fillText(spec.knobLabel, TX, ky + 5.5);
      }
      ctx.globalAlpha = 1;

      // 화살 둘 — 신호가 손잡이를 지나 결과로 간다
      ctx.globalAlpha = a * 0.9;
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(INX + INW + 6, 196); ctx.lineTo(147, 196); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(208, 196); ctx.lineTo(OUTX - 6, 196); ctx.stroke();
      ctx.fillStyle = tone('sub');
      ctx.beginPath();
      ctx.moveTo(147, 196); ctx.lineTo(139, 191); ctx.lineTo(139, 201); ctx.closePath(); ctx.fill();
      ctx.beginPath();
      ctx.moveTo(OUTX - 6, 196); ctx.lineTo(OUTX - 14, 191); ctx.lineTo(OUTX - 14, 201); ctx.closePath(); ctx.fill();
      ctx.globalAlpha = 1;
    }

    /* ── 오른쪽 — 결과. 손잡이를 따라 늘었다 줄었다 한다 ── */
    if (ok > 0.02) {
      const h = (46 + 132 * kp) * ok;
      const y = BASE - h;
      const over = y < LINE;
      const col = over ? tone('accent') : tone('ink');

      ctx.globalAlpha = clamp(ok * 2);
      ctx.fillStyle = tone('bg'); ctx.fillRect(OUTX, y, OUTW, h);
      hatch(OUTX, y, OUTW, h, col);
      ctx.strokeStyle = col; ctx.lineWidth = 3;
      if (over) setShadow(ctx, GLOW, 8, 0);
      ctx.beginPath(); ctx.rect(OUTX, y, OUTW, h); ctx.stroke();
      clearShadow(ctx);

      if (spec.outLabel) {
        ctx.textAlign = 'center';
        const fs = fit(spec.outLabel, 800, 10.5, 76, 8);
        ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.outLabel, OUTX + OUTW / 2, BASE + 20);
      }
      ctx.globalAlpha = 1;
    }

    /* 바닥선 */
    if (ik > 0.02) {
      ctx.globalAlpha = clamp(ik * 2) * 0.8;
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(INX - 8, BASE); ctx.lineTo(INX + INW + 8, BASE); ctx.stroke();
      if (ok > 0.02) {
        ctx.beginPath(); ctx.moveTo(OUTX - 8, BASE); ctx.lineTo(OUTX + OUTW + 8, BASE); ctx.stroke();
      }
      ctx.globalAlpha = 1;
    }

    /* ── 이 손잡이가 무엇 때문에 움직이는가 ─────────── */
    if (spec.byLabel) {
      const k = ease(clamp((c0 - 0.45) / 0.4));
      if (k > 0.02) {
        ctx.globalAlpha = k;
        ctx.textAlign = 'center';
        const fs = fit(spec.byLabel, 900, 13, 120, 9);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
        ctx.fillText(spec.byLabel, TX, 288);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
