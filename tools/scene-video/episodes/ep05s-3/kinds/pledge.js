import {
  disp, ease, clamp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* pledge — 종이에는 값이 적혀 있는데, 그 아래 도장 자리는 아직 비어 있다.

   이 편의 ①어긋난 상태가 여기 있다. paperCue 에서 종이 한 장이 떠오르고 위쪽에
   원문 표기 그대로 「기존 계열 액션 추가 비용」이 붙는다. quoteCue 에서 그 값이
   「에셋 1개, 코드 0줄」로 적힌다 — 원문이 따옴표로 묶어 부른 문자열이다.
   그리고 종이 아래에 **점선 도장 자리 두 칸**이 처음부터 비어 있다. 이 샷의 사건은
   무엇이 나타나는 것이 아니라 **무엇이 비어 있는가**다.

   🔴 이 두 칸이 이 회차의 척추다. S3 과 S4 가 하나씩 채우고, 마지막 샷 twice 에서
   그 둘이 가운데로 모여 포개진다. 그래서 자리를 좌우로 벌려 뒀다 — 세로로 쌓으면
   마지막 샷의 '가운데로 모인다'가 이어지지 않는다.

   🔴 문서를 쌓거나 항목을 지우는 그림(앞선 회차의 서류철·장부)과 갈랐다. 여기 종이는
   한 장뿐이고 아무것도 안 쌓인다. 비용표를 표로 그리지도 않았다 — 원문이 그 표를
   인용하지 않았고, 표를 그리면 원문에 없는 계열 이름들이 화면에 뜬다.

   계속 도는 것 = 종이 전체의 부유(주기 4.6초·진폭 2.4px, 테두리 792px 가 통째로 움직인다),
   도장 자리 두 칸의 점선 흐름. */

const FLOAT = 4.6;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));

    const pk = ease(cue(spec.paperCue ?? 0, 0.15, 0.6));
    const qk = ease(cue(spec.quoteCue ?? 1, 0.15, 0.66));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    /* 계속 도는 것 — 종이가 아주 느리게 뜬다 */
    const fy = 2.4 * Math.sin(frac(t / FLOAT) * Math.PI * 2);
    const PX = 44, PW = w - 88, PY = 30 + fy, PH = 132;

    /* ── 종이 ─────────────────────────────────────── */
    if (pk > 0.02) {
      const k = clamp(pk * 2);
      const s = Math.min(1, spring(clamp(pk * 1.3)));

      if (spec.docLabel) {
        ctx.globalAlpha = k * 0.9;
        ctx.textAlign = 'left';
        const fs = fit(spec.docLabel, 700, 10.5, 180, 8);
        ctx.font = disp(700, fs); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.docLabel, PX, PY - 10);
        ctx.globalAlpha = 1;
      }

      ctx.globalAlpha = k;
      ctx.lineWidth = 4; ctx.strokeStyle = tone('ink');
      roundRect(ctx, PX + (PW - PW * s) / 2, PY + (PH - PH * s) / 2, PW * s, PH * s, 4);
      ctx.stroke();

      // 접힌 모서리 — 종이라는 것을 도형으로만 말한다
      ctx.beginPath();
      ctx.moveTo(PX + PW - 22, PY + 3);
      ctx.lineTo(PX + PW - 22, PY + 22);
      ctx.lineTo(PX + PW - 3, PY + 22);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    if (pk > 0.4 && spec.costLabel) {
      const k = clamp((pk - 0.4) / 0.4);
      ctx.globalAlpha = k;
      ctx.textAlign = 'left';
      const fs = fit(spec.costLabel, 700, 11.5, PW - 60, 8.5);
      ctx.font = disp(700, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.costLabel, PX + 18, PY + 36);

      ctx.globalAlpha = k * 0.9;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(PX + 18, PY + 50); ctx.lineTo(PX + PW - 18, PY + 50);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 적힌 값 ──────────────────────────────────── */
    if (qk > 0.02 && spec.quote) {
      const k = clamp(qk * 2);
      const s = Math.min(1, spring(clamp(qk * 1.25)));
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.quote, 900, 21, PW - 40, 13);
      ctx.font = disp(900, fs * s); ctx.fillStyle = tone('accent');
      if (qk > 0.7) setShadow(ctx, GLOW, 12, 0);
      ctx.fillText(spec.quote, w / 2, PY + 98);
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    /* ── 빈 도장 자리 둘 ──────────────────────────── */
    const SW = 104, SH = 52, SY = 196;
    if (pk > 0.35) {
      const k = clamp((pk - 0.35) / 0.5);
      [w / 2 - 60, w / 2 + 60].forEach(scx => {
        ctx.globalAlpha = k * 0.95;
        ctx.setLineDash([9, 7]);
        ctx.lineDashOffset = -(t * 13) % 16;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        roundRect(ctx, scx - SW / 2, SY, SW, SH, 4); ctx.stroke();
        ctx.setLineDash([]); ctx.lineDashOffset = 0;
        ctx.globalAlpha = 1;
      });
    }

    /* ── 아직 종이 위 ─────────────────────────────── */
    if (qk > 0.45 && spec.pending) {
      const k = clamp((qk - 0.45) / 0.45);
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.pending, 900, 15, w - 40, 10);
      ctx.font = disp(900, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(spec.pending, w / 2, 274);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
