import { ease, clamp, lerp, disp, tone, fitCanvas, mkCanvas, roundRect, depthGrad }
  from '../../../engine/lib.js';

/* notch — 원인. 회차 축(「한 칸의 폭」)의 **계측** 배치다.

   플래너는 「액션 하나당 최소 이 정도는 든다」는 값을 자의 눈금으로 쓴다.
   위에서 「간호 1」 칩이 내려와 앉으면 **최소 비용 표시가 5에서 1로 바뀌고**,
   그 순간 자의 눈금이 5분의 1 폭으로 촘촘해지며 축선 아래 「어림값」 띠가
   정확히 5분의 1 길이로 줄어든다. 남은 거리는 하나도 안 변했는데 어림값만 준다.

   🔴 층으로 색을 갈랐다.
     · 축선 **위**(y 158~176) = 눈금과 걸음 표식 — 전부 흰 계열
     · 축선 **아래**(y 196~212) = 어림값 띠 — 강조색
     · 값 글자(강조색)는 baseline 132 라 눈금 최상단 158 보다 26px 위다
     · 「간호 1」 칩(강조색)은 y 70~104, 바로 아래 흰 라벨의 글자 윗변이 약 123 이라
       19px 뜬다. 값 숫자가 아래에서 미끄러져 들어올 때의 **최저 위치까지 넣어도**
       글자 아랫변이 144 라 눈금 158 에 14px 못 미친다(움직이는 요소를 최악의
       순간으로 재라는 규칙).
   🔴 글로우(그림자)는 쓰지 않았다. 강조색 번짐이 위쪽 흰 눈금에 얹히는 경로를
      아예 없앴다. 대신 띠는 depthGrad 로 같은 색 안에서 깊이를 준다.

   ⏱ 정적 구간 대책 둘 — ①축선의 점선이 계속 흐른다(길이 308px 짜리 획이라
      갱신 면적이 크다) ②걸음 표식이 **한 눈금씩** 뛰어간다. 눈금이 좁아지면
      걸음도 잘아지는데, 그게 곧 이 샷의 뜻이라 장식이 아니다.

   세로 예산(캔버스 307) — 구획 이름 40 · 칩 70~104 · 값/라벨 132 ·
   눈금 158~176 · 축선 176 · 띠 196~212 · 아래 라벨 236. 최저 240(디센더 포함). */

