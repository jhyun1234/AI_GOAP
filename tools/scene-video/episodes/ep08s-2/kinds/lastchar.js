import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* lastchar — 앞은 다 같고 끝 한 칸만 다른 이름 다섯이, 거의 같은 높이로 떨어진다.

   원문: "원인은 이름에 있었습니다. 편차를 만드는 데 쓰던 계산이 이름 문자열을 숫자로 바꾸는
   방식이었는데, 주민 이름이 끝 글자 하나만 다르다 보니(A, B, C, D, E) 그 숫자들이 거의
   벌어지지 않았습니다."

   🔴 이름 앞부분을 **안 적었다.** 원문이 준 것은 끝 글자 다섯(A·B·C·D·E)뿐이고 앞이 무슨
   글자였는지는 한 자도 없다. 그래서 앞 칸 셋은 빈 칸에 가로 획만 그은 **익명 자리**다.
   여기에 그럴듯한 이름을 채우면 그 순간 지어낸 문자열이 화면에 올라간다.

   🔴 결과 쪽에도 숫자를 안 붙였다. 다섯이 어떤 수로 바뀌었는지는 원문에 없다. 레일 위
   자국 다섯의 높이 차(8px)가 "거의 안 벌어졌다"를 지고, 그 옆 괄호가 그 폭을 가리킨다.
   괄호가 손톱만 한 것이 이 그림의 전부다.

   🔤 끝 글자와 그 칸은 코드에 그대로 있는 문자라 mono, 칸 이름(LAST CHAR)과 레일
   이름(HASH)은 인스펙터에 있을 법한 말이라 영어 disp 다.

   계속 도는 것 = 화살표 다섯의 점선 흐름, 그 위를 달리는 표식 다섯(위상이 어긋나 있다),
   다섯 행을 위에서 아래로 훑는 밝기 물결. 셋 다 t 의 함수이고 문턱이 없다. */

const FLOW = 2.0;    // 화살표 점선
const RUN = 2.6;     // 화살표 위를 달리는 표식
const WAVE = 3.1;    // 행을 훑는 물결
const OFF = [-4.5, -1.5, 1.0, 3.5, -2.5];   // 결과 다섯의 어긋남(픽셀)

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

    const chars = (spec.chars || []).slice(0, 5);
    const N = chars.length || 5;
    const rowY = i => 80 + i * 38;
    const CW = 20, CH = 24, LW = 28, LH = 26;
    const CX = [16, 40, 64];              // 익명 칸 셋
    const LX = 88;                        // 끝 글자 칸
    const AX = LX + LW + 10;              // 화살표 시작
    const RX = 268;                       // 결과 레일
    const CY = 156;                       // 결과가 몰리는 높이

    const nk = ease(cue(spec.nameCue ?? 0, 0.15, 0.85));
    const rowA = i => clamp(nk * 5 - i * 0.5);
    const arrowA = clamp((nk - 0.42) * 3);
    const hitA = clamp((nk - 0.62) * 3.2);
    const brkA = clamp((nk - 0.80) * 4);

    /* ── 칸 이름 ──────────────────────────────────── */
    if (spec.lastLabel) {
      ctx.globalAlpha = clamp(nk * 3);
      ctx.textAlign = 'center';
      ctx.font = disp(800, fit(spec.lastLabel, 800, 11, 96, 8));
      ctx.fillStyle = tone('accent');
      ctx.fillText(spec.lastLabel, LX + LW / 2, 58);
      ctx.globalAlpha = 1;
    }
    if (spec.hashLabel) {
      ctx.globalAlpha = arrowA;
      ctx.textAlign = 'center';
      ctx.font = disp(800, fit(spec.hashLabel, 800, 12, 110, 9));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.hashLabel, RX, 58);
      ctx.globalAlpha = 1;
    }
    ctx.textAlign = 'left';

    /* ── 이름 다섯 줄 ─────────────────────────────── */
    for (let i = 0; i < N; i++) {
      const a = rowA(i);
      if (a <= 0.02) continue;
      const y = rowY(i);

      /* 앞 칸 셋 — 다섯 줄이 전부 같다. 무슨 글자인지는 원문에 없어 획만 긋는다. */
      ctx.globalAlpha = a * 0.8;
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      for (const cx of CX) {
        roundRect(ctx, cx, y - CH / 2, CW, CH, 4); ctx.stroke();
        ctx.beginPath();
        ctx.moveTo(cx + 6, y); ctx.lineTo(cx + CW - 6, y);
        ctx.stroke();
      }
      ctx.globalAlpha = 1;

      /* 끝 한 칸 — 여기만 다르다 */
      ctx.globalAlpha = a;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, LX, y - LH / 2, LW, LH, 4); ctx.stroke();
      if (chars[i]) {
        ctx.textAlign = 'center';
        ctx.font = mono(700, 15); ctx.fillStyle = tone('accent');
        ctx.fillText(String(chars[i]), LX + LW / 2, y + 5.5);
        ctx.textAlign = 'left';
      }
      ctx.globalAlpha = 1;
    }

    /* ── 이름 → 값 ────────────────────────────────── */
    if (arrowA > 0.02) {
      for (let i = 0; i < N; i++) {
        const a = Math.min(arrowA, rowA(i));
        if (a <= 0.02) continue;
        const y0 = rowY(i), y1 = CY + OFF[i];

        /* 계속 도는 것 ③ — 물결이 지나가는 행이 굵어진다(문턱 없이 매끈하게) */
        const wv = 0.5 + 0.5 * Math.cos((frac(t / WAVE) - i / N) * Math.PI * 2);

        ctx.globalAlpha = a * (0.42 + 0.42 * wv);
        ctx.setLineDash([9, 7]);
        ctx.lineDashOffset = -(t * 15) % 16;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3 + 1.5 * wv;
        ctx.beginPath(); ctx.moveTo(AX, y0); ctx.lineTo(RX - 8, y1); ctx.stroke();
        ctx.setLineDash([]); ctx.lineDashOffset = 0;
        ctx.globalAlpha = 1;

        /* 계속 도는 것 ② — 화살표 위를 달리는 표식 */
        const u = frac(t / RUN + i * 0.17);
        ctx.globalAlpha = a * 0.9;
        ctx.fillStyle = tone('accent');
        ctx.beginPath();
        ctx.arc(lerp(AX, RX - 8, u), lerp(y0, y1, u), 5, 0, Math.PI * 2);
        ctx.fill();
        ctx.globalAlpha = 1;
      }
    }

    /* ── 값이 놓이는 레일 ─────────────────────────── */
    if (arrowA > 0.02) {
      ctx.globalAlpha = arrowA;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(RX, 66); ctx.lineTo(RX, 246); ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 다섯이 떨어진 자리 ───────────────────────── */
    if (hitA > 0.02) {
      for (let i = 0; i < N; i++) {
        ctx.globalAlpha = Math.min(hitA, rowA(i));
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(RX - 9, CY + OFF[i]); ctx.lineTo(RX + 9, CY + OFF[i]);
        ctx.stroke();
        ctx.globalAlpha = 1;
      }
    }

    /* ── 그 다섯이 차지한 폭 ──────────────────────── */
    if (brkA > 0.02) {
      const lo = CY + Math.min(...OFF.slice(0, N));
      const hi = CY + Math.max(...OFF.slice(0, N));
      ctx.globalAlpha = brkA;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(RX + 26, lo); ctx.lineTo(RX + 14, lo);
      ctx.lineTo(RX + 14, hi); ctx.lineTo(RX + 26, hi);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
