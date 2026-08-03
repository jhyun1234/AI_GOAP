import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* wicket — 합친 것 자체가 아니라, 합친 뒤에 왼쪽이 안 변한다는 것이 이 그림의 사건이다.

   왼쪽 Runner 상자 셋에 똑같은 '앵커 찾기' 조각이 하나씩 붙어 제각기 떨고 있다(중복).
   mergeCue 에서 조각 셋이 상자를 떠나 오른쪽 창구 하나로 빨려 들어가고, 창구가 굵어지며
   Runner 마다 창구로 이어지는 선이 생긴다. addCue 에서 위에서 **새 앵커(집)** 칩이 내려와
   창구에만 붙는데, Runner 상자 셋은 눈금 하나도 움직이지 않는다.

   🔴 앞선 회차에 '여럿이 하나로'가 두 번 있었지만 둘 다 **모여서 나빠진** 그림이었다
   (한 파일에 다 몰림 / 한 줄기가 관 넷으로). 여기는 방향이 반대라, 합쳐진 쪽을 강조색으로
   두껍게 그리고 '나빠짐'을 뜻하는 ✕·금·기울기를 하나도 쓰지 않았다. 그리고 결말을
   합침이 아니라 **새 것이 들어와도 안 흔들림**에 뒀다 — 저 둘에는 그 마지막 장면이 없다.

   계속 도는 것 = 중복 조각의 떨림, 합쳐진 뒤에는 연결선 위를 흐르는 표식. */

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));

    const dk = ease(cue(spec.dupCue ?? 0, 0.15, 0.6));
    const mk = ease(cue(spec.mergeCue ?? 1, 0.15, 0.62));
    const ak = ease(cue(spec.addCue ?? 2, 0.15, 0.6));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const RX = 12, RW = 130, RH = 50, RY = [60, 124, 188];
    const WX = 206, WW = w - WX - 12, WY = 104, WH = 72;
    const wcx = WX + WW / 2, wcy = WY + WH / 2;

    /* ── 중복 안내 ────────────────────────────────── */
    if (dk > 0.02 && spec.dupNote) {
      ctx.globalAlpha = clamp(dk * 2);
      ctx.textAlign = 'left';
      const fs = fit(spec.dupNote, 800, 10.5, 176, 8);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.dupNote, RX + 2, 48);
      ctx.globalAlpha = 1;
    }

    /* ── 연결선 — 합친 뒤에 생긴다 ────────────────── */
    if (mk > 0.55) {
      const k = clamp((mk - 0.55) / 0.45);
      RY.forEach((ry, i) => {
        const y0 = ry + RH / 2;
        const x0 = RX + RW, x1 = WX;
        ctx.globalAlpha = k * 0.75;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(x0, y0);
        ctx.lineTo(lerp(x0, x1, 0.55), y0);
        ctx.lineTo(x1, wcy);
        ctx.stroke();

        const u = frac(t / 2.9 + i / 3);
        const mxp = u < 0.55 ? lerp(x0, lerp(x0, x1, 0.55), u / 0.55) : lerp(lerp(x0, x1, 0.55), x1, (u - 0.55) / 0.45);
        const myp = u < 0.55 ? y0 : lerp(y0, wcy, (u - 0.55) / 0.45);
        ctx.globalAlpha = k * 0.9;
        ctx.fillStyle = tone('accent');
        ctx.beginPath(); ctx.arc(mxp, myp, 3.5, 0, Math.PI * 2); ctx.fill();
        ctx.globalAlpha = 1;
      });
    }

    /* ── Runner 상자 셋 ───────────────────────────── */
    RY.forEach((ry, i) => {
      const born = clamp(dk * 3.4 - i * 0.5);
      if (born < 0.02) return;
      const s = Math.min(1, spring(clamp(born)));
      ctx.globalAlpha = clamp(born * 1.8);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      roundRect(ctx, RX + (RW - RW * s) / 2, ry + (RH - RH * s) / 2, RW * s, RH * s, 3);
      ctx.stroke();
      ctx.textAlign = 'left';
      ctx.font = mono(700, 10.5); ctx.fillStyle = tone('ink');
      ctx.fillText(spec.runner || 'Runner', RX + 10, ry + 17);
      ctx.globalAlpha = 1;

      /* 중복 조각 — 상자를 떠나 창구로 간다 */
      const gone = ease(clamp((mk - i * 0.08) / 0.62));
      const px = lerp(RX + 12, wcx - 30, gone);
      const py = lerp(ry + 24, wcy - 11, gone);
      const pw = lerp(96, 30, gone), ph = 22;
      const jit = gone < 0.02 ? 1.6 * Math.sin(frac(t / 0.9) * Math.PI * 2 + i * 2.1) : 0;
      const pa = clamp(born * 1.8) * (1 - clamp((gone - 0.7) / 0.3));
      if (pa > 0.02) {
        ctx.globalAlpha = pa;
        ctx.lineWidth = 3; ctx.strokeStyle = tone('sub');
        roundRect(ctx, px + jit, py, pw, ph, 3); ctx.stroke();
        if (gone < 0.5) {
          ctx.textAlign = 'left';
          const fs = fit(spec.piece || '앵커 찾기', 800, 10.5, pw - 10, 7.5);
          ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
          ctx.fillText(spec.piece || '앵커 찾기', px + jit + 6, py + 15);
        }
        ctx.globalAlpha = 1;
      }
    });

    /* ── 창구 하나 ────────────────────────────────── */
    if (mk > 0.05) {
      const k = clamp(mk * 1.6);
      const done = clamp((mk - 0.6) / 0.4);
      const s = Math.min(1, spring(clamp(mk * 1.3)));
      ctx.globalAlpha = k;
      ctx.lineWidth = 3 + 2 * done;
      ctx.strokeStyle = tone('accent');
      if (done > 0.4) setShadow(ctx, GLOW, 14, 0);
      roundRect(ctx, WX + (WW - WW * s) / 2, WY + (WH - WH * s) / 2, WW * s, WH * s, 4);
      ctx.stroke();
      clearShadow(ctx);

      ctx.textAlign = 'center';
      let fs = 11.5; ctx.font = mono(700, fs);
      const nm = spec.wicket || '';
      while (fs > 6.5 && ctx.measureText(nm).width > WW - 16) { fs -= 0.5; ctx.font = mono(700, fs); }
      ctx.fillStyle = tone('accent');
      ctx.fillText(nm, wcx, wcy + 2);

      if (spec.wicketNote) {
        const nfs = fit(spec.wicketNote, 800, 10.5, WW - 16, 8);
        ctx.font = disp(800, nfs); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.wicketNote, wcx, wcy + 22);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 새 앵커가 들어와도 Runner 는 그대로 ──────── */
    if (ak > 0.02 && spec.newAnchor) {
      const k = clamp(ak * 1.8);
      const bw = WW - 6, bx = WX + 3;
      const by = lerp(38, 62, ease(clamp(ak / 0.7)));
      const s = Math.min(1, spring(clamp(ak * 1.2)));
      ctx.globalAlpha = k;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
      roundRect(ctx, bx + (bw - bw * s) / 2, by + (26 - 26 * s) / 2, bw * s, 26 * s, 3);
      ctx.stroke();
      ctx.textAlign = 'center';
      const fs = fit(spec.newAnchor, 800, 12, bw - 14, 8.5);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.newAnchor, bx + bw / 2, by + 18);

      if (ak > 0.5) {
        const kk = clamp((ak - 0.5) / 0.4);
        ctx.globalAlpha = kk;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        const ay0 = by + 27, ay1 = ay0 + 10 * kk;
        ctx.beginPath();
        ctx.moveTo(wcx, ay0); ctx.lineTo(wcx, ay1);
        ctx.moveTo(wcx, ay1); ctx.lineTo(wcx - 5, ay1 - 5);
        ctx.moveTo(wcx, ay1); ctx.lineTo(wcx + 5, ay1 - 5);
        ctx.stroke();
      }
      ctx.globalAlpha = 1;
    }

    if (ak > 0.45 && spec.stillLabel) {
      const k = clamp((ak - 0.45) / 0.45);
      const s = Math.min(1, spring(k));
      ctx.globalAlpha = k;
      ctx.textAlign = 'left';
      const fs = fit(spec.stillLabel, 900, 15, RW + 46, 10);
      ctx.font = disp(900, fs * s); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.stillLabel, RX + 2, 268);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
