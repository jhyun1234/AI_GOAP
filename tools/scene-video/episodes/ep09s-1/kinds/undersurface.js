import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* undersurface — 표면 아래에는 다 갈려 있는데, 그게 위로 못 올라온다.

   원문: "그동안 저는 주민마다 성격(M4)과 직업(M5)을 나눠 넣는 작업을 해왔습니다. 농사꾼은
   밭을 붙들고, 떠돌이는 잘 돌아다니고, 고집쟁이는 웬만하면 시키는 걸 미룹니다. 코드 속에서는
   분명히 다른 개체들이었습니다." / "속은 다들 다르게 생각하고 있는데, 그 다름이 화면에는
   하나도 드러나지 않는 상황이었습니다."

   🔴 같은 표면선을 이번엔 **아래에서** 본다. S1 에서 겉면뿐이던 그 선 밑에 코드 칸이 있고,
   칸마다 값이 서로 다르다. 값들이 위로 밀어 올라가려다 선에 부딪혀 되돌아온다 —
   이것이 이 편의 어긋난 상태 그 자체다.

   🔴 **화면에 적은 값은 원문 문장에 실재하는 낱말 셋뿐이고, 셋 다 `M4 TRAIT` 행에 있다.**
   「고집쟁이」·「떠돌이」·「농사꾼」은 **전부 성격 이름**이다
   (`Assets/M0Config/Personalities/` 의 `Personality_Stubborn`·`Wanderer`·`Farmer`).
   원문이 "농사꾼은 밭을 붙들고, 떠돌이는 잘 돌아다니고, 고집쟁이는 …"으로 셋을 나란히 쓴 것은
   **서로 다른 주민 셋의 성격**을 든 것이라, 한 행에 값 셋이 서로 다르게 앉는 것이 맞다.

   🔴 **`M5 JOB` 행은 네 칸이 전부 가려져 있다.** 직업 축이 있다는 것은 원문이 말하지만
   ("성격(M4)과 직업(M5)을 나눠 넣는 작업") **직업 이름은 한 번도 안 부른다** —
   실재하는 직업은 목수·요리사·탐험가·농부·나무꾼·치료사·광부인데 그중 어느 것도 원문에 없다.
   🔴 1차본은 이 행에 「농사꾼」·「떠돌이」를 넣었다가 반려됐다. **낱말은 원문에 있어도
   분류는 원문에 없었다** — 브리프에 없는 것을 채운 것이고, 이 회차가 다루는 밀스톤의
   ADR-M7-5 가 정확히 그 혼동을 막으려고 세워진 조항이다.
   🔑 그래서 여덟 칸 중 값이 든 것은 셋, 가린 것이 다섯이다. 가려 두는 쪽이 뜻에도 맞다 —
   보는 사람 입장에서는 애초에 안 보이는 값들이다.

   🎨 여기에는 강조색이 한 번도 안 나온다. 이 회차에서 강조색은 '화면에서 읽히는 것'이고,
   이 샷에서는 아무것도 화면에 못 올라왔기 때문이다. */

const FY0 = 22, FY1 = 190;
const DPAD = 22;
const R = 9;

const CODE_LB = 214;                   // CODE 라벨 baseline
const ROW_Y = [220, 252];              // 행 상자 top
const ROW_H = 24;
const CELL_X0 = 84;

/* 궤도 상수 — S1·S3 와 일부러 같다(같은 마을이 이어진다).
   넷을 2×2 칸에 가둬 어느 프레임에서도 안 겹치게 했다 — 근거와 검산은 samedots.js 에. */
const PX = [7.3, 9.1, 6.2, 10.4];
const PY = [5.1, 4.3, 6.7, 3.9];
const PH = [0.00, 0.37, 0.68, 0.15];
const CX = [0.20, 0.78, 0.20, 0.78];
const CY = [0.20, 0.20, 0.82, 0.82];
const AX = [0.13, 0.12, 0.12, 0.13];
const AY = [0.10, 0.09, 0.09, 0.10];

function dotAt(i, t, x0, x1, y0, y1) {
  const bw = x1 - x0, bh = y1 - y0;
  return {
    x: lerp(x0, x1, CX[i]) + bw * AX[i] * Math.sin(2 * Math.PI * (t / PX[i] + PH[i])),
    y: lerp(y0, y1, CY[i]) + bh * AY[i] * Math.cos(2 * Math.PI * (t / PY[i] + PH[i] * 1.7))
  };
}

