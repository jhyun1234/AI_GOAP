import {
  disp, mono, ease, clamp, frac,
  fitCanvas, mkCanvas, tone, roundRect
} from '../../../engine/lib.js';

/* hush — 있어야 할 신호가 없다.

   이 편의 콜드오픈. 씬에 직접 배치된 주민 셋이 나란히 서 있는데 말풍선 자리와
   성격 라벨 자리가 전부 비어 있다. 하나(맨 위, 게으름 주민)는 회색으로 원문 유일 대사
   '왜 나만 시키지..' 가 기대치로만 남아 있다 — 원래 저 자리에 이 대사가 들어왔어야 한다.
   하단에는 'error: none' 이 놓여 조용함이 실패가 아니라 그냥 조용함임을 확인한다.

   재작성 사유: 1차 컷은 위쪽에 원문에 없는 정상 주민 하나('탐구' 성격, '탐구하고 싶어!' 대사)
   를 대비군으로 뒀다가 반려됨. 여기서는 대비군을 지우고 조용한 셋만 남긴다 — '조용함'
   자체가 대비 없이도 읽히도록 빈 자리를 재료로 쓴다.

   회차의 기본 도형(방향의 획) 안에서 이 그림의 몫은 '있어야 할 방향의 신호가 나오지 않는다'.
   계속 도는 것 = 빈 말풍선 안에서 깜빡이는 커서 — 있어야 할 자리를 표시한다. */

const BLINK = 0.86;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const quiet = spec.quiet || [];
    const qk = ease(cue(spec.quietCue ?? 0, 0.1, 0.55));
    const ek = ease(cue(spec.expectCue ?? 1, 0.2, 0.65));

    ctx.textBaseline = 'alphabetic';

    /* 레이아웃 : 위 20px 여유, 하단 44px = 에러 로그. 나머지를 3행으로 나눈다. */
    const HEAD = 20;
    const FOOT = 44;
    const GAP = 10;
    const n = Math.max(1, quiet.length);
    const rowH = (h - HEAD - FOOT - GAP * (n - 1)) / n;

    quiet.forEach((q, i) => {
      const ry = HEAD + i * (rowH + GAP);
      const cxIcon = 24;
      const cyIcon = ry + rowH / 2;

      // 등장 : quietCue 로 전체 셋이 함께 뜬다
      ctx.globalAlpha = clamp(qk * 1.4);

      // 사람 아이콘
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      ctx.beginPath(); ctx.arc(cxIcon, cyIcon, 12, 0, Math.PI * 2); ctx.stroke();

      // 라벨
      ctx.textAlign = 'left';
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 9);
      ctx.fillText(q.label || '', cxIcon + 18, ry + 12);

      // 성격 자리 — 대시 셋
      const dashY = ry + 24;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      for (let d = 0; d < 3; d++) {
        const dx = cxIcon + 18 + d * 8;
        ctx.beginPath(); ctx.moveTo(dx, dashY); ctx.lineTo(dx + 5, dashY); ctx.stroke();
      }

      // 말풍선 자리 (오른쪽 큰 상자, 점선)
      const bx = cxIcon + 88;
      const bh = rowH - 8;
      const bw = w - bx - 6;
      const by = ry + 4;
      ctx.setLineDash([5, 4]);
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, bx, by, bw, bh, 4); ctx.stroke();
      ctx.setLineDash([]);

      // 커서 깜빡임 — 있어야 할 자리를 표시. 계속 도는 것.
      const bl = frac(t / BLINK + i * 0.31);
      if (bl < 0.5) {
        ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(bx + 10, by + 8); ctx.lineTo(bx + 10, by + bh - 8);
        ctx.stroke();
      }

      // 기대 대사 (화면에만 · 있는 자리만) — expectCue 로 뒤늦게 뜬다
      const expects = q.expects || '';
      if (expects) {
        ctx.globalAlpha = clamp(qk * 1.4) * 0.5 * ek;
        ctx.textAlign = 'left';
        ctx.fillStyle = tone('sub');
        let s = 10;
        ctx.font = disp(700, s);
        while (s > 8 && ctx.measureText(expects).width > bw - 26) { s -= 0.5; ctx.font = disp(700, s); }
        ctx.fillText(expects, bx + 18, by + bh / 2 + 4);
      }
      ctx.globalAlpha = 1;
    });

    /* ── 하단 : 에러 로그 자리도 비어 있다 ────────── */
    {
      const by = h - FOOT + 8;
      const bh = 26;
      ctx.globalAlpha = 0.9;
      ctx.setLineDash([5, 4]);
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, 12, by, w - 24, bh, 3); ctx.stroke();
      ctx.setLineDash([]);
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('sub'); ctx.font = mono(700, 10);
      ctx.fillText(spec.noError || 'error: none', w / 2, by + bh / 2 + 4);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
