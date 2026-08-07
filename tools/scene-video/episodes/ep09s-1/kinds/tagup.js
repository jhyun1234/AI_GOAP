import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* tagup — 선이 열리고, 아래 있던 것이 머리 위로 올라온다.

   원문: "먼저 주민 머리 위에 이름표를 달고, 클릭하면 이름·성격·직업이 뜨는 정보줄을
   만들었습니다. … 이 한 줄 덕분에 그동안 코드 속에만 있던 성격(M4)·직업(M5) 분화가
   처음으로 화면에서 실제로 보이게 됐습니다."

   🔴 같은 표면선을 세 번째로 쓴다. S2 에서 획을 튕겨내던 그 선이 여기서 **뚫린다** —
   실선이 흐르는 점선으로 바뀌고, 코드 칸에서 이름표가 올라와 각 동그라미 머리에 앉는다.
   앉은 뒤에도 동그라미가 계속 돌아다니므로 이름표가 그것을 따라다니고, 코드 칸까지
   이어진 줄이 선을 가로지른 채로 함께 흔들린다. 이 편에서 면적이 가장 큰 움직임이다.

   🔴 **이름표 안에 글자를 안 썼다.** 원문에 주민 개인의 이름이 한 번도 안 나온다.
   대신 이름이 적히는 자리를 **길이가 서로 다른 막대**로 뒀다 — 넷이 다른 이름을 가졌다는
   뜻은 지되, 없는 이름을 지어내지는 않는다. (정보줄에 실제로 뜨는 글자는 S4 가 진다.)

   🎨 이 회차에서 강조색은 '화면에서 읽히는 것'이다. 그래서 여기서 처음으로 강조색이
   위쪽 화면 안에 들어온다 — 뚫린 선, 이름표, 그것을 잇는 줄. */

const FY0 = 22, FY1 = 190;
const DPAD = 22;
const R = 9;

const ROW_Y = [220, 252];
const ROW_H = 24;
const CELL_X0 = 84;

const TAG_W = 46, TAG_H = 18;
const BAR_W = [26, 18, 30, 22];   // 이름 길이가 서로 다르다는 뜻 — 글자는 안 적는다

/* 코드 칸 i 가 어느 동그라미로 올라가는가.
   2×2 배치에서 왼쪽 칸 둘은 왼쪽 동그라미로, 오른쪽 칸 둘은 오른쪽으로 보낸다 —
   연결선이 서로 길게 엇갈리면 뚫렸다는 것보다 엉켰다는 것이 먼저 읽힌다. */
const TAG_TO_DOT = [0, 2, 1, 3];