/* 밀어 올리기 — 0 → 1 → 0 을 반복한다. t 의 함수라 문턱이 없다. */
function press(i, t) {
  const u = frac((t + i * 0.34) / 2.4);
  return u < 0.5 ? ease(u / 0.5) : ease((1 - u) / 0.5);
}

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

    const X0 = 14, X1 = w - 14;
    const bx0 = X0 + DPAD, bx1 = X1 - DPAD, by0 = FY0 + DPAD, by1 = 128;

    const rows = spec.rows ?? [];
    const NC = spec.cols ?? 4;
    const CW = (X1 - CELL_X0 - 6 * (NC - 1)) / NC;
    const colCx = i => CELL_X0 + i * (CW + 6) + CW / 2;

    const ck = ease(cue(spec.codeCue ?? 0, 0.12, 0.62));    // 코드 칸이 드러난다
    const bk = ease(cue(spec.blockCue ?? 1, 0.12, 0.55));   // 선이 벽이 된다

    /* ── 화면 프레임(위 세 변) + 그 안의 동그라미들 ── */
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(X0, FY1); ctx.lineTo(X0, FY0 + 8);
    ctx.quadraticCurveTo(X0, FY0, X0 + 8, FY0);
    ctx.lineTo(X1 - 8, FY0);
    ctx.quadraticCurveTo(X1, FY0, X1, FY0 + 8);
    ctx.lineTo(X1, FY1);
    ctx.stroke();

    ctx.textAlign = 'left';
    ctx.font = disp(800, 10.5); ctx.fillStyle = tone('sub');
    ctx.fillText(spec.screenLabel ?? '', X0 + 2, FY0 - 6);

    for (let i = 0; i < 4; i++) {
      const p = dotAt(i, t, bx0, bx1, by0, by1);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(p.x, p.y, R, 0, Math.PI * 2); ctx.stroke();
    }

    /* ── 코드 칸 — 아래에는 다 갈려 있다 ─────────────── */
    if (ck > 0.02) {
      ctx.globalAlpha = ck;
      ctx.textAlign = 'left';
      ctx.font = disp(800, 10.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.codeLabel ?? '', X0 + 2, CODE_LB);
      ctx.globalAlpha = 1;

      for (let r = 0; r < rows.length; r++) {
        const y = ROW_Y[r];
        const ra = clamp(ck * 2.6 - r * 0.55);
        if (ra <= 0.02) continue;

        ctx.globalAlpha = ra;
        ctx.textAlign = 'left';
        const lfs = fit(rows[r].label ?? '', 800, 10.5, CELL_X0 - X0 - 10, 7.5);
        ctx.font = disp(800, lfs); ctx.fillStyle = tone('sub');
        ctx.fillText(rows[r].label ?? '', X0 + 2, y + ROW_H - 7);
        ctx.globalAlpha = 1;

        for (let c = 0; c < NC; c++) {
          const a = clamp(ra * 2.2 - c * 0.26);
          if (a <= 0.02) continue;
          const cx = CELL_X0 + c * (CW + 6);
          const v = (rows[r].cells ?? [])[c];

          if (v) {
            ctx.globalAlpha = a;
            ctx.textAlign = 'center';
            const fs = fit(v, 900, 13.5, CW - 8, 8.5);
            ctx.font = disp(900, fs); ctx.fillStyle = tone('ink');
            ctx.fillText(v, cx + CW / 2, y + ROW_H - 7);
            ctx.globalAlpha = 1;
          } else {
            /* 가린 값 — 원문에 없는 이름을 지어내지 않는다. 빗금이 계속 흐른다(t). */
            ctx.save();
            ctx.beginPath(); ctx.rect(cx, y + 3, CW, ROW_H - 6); ctx.clip();
            const shift = (t * 13) % 14;
            ctx.globalAlpha = a * 0.38;
            ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
            const nS = Math.ceil(CW / 14) + 3;
            for (let k = -2; k < nS; k++) {
              const px = cx + k * 14 - shift;
              ctx.beginPath();
              ctx.moveTo(px + 11, y + 3); ctx.lineTo(px, y + ROW_H - 3);
              ctx.stroke();
            }
            ctx.restore();
            ctx.globalAlpha = 1;
          }

          ctx.globalAlpha = a;
          ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
          roundRect(ctx, cx, y, CW, ROW_H, 5); ctx.stroke();
          ctx.globalAlpha = 1;
        }
      }
    }

    /* ── 밀어 올리는 획 + 그것을 막는 선 ────────────────
       획은 t 로 계속 오르내리고, '언제 처음 뜨는가'만 blockCue 가 정한다. */
    /* 0.18 은 S1 의 프레임 아랫변(track 0.12)에서 이어지는 밝기다 — 같은 선이라는 것이
       샷이 바뀌어도 읽혀야 한다. 끝값 1.0 / 5px 은 S3 가 그대로 받아 점선으로 푼다. */
    const barA = lerp(0.18, 1, bk);
    const barW = lerp(3, 5, bk);

    if (bk > 0.02) {
      for (let c = 0; c < NC; c++) {
        const k = press(c, t) * bk;
        const cx = colCx(c);
        const tipY = lerp(ROW_Y[0] - 2, FY1 + 5, k);

        ctx.globalAlpha = clamp(bk * 2) * (0.45 + 0.55 * k);
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 5;
        ctx.beginPath(); ctx.moveTo(cx, ROW_Y[0] - 2); ctx.lineTo(cx, tipY); ctx.stroke();

        /* 닿는 순간 획 끝이 납작하게 눌린다 — 뚫지 못하고 퍼진다 */
        const hit = clamp((k - 0.72) / 0.28);
        if (hit > 0.02) {
          ctx.globalAlpha = clamp(bk * 2) * hit;
          ctx.lineWidth = 5;
          ctx.beginPath();
          ctx.moveTo(cx - 11 * hit, tipY + 2); ctx.lineTo(cx + 11 * hit, tipY + 2);
          ctx.stroke();
        }
        ctx.globalAlpha = 1;
      }
    }

    /* 선 — 눌린 자리마다 아래로 살짝 처지지만 끝내 안 뚫린다 */
    ctx.globalAlpha = barA;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = barW;
    ctx.beginPath();
    ctx.moveTo(X0, FY1);
    for (let c = 0; c < NC; c++) {
      const cx = colCx(c);
      const sag = press(c, t) * bk * 4;
      ctx.lineTo(cx - 22, FY1);
      ctx.quadraticCurveTo(cx, FY1 + sag * 2, cx + 22, FY1);
    }
    ctx.lineTo(X1, FY1);
    ctx.stroke();
    ctx.globalAlpha = 1;

    ctx.textAlign = 'left';
  }
};
