import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, depthGrad, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* costgap — S1 의 세로 기준선을 눕힌 것. 이번엔 값이 오른쪽으로 선을 넘어간다.

   원문: "평시에는 생식 몇 개로 조리식 하나가 나오지만, 여름과 겨울 같은 위기 계절에는
   더 많은 재료를 넣어야 같은 조리식 하나가 나옵니다. 힘든 계절엔 요리가 비싸지는 겁니다." /
   "그러면 요리사가 \"재료 충분해!\"라고 판단해 요리를 시작했는데 정작 레시피는 못 채우는
   상황이 생깁니다. 플래너는 계획을 못 찾고 헛돌게 되고요."

   🔴 **재료를 칸으로 안 그렸다.** 칸으로 그리면 세어지고, 그 순간 「생식 몇 개」의 '몇'이
   화면에 생긴다 — 원문에 그 값이 없다. 띠는 셀 수 없다.

   ① costCue — 위기 계절 표가 켜지고 비용 띠가 오른쪽으로 길어져 강조색 한계선을 넘어간다
      (넘어간 부분만 강조색이다). 요리사는 「재료 충분해!」라고 판단해 시작하지만, 진행 띠는
      한계선 앞에서 멈추고 그 앞은 강조색 점선으로 비어 흐른다.

   🔴 흰 것과 강조색을 같은 픽셀에 겹치지 않는다. 흰 것은 전부 x ≤ 168 에서 끊고 강조색은
   x ≥ 184 에서 시작한다 — 그 사이 16px 안에 강조색 한계선(x 176, 선폭 3)만 있다.
   클립으로 띠를 두 장 맞대어 가르지 않는다. 흰 구간과 강조 구간의 x 를 각각 계산해
   서로 겹치지 않는 두 번의 fillRect 로 그린다(맞닿은 클립이 글자를 두 번 그린 사고 회피).

   계속 도는 것 = 한계선 점선의 흐름 · 도달 못한 구간 점선의 흐름 · 아래쪽 자 눈금의 흐름. */

const X0 = 22;
const WHITE_END = 168;     // 흰 것은 여기서 끊는다
const LIMIT_X = 176;       // 강조색 한계선
const ACC_START = 184;     // 강조색은 여기서 시작한다
const COST_MAX = 244;