/* 궤도 상수 — S1·S2 와 같은 값이다. 넷을 2×2 칸에 가둬 어느 프레임에서도 안 겹친다
   (근거와 검산은 samedots.js 에). 🔴 **이 샷이 그 상수가 필요한 이유 그 자체다** —
   여기서만 동그라미 위에 46×18 짜리 이름표가 얹히므로, 동그라미가 안 닿아도 이름표가 닿는다.
   도는 칸(by0/by1)은 이름표가 프레임 위로 안 삐져나갈 만큼만 아래로 내렸다. */
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
    /* 62~150 — 이름표가 동그라미 위 29px 에 앉으므로 위로 40px 을 비워 두고,
       아래로는 코드 칸으로 내려가는 연결선 자리를 남긴다. */
    const bx0 = X0 + DPAD, bx1 = X1 - DPAD, by0 = 62, by1 = 150;

    const rows = spec.rows ?? [];
    const NC = spec.cols ?? 4;
    const CW = (X1 - CELL_X0 - 6 * (NC - 1)) / NC;
    const colCx = i => CELL_X0 + i * (CW + 6) + CW / 2;

    const ok = ease(cue(spec.openCue ?? 0, 0.10, 0.72));
    const rise = i => ease(clamp((ok - i * 0.10) / 0.60));   // 마지막 하나가 ok = 0.90 에 앉는다

    /* ── 화면 프레임(위 세 변) ─────────────────────── */
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
    ctx.fillText(spec.codeLabel ?? '', X0 + 2, 214);

    /* ── 코드 칸 — S2 에서 세운 그대로 ─────────────── */
    for (let r = 0; r < rows.length; r++) {
      const y = ROW_Y[r];
      ctx.globalAlpha = 0.75;
      ctx.textAlign = 'left';
      const lfs = fit(rows[r].label ?? '', 800, 10.5, CELL_X0 - X0 - 10, 7.5);
      ctx.font = disp(800, lfs); ctx.fillStyle = tone('sub');
      ctx.fillText(rows[r].label ?? '', X0 + 2, y + ROW_H - 7);

      for (let c = 0; c < NC; c++) {
        const cx = CELL_X0 + c * (CW + 6);
        const v = (rows[r].cells ?? [])[c];
        if (v) {
          ctx.textAlign = 'center';
          const fs = fit(v, 900, 13.5, CW - 8, 8.5);
          ctx.font = disp(900, fs); ctx.fillStyle = tone('ink');
          ctx.fillText(v, cx + CW / 2, y + ROW_H - 7);
        } else {
          ctx.save();
          ctx.beginPath(); ctx.rect(cx, y + 3, CW, ROW_H - 6); ctx.clip();
          const shift = (t * 13) % 14;
          ctx.globalAlpha = 0.30;
          ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
          const nS = Math.ceil(CW / 14) + 3;
          for (let k = -2; k < nS; k++) {
            const px = cx + k * 14 - shift;
            ctx.beginPath(); ctx.moveTo(px + 11, y + 3); ctx.lineTo(px, y + ROW_H - 3); ctx.stroke();
          }
          ctx.restore();
          ctx.globalAlpha = 0.75;
        }
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        roundRect(ctx, cx, y, CW, ROW_H, 5); ctx.stroke();
      }
      ctx.globalAlpha = 1;
    }

    /* ── 선 — 실선에서 흐르는 점선으로 바뀐다 ─────────
       뚫렸다는 것을 색이 아니라 **끊긴 선**으로 말한다. 점선은 t 로 계속 흐른다. */
    ctx.globalAlpha = lerp(1, 0.55, ok);
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = lerp(5, 3, ok);
    if (ok > 0.05) { ctx.setLineDash([lerp(300, 9, ok), lerp(0.01, 11, ok)]); ctx.lineDashOffset = -(t * 16) % 20; }
    ctx.beginPath(); ctx.moveTo(X0, FY1); ctx.lineTo(X1, FY1); ctx.stroke();
    ctx.setLineDash([]); ctx.lineDashOffset = 0;
    ctx.globalAlpha = 1;

    /* ── 동그라미들 — 계속 돌아다닌다 ───────────────── */
    const pos = [];
    for (let i = 0; i < 4; i++) pos.push(dotAt(i, t, bx0, bx1, by0, by1));

    /* ── 코드 칸에서 머리 위로 — 이름표가 올라온다 ──── */
    for (let i = 0; i < 4; i++) {
      const g = rise(i);
      if (g <= 0.02) continue;
      const p = pos[TAG_TO_DOT[i]];
      const ax = p.x, ay = p.y - R - 20;
      const tx = lerp(colCx(i), ax, g);
      const ty = lerp(ROW_Y[0] - 4, ay, g);
      const a = clamp(g * 3.2);

      /* 코드 칸까지 이어진 줄 — 선을 가로지른 채 함께 흔들린다 */
      ctx.globalAlpha = a * 0.55;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(colCx(i), ROW_Y[0] - 2);
      ctx.lineTo(tx, ty + TAG_H / 2);
      ctx.stroke();

      /* 이름표 — 강조색 테두리 안에 이름이 적히는 자리(길이가 저마다 다르다) */
      ctx.globalAlpha = a;
      setShadow(ctx, GLOW, 9, 0);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, tx - TAG_W / 2, ty - TAG_H / 2, TAG_W, TAG_H, 5); ctx.stroke();
      clearShadow(ctx);

      ctx.fillStyle = tone('ink');
      roundRect(ctx, tx - BAR_W[i] / 2, ty - 2.5, BAR_W[i] * g, 5, 2.5); ctx.fill();
      ctx.globalAlpha = 1;
    }

    for (let i = 0; i < 4; i++) {
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(pos[i].x, pos[i].y, R, 0, Math.PI * 2); ctx.stroke();
    }

    ctx.textAlign = 'left';
  }
};
