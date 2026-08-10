import { ease, clamp, lerp, disp, tone, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* twoends — 훅. 이 회차의 기본 도형(「한 칸의 폭」)을 처음 세우는 그림이다.

   회차 축 한 문장: **한 걸음의 폭이 모든 것을 정한다.**
   여기서는 그 축을 「양 끝」 배치로 보여 준다 — 한 줄의 왼쪽 끝에 「간호」 칸,
   오른쪽 끝에 「돌 캐기」 칸이 있고 **둘 사이를 잇는 선은 한 획도 없다.**
   그런데 왼쪽 칸이 좁아지는 순간 줄 전체에 그 폭으로 눈금이 왼쪽부터 번지고,
   맨 오른쪽 「돌 캐기」 칸의 실선 테두리가 점선으로 풀린다.
   자막을 가려도 「상관없어 보이는 둘이 자 하나를 같이 쓰고 있다」가 읽힌다.

   🔴 이 회차의 다른 그림과 겹치지 않게 배치를 나눴다 —
   여기는 **양 끝(가로)**, S1 은 **한 줄 확대(글쓰기)**, S2 는 **자와 띠(계측)**,
   S3 은 **자라나는 줄과 두 선(한계)**. 도형은 하나인데 배치가 넷이다.

   🔴 색은 층으로 갈랐다. 축선 **위** = 칸(간호=강조색 / 돌 캐기=흰색),
   축선 **아래** = 눈금(흰 계열)뿐이고, 그 사이를 8px 띄웠다.
   칸 아래 획의 끝이 217.5, 축선과 눈금 시작이 224 라 **어느 위상에서도 6.5px 이 뜬다.**
   글로우(그림자)는 이 회차에서 한 곳도 쓰지 않았다 — 강조색 번짐이 아래 눈금(흰)에
   얹히는 경로를 아예 없애려고 뺐다.

   세로 예산(캔버스 307) — 구획 이름 40 · 칸 라벨 146 · 칸 156~216 · 축선 224 ·
   눈금 224~244(파도 최대). 최저 244(선 반폭 포함 245.5). */

const M = 22;
const AXIS = 224;
const CELL_H = 60;
/* 🔴 칸 바닥을 축선에서 8px 띄웠다. 붙여 놓으면 강조색 「간호」 칸의 아래 획
   (경로 y 에서 ±1.5)과 축선·눈금(흰 계열)이 같은 픽셀에 닿는다 —
   칸 바닥 216(획 아래끝 217.5) / 축선 224 / 눈금 시작 224 로 6.5px 이 뜬다. */
const CELL_BOT = AXIS - 8;
const LABEL_Y = 146;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    /* 가운데 정렬 글자는 폭을 재서 캔버스 안쪽에 가둔다 */
    const put = (txt, cx, by, weight, size, color, maxW) => {
      if (!txt) return;
      let fs = size; ctx.font = disp(weight, fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(weight, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.fillStyle = color;
      ctx.textAlign = 'center';
      ctx.fillText(txt, clamp(cx, M + tw / 2, w - M - tw / 2), by);
      ctx.textAlign = 'left';
    };

    const k = ease(cue(spec.cue ?? 0, 0.15, 0.72));

    /* ── 구획 이름 ─────────────────────────────────── */
    ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
    ctx.fillText(spec.axisLabel ?? '', M, 40);

    /* ── 축선 (계속 흐른다 = 정적 구간 대책) ────────── */
    ctx.save();
    ctx.setLineDash([10, 8]);
    ctx.lineDashOffset = -((t * 18) % 18);
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(M, AXIS); ctx.lineTo(w - M, AXIS); ctx.stroke();
    ctx.restore();

    /* ── 눈금 : 왼쪽부터 오른쪽으로 번진다 ───────────
       폭(pitch)이 좁아지는 것이 이 회차의 사건이다. 파도는 t 로 돌아
       자막이 끝난 뒤에도 화면이 멎지 않는다. */
    const pitch = lerp(spec.pitchFrom ?? 30, spec.pitchTo ?? 6, k);
    const front = M + (w - 2 * M) * clamp(k * 1.25);
    ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
    for (let x = M + pitch; x < w - M; x += pitch) {
      if (x > front) break;
      const len = 13 + 7 * Math.sin(x * 0.055 - t * 3.4);
      ctx.beginPath(); ctx.moveTo(x, AXIS); ctx.lineTo(x, AXIS + len); ctx.stroke();
    }

    /* ── 왼쪽 끝 : 「간호」 칸이 좁아진다 (강조색) ──── */
    {
      const cw = lerp(spec.wideW ?? 78, spec.narrowW ?? 15, k);
      const x = M + 4;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, x, CELL_BOT - CELL_H, cw, CELL_H, 4); ctx.stroke();
      put(spec.leftLabel, x + cw / 2, LABEL_Y, 800, 17, tone('accent'), 118);
    }

    /* ── 오른쪽 끝 : 「돌 캐기」 칸이 점선으로 풀린다 ──
       실선과 점선을 알파로 교차시킨다. 둘 다 흰색이라 팔레트는 한 색뿐이고,
       문턱으로 색을 바꾸지 않으므로 프레임이 튀지 않는다. */
    {
      const cw = spec.rightW ?? 86;
      const x = w - M - 4 - cw;
      ctx.lineWidth = 3;

      ctx.save();
      ctx.globalAlpha = 1 - k;
      ctx.strokeStyle = tone('ink');
      roundRect(ctx, x, CELL_BOT - CELL_H, cw, CELL_H, 4); ctx.stroke();
      ctx.restore();

      ctx.save();
      ctx.globalAlpha = k;
      ctx.setLineDash([7, 7]);
      ctx.lineDashOffset = (t * 14) % 14;
      ctx.strokeStyle = tone('ink');
      roundRect(ctx, x, CELL_BOT - CELL_H, cw, CELL_H, 4); ctx.stroke();
      ctx.restore();

      put(spec.rightLabel, x + cw / 2, LABEL_Y, 800, 17, tone('ink'), 128);
    }
  }
};