const A_BAR = 70, B_BAR = 120, C_BAR = 176, BAR_H = 16;

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
    const lab = (txt, x, y, max) => {
      if (!txt) return;
      ctx.textAlign = 'left';
      ctx.font = disp(900, fit(txt, 900, 11, max, 8));
      ctx.fillStyle = tone('sub');
      ctx.fillText(txt, x, y);
    };

    const c0 = cue(spec.costCue ?? 0, 0.15, 0.78);
    const appear = ease(clamp(c0 / 0.16));
    if (appear <= 0.004) { ctx.textAlign = 'left'; return; }

    const chipA = ease(clamp((c0 - 0.16) / 0.16));
    const grow = ease(clamp((c0 - 0.20) / 0.28));
    const bubbleA = ease(clamp((c0 - 0.50) / 0.16));
    const prog = ease(clamp((c0 - 0.56) / 0.22));
    const stallA = ease(clamp((c0 - 0.78) / 0.18));

    /* ── 아래쪽 자 — 자막과 무관하게 흐른다 ─────────── */
    ctx.globalAlpha = appear * 0.9;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    const step = 24, sl = (t * 22) % step;
    for (let i = -1; i * step + 16 - sl < w - 12; i++) {
      const x = 16 + i * step - sl;
      if (x < 14 || x > w - 14) continue;
      ctx.beginPath(); ctx.moveTo(x, 266); ctx.lineTo(x, 274); ctx.stroke();
    }
    ctx.beginPath(); ctx.moveTo(14, 266); ctx.lineTo(w - 14, 266); ctx.stroke();
    ctx.globalAlpha = 1;

    /* ── 재료 (그대로) ─────────────────────────── */
    ctx.globalAlpha = appear;
    lab(spec.stockLabel, X0, A_BAR - 8, 120);
    setShadow(ctx, 'rgba(255,255,255,0.18)', 10);
    ctx.fillStyle = depthGrad(ctx, X0, A_BAR, WHITE_END, A_BAR + BAR_H, 'ink');
    roundRect(ctx, X0, A_BAR, WHITE_END - X0, BAR_H, 5); ctx.fill();
    clearShadow(ctx);
    ctx.globalAlpha = 1;

    /* ── 레시피 비용 — 길어져 선을 넘어간다 ────────── */
    ctx.globalAlpha = appear;
    lab(spec.costLabel, X0, B_BAR - 8, 120);
    const end = lerp(150, COST_MAX, grow);
    const whiteTo = Math.min(end, WHITE_END);
    ctx.fillStyle = depthGrad(ctx, X0, B_BAR, whiteTo, B_BAR + BAR_H, 'ink');
    roundRect(ctx, X0, B_BAR, whiteTo - X0, BAR_H, 5); ctx.fill();
    if (end > ACC_START + 2) {
      setShadow(ctx, GLOW, 12);
      ctx.fillStyle = depthGrad(ctx, ACC_START, B_BAR, end, B_BAR + BAR_H, 'accent');
      roundRect(ctx, ACC_START, B_BAR, end - ACC_START, BAR_H, 5); ctx.fill();
      clearShadow(ctx);
    }
    ctx.globalAlpha = 1;

    /* ── 요리 진행 — 선 앞에서 멈춘다 ───────────── */
    ctx.globalAlpha = appear;
    lab(spec.cookLabel, X0, C_BAR - 8, 120);
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    roundRect(ctx, X0, C_BAR, COST_MAX - X0, 14, 7); ctx.stroke();
    const done = lerp(X0, WHITE_END - 8, prog);
    if (done > X0 + 2) {
      ctx.fillStyle = tone('ink');
      roundRect(ctx, X0 + 3, C_BAR + 3, done - X0 - 3, 8, 4); ctx.fill();
    }
    if (stallA > 0.02) {
      ctx.globalAlpha = appear * stallA;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.setLineDash([10, 8]);
      ctx.lineDashOffset = -(t * 30) % 18;
      ctx.beginPath();
      ctx.moveTo(ACC_START, C_BAR + 7); ctx.lineTo(COST_MAX - 4, C_BAR + 7);
      ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
    }
    ctx.globalAlpha = 1;

    /* ── 한계선 — 이 회차의 「선」 ────────────────── */
    ctx.globalAlpha = appear;
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
    ctx.setLineDash([11, 8]);
    ctx.lineDashOffset = (t * 20) % 19;
    ctx.beginPath(); ctx.moveTo(LIMIT_X, 56); ctx.lineTo(LIMIT_X, 200); ctx.stroke();
    ctx.setLineDash([]); ctx.lineDashOffset = 0;
    ctx.globalAlpha = 1;

    /* ── 위기 계절 표 ─────────────────────────── */
    if (chipA > 0.02 && spec.seasonLabel) {
      const bx = 240, bw = w - 18 - bx;
      ctx.globalAlpha = chipA;
      setShadow(ctx, GLOW, 12);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, bx, 28, bw, 26, 8); ctx.stroke();
      clearShadow(ctx);
      ctx.textAlign = 'center';
      ctx.font = disp(900, fit(spec.seasonLabel, 900, 11, bw - 14, 7.5));
      ctx.fillStyle = tone('accent');
      ctx.fillText(spec.seasonLabel, bx + bw / 2, 46);
      ctx.globalAlpha = 1;
    }

    /* ── 「재료 충분해!」 ──────────────────────── */
    if (bubbleA > 0.02 && spec.quote) {
      const bx = X0, bw = 172, by = 212, bh = 36;
      ctx.globalAlpha = bubbleA;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, bx, by, bw, bh, 9); ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(bx + 26, by); ctx.lineTo(bx + 20, by - 11); ctx.lineTo(bx + 44, by);
      ctx.closePath();
      ctx.fillStyle = tone('bg'); ctx.fill();
      ctx.stroke();
      ctx.textAlign = 'center';
      ctx.font = disp(900, fit(spec.quote, 900, 15, bw - 22, 9));
      ctx.fillStyle = tone('ink');
      ctx.fillText(spec.quote, bx + bw / 2, by + bh / 2 + 6);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