const M = 22;
const AXIS = 176;
const TICK_TOP = 158;
const BAR_Y = 196, BAR_H = 16;
const CHIP_W = 84, CHIP_H = 34;
const VAL_Y = 132;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const c0 = cue(spec.cue ?? 0, 0.15, 0.85);
    const seat = ease(clamp((c0 - 0.10) / 0.30));   // 칩이 내려와 앉는다
    const shr = ease(clamp((c0 - 0.52) / 0.34));    // 눈금이 좁아지고 띠가 준다

    /* ── 구획 이름 ─────────────────────────────────── */
    ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
    ctx.fillText(spec.panelLabel ?? '', M, 40);

    /* ── 「간호 1」 칩 — 위에서 내려와 앉는다 ────────── */
    {
      /* 🔴 시작 높이 52 는 구획 이름(흰색, baseline 40)과의 간격에서 나왔다 —
         칩은 x 로 그 라벨과 겹치므로 세로로 못 만나게 12px 을 띄웠다.
         내려오는 도중의 어떤 위상에서도 칩 윗변이 52 아래다. */
      const y = lerp(52, 70, seat);
      ctx.save();
      ctx.globalAlpha = clamp(seat * 1.6);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, M + 6, y, CHIP_W, CHIP_H, 6); ctx.stroke();
      ctx.font = disp(800, 15);
      const s = spec.chipLabel ?? '';
      const tw = ctx.measureText(s).width;
      ctx.fillStyle = tone('accent');
      ctx.fillText(s, M + 6 + (CHIP_W - tw) / 2, y + 23);
      ctx.restore();
    }

    /* ── 최소 비용 : 5 가 1 로 바뀐다 ────────────────
       글자는 못 섞으므로 두 숫자를 **세로로 스쳐 지나가며** 교차시킨다.
       둘 다 위로 움직이므로 아래쪽 눈금 띠에 절대 안 닿는다. */
    ctx.font = disp(700, 12.5); ctx.fillStyle = tone('sub');
    ctx.fillText(spec.minLabel ?? '', M, VAL_Y);
    const valX = M + 4 + ctx.measureText(spec.minLabel ?? '').width + 14;
    {
      ctx.font = disp(900, 26);
      const g = clamp((shr - 0.05) / 0.30);
      ctx.save();
      ctx.globalAlpha = 1 - g;
      ctx.fillStyle = tone('accent');
      /* 위로 8px 만 빠진다 — 더 올리면 사그라드는 「5」가 바로 위 칩(같은 강조색)의
         아래 획에 닿아 판독이 흐려진다(칩 아래 획 끝 105.5 · 글자 윗변 109). */
      ctx.fillText(String(spec.minFrom ?? ''), valX, VAL_Y - 8 * g);
      ctx.restore();
      ctx.save();
      ctx.globalAlpha = g;
      ctx.fillStyle = tone('accent');
      ctx.fillText(String(spec.minTo ?? ''), valX, VAL_Y + 12 * (1 - g));
      ctx.restore();
    }

    /* ── 자 : 축선 + 눈금 ──────────────────────────── */
    ctx.save();
    ctx.setLineDash([9, 8]);
    ctx.lineDashOffset = -((t * 17) % 17);
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(M, AXIS); ctx.lineTo(w - M, AXIS); ctx.stroke();
    ctx.restore();

    const pitch = lerp(spec.pitchFrom ?? 30, spec.pitchTo ?? 6, shr);
    ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
    for (let x = M + pitch; x < w - M; x += pitch) {
      ctx.beginPath(); ctx.moveTo(x, TICK_TOP); ctx.lineTo(x, AXIS); ctx.stroke();
    }

    /* ── 걸음 표식 — 한 눈금씩 뛴다 (흰색, 눈금과 같은 계열) ── */
    {
      const per = 0.30;                       // 한 걸음에 걸리는 초
      const stepN = Math.floor(t / per);
      const fr = (t / per) - stepN;
      const lane = w - M * 2 - 24;
      const total = Math.max(2, Math.round(lane / pitch));
      const i = stepN % total;
      const x = M + 6 + ((i + ease(fr)) * pitch) % lane;
      /* 뛰는 높이 6px 은 위쪽 값 글자와의 간격에서 나왔다 — 값이 가장 낮게
         내려오는 순간의 글자 아랫변이 144 이고, 표식 테두리 윗변은 150.5 다. */
      const hop = -6 * Math.sin(Math.PI * clamp(fr));
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, x, TICK_TOP + hop, 20, 18, 3); ctx.stroke();
    }

    /* ── 어림값 띠 — 정확히 5분의 1로 준다 ──────────── */
    {
      const full = w - M * 2 - 18;
      const len = full * lerp(1, (spec.ratio ?? 0.2), shr);
      ctx.fillStyle = depthGrad(ctx, M, BAR_Y, M + full, BAR_Y + BAR_H, 'accent');
      ctx.fillRect(M, BAR_Y, Math.max(6, len), BAR_H);
    }

    /* ── 아래 라벨 : 「어림값」 과 「5분의 1」 ─────────
       흰 라벨과 강조색 라벨이 같은 가로줄에 있으므로 x 를 재서 갈라 놓는다. */
    ctx.font = disp(700, 12.5);
    const el = spec.estLabel ?? '';
    ctx.fillStyle = tone('sub');
    ctx.fillText(el, M, 236);
    const ew = ctx.measureText(el).width;
    if (shr > 0.35) {
      ctx.save();
      ctx.globalAlpha = clamp((shr - 0.35) / 0.30);
      ctx.font = disp(800, 13.5);
      ctx.fillStyle = tone('accent');
      ctx.fillText(spec.ratioLabel ?? '', M + ew + 16, 236);
      ctx.restore();
    }
  }
};
