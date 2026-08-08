import {
  disp, ease, clamp, frac,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* selfbuilt — 아무것도 안 지나는데 집이 선다.

   원문: "원래는 집이 부족하면 목수가 알아서 집을 짓고, 빈집이 생기면 무주택 주민에게
   자동으로 배정되게 해뒀습니다. 편리했죠. … 어차피 집이 알아서 지어지고 알아서
   배정되면, 누가 '집 좀 지어줄래?'라고 부탁할 이유가 없으니까요."

   🔴 이 회차의 기본 도형이 여기서 **「비어 있는 위쪽」**으로 나온다.
   택지 칸은 밑에서부터 저절로 빗금이 차오르는데, 그 칸으로 **들어오는 선이 하나도 없다.**
   칸마다 위로 뻗다 만 짧은 점선 스텁이 「여기로 무언가 와야 하는데 안 온다」를 진다.

   🔴 색이 뜻을 진다 — **강조색은 이 회차 내내 「부탁」 하나만 가리킨다.**
   집·택지·주민은 전부 흰색이고, 주민이 내보내려던 요청 캡슐만 강조색이다.
   그래서 마지막 샷에서 강조색이 사라진 집과 남은 집을 나란히 놓을 수 있다.

   🔴 캡슐은 **부풀었다 도로 꺼진다.** 등장 자체는 cue 에 물려 있고, 부풀기는
   t 의 연속 함수다(sin, 문턱 없음) — 「매번 말하려다 만다」가 뜻이므로 한 번 뜨고 마는
   것보다 되풀이가 맞다. 캡슐이 꺼진 자리에 아무 일도 안 일어나는 것이 이 샷의 결론이다.

   🟢 **2026-08-08 4단 구조 개정 — 자막이 둘에서 하나로 합쳐져 cue 를 하나만 쓴다.**
   전에는 buildCue(0)/askCue(1) 로 자막 두 줄에 나눠 걸었는데, 이 샷의 자막이
   「마을 게임에서 집은 부탁 없이도 알아서 지어지잖아요」 한 줄이 되면서 gnaw 와 같은
   방식으로 **같은 자막의 앞뒤 구간**으로 가른다(spec.split). split 은 「부탁」이 시작되는
   자리다 — 앞 절이 택지를 세우고, **「부탁」이라는 낱말이 나오는 순간 캡슐이 열린다.**
   🔴 그리고 캡슐 주기를 2.6 → **1.6초**로 줄였다. 캡슐이 보이는 구간이 자막 두 줄 시절의
   절반쯤으로 짧아져, 주기가 2.6 이면 위상에 따라 **부푸는 것을 한 번도 못 보고 지나갈 수**
   있다. 1.6 이면 보이는 구간(약 2.9초) 안에 반드시 한 번은 완전히 부푼다.

   🔴 화면에 숫자를 하나도 안 적는다. 택지 칸이 다섯인 것은 도형 파라미터이고
   '여러 자리'라는 뜻일 뿐이다 — 원문의 「주민이 넷뿐인」과 무관하고, 셀 값이 아니다.

   계속 도는 것 = 채워진 칸 안을 흐르는 빗금(칸 하나가 52×52 라 이 샷에서 가장 큰 면적) ·
   스텁 점선 · 캡슐의 부풀기 · 주민 표식의 숨. */

const N = 5, PW = 52, PH = 52, GAP = 10, PY = 212;
const STUB = 18;
const VILL = { x: 46, y: 126 };
const CAP = { w: 108, h: 28, cy: 74 };
const PUFF = 1.6;   // 요청이 부풀었다 꺼지는 주기(초) — 위 주석 참조

/** 아래에서 차오르는 빗금. k = 찬 비율, t = 흐름. */
function hatch(ctx, x, y, bw, bh, k, t, color) {
  if (k <= 0.004) return;
  const fh = bh * k, fy = y + bh - fh;
  ctx.save();
  ctx.beginPath(); ctx.rect(x, fy, bw, fh); ctx.clip();
  ctx.strokeStyle = color; ctx.lineWidth = 3;
  const step = 13, off = (t * 11) % step;
  for (let d = -bh - step; d < bw + step; d += step) {
    ctx.beginPath();
    ctx.moveTo(x + d + off, y + bh);
    ctx.lineTo(x + d + off + bh, y);
    ctx.stroke();
  }
  ctx.restore();
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

    const X0 = 20;
    const total = N * PW + (N - 1) * GAP;
    const px0 = (w - total) / 2;

    const c0 = cue(spec.buildCue ?? 0, 0.15, 0.85);
    /* 자막 한 줄을 앞뒤로 가른다 — split 은 그 줄에서 「부탁」이 시작되는 지점이다 */
    const split = spec.split ?? 0.43;
    const c1 = clamp((c0 - split) / Math.max(1e-6, 1 - split));

    /* ── 라벨 ─────────────────────────────────────── */
    if (spec.buildLabel) {
      const a = clamp(c0 * 5);
      if (a > 0.02) {
        ctx.globalAlpha = a;
        ctx.textAlign = 'left';
        ctx.font = disp(900, fit(spec.buildLabel, 900, 13, w - 44, 9));
        ctx.fillStyle = tone('ink');
        ctx.fillText(spec.buildLabel, X0, 34);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 택지 칸 — 아무 선도 안 닿는데 밑에서부터 찬다 ── */
    for (let i = 0; i < N; i++) {
      const x = px0 + i * (PW + GAP);
      const app = clamp(c0 * 6 - i * 0.12);
      if (app <= 0.02) continue;

      /* 위로 뻗다 마는 스텁 — 들어와야 할 것이 안 들어온다 */
      ctx.globalAlpha = app * 0.9;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.setLineDash([5, 6]);
      ctx.lineDashOffset = (t * 9) % 11;
      ctx.beginPath();
      ctx.moveTo(x + PW / 2, PY);
      ctx.lineTo(x + PW / 2, PY - STUB);
      ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;

      /* 빈 자리 */
      ctx.globalAlpha = app;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, x, PY, PW, PH, 5); ctx.stroke();
      ctx.globalAlpha = 1;

      /* 차오름 */
      const k = ease(clamp((c0 - (0.08 + i * 0.15)) / 0.28));
      if (k > 0.004) {
        hatch(ctx, x, PY, PW, PH, k, t, tone('ink'));
        ctx.globalAlpha = clamp(k * 2.2);
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        roundRect(ctx, x, PY, PW, PH, 5); ctx.stroke();
        ctx.globalAlpha = 1;
      }
    }

    /* ── 주민 ─────────────────────────────────────── */
    const va = clamp(c0 * 6);
    if (va > 0.02) {
      ctx.globalAlpha = va;
      ctx.fillStyle = tone('ink');
      ctx.beginPath();
      ctx.arc(VILL.x, VILL.y, 7 + Math.sin(t * 2.3) * 1.1, 0, Math.PI * 2);
      ctx.fill();
      ctx.globalAlpha = 1;
    }

    /* ── 부풀었다 꺼지는 요청 ──────────────────────
       등장은 cue(자막의 「부탁」 지점), 부풀기는 t 의 연속 함수다. ph 가 0.62 를 넘으면
       sin(π)=0 이라 값이 이어진 채 사라진다 — 시각 문턱이 아니다. */
    const gate = clamp(c1 * 3.2);
    if (gate > 0.02 && spec.askLabel) {
      const ph = frac(t / PUFF);
      const puff = Math.sin(Math.PI * clamp(ph / 0.62));
      const a = gate * puff;
      if (a > 0.02) {
        const sc = 0.72 + 0.28 * puff;
        const cw = CAP.w * sc, ch = CAP.h * sc;
        const cx = Math.min(VILL.x + 72, w - 24 - cw / 2);
        const cyy = CAP.cy;

        /* 꼬리 — 주민에서 캡슐로 뻗다 만다 */
        ctx.globalAlpha = a * 0.8;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(VILL.x, VILL.y - 10);
        ctx.lineTo(cx - cw * 0.18, cyy + ch / 2 + 8 * puff);
        ctx.stroke();

        ctx.globalAlpha = a;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        roundRect(ctx, cx - cw / 2, cyy - ch / 2, cw, ch, ch / 2); ctx.stroke();

        ctx.textAlign = 'center';
        ctx.font = disp(900, fit(spec.askLabel, 900, 13 * sc, cw - 18, 8));
        ctx.fillStyle = tone('accent');
        ctx.fillText(spec.askLabel, cx, cyy + 4.5 * sc);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
