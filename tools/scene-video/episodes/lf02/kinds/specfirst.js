import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* specfirst — 명세 칸이 먼저 채워져야 오른쪽 커밋 칸이 하나씩 열린다.
   왼쪽 CODE 칸은 파이프의 시작점이 아니라 **끝**에 있다 — 이 프로젝트에서 코드는
   명세의 번역이라(N3), 순서가 그림의 전부다.
   연속 모션 = 파이프를 따라 계속 흐르는 표식. 명세 칸이 비어 있는 동안에는 그 표식이
   명세 칸 앞에서 되돌아온다(코드로 못 넘어간다). */

const ITEMS = ['W1', 'W2', 'W3'];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    const kSpec = ease(cue(1));    // 실행명세서를 먼저 쓴다
    const kBound = ease(cue(2));   // 무엇을 하지 않을지까지
    const kOne = ease(cue(3));     // 명세 1항목 = 커밋 1개
    const kBuild = ease(cue(4));   // 빌드 깨진 커밋 금지

    // ── 왼쪽 : SPEC 카드 ───────────────────────────
    const sx = M, sy = 46, sw = 186, sh = h - sy - 46;

    /* 🔴 2차 개정 (검수 C6): 연속 모션이 반지름 5px 흐름점 하나뿐이라 문턱 아래였다.
       명세 → 커밋으로 번역돼 가는 흐름을 48px 폭 띠로 세운다. 카드·칸보다 **먼저**
       그려서 강조색 위에 반투명 흰색이 얹히지 않게 한다. */
    {
      const BR = 3.0, bk = (t % BR) / BR;
      const bx0 = lerp(sx - 22, w - M + 22, bk);
      ctx.fillStyle = tone('track');
      ctx.fillRect(bx0 - 24, sy - 14, 48, sh + 28);
    }
    ctx.strokeStyle = kSpec > 0.1 ? tone('accent') : tone('track');
    ctx.lineWidth = 3;
    roundRect(ctx, sx, sy, sw, sh, 4); ctx.stroke();
    ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('실행명세서', sx, sy - 12);

    // 명세 안 : 한다 / 안 한다 두 층
    for (let i = 0; i < 2; i++) {
      const ry = sy + 18 + i * 46;
      const k = i === 0 ? kSpec : kBound;
      ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText(i === 0 ? '무엇을 한다' : '무엇을 안 한다', sx + 12, ry + 10);
      const bx = sx + 12, bw = sw - 24;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, bx, ry + 16, bw, 20, 3); ctx.stroke();
      if (k > 0.02) {
        ctx.fillStyle = tone('accent');
        ctx.fillRect(bx + 4, ry + 22, (bw - 8) * ease(k), 8);
      }
    }
    // 명세 칸 하단 : 1항목
    if (kOne > 0.02) {
      ctx.save(); ctx.globalAlpha = clamp(kOne);
      ctx.font = disp(700, 11); ctx.fillStyle = tone('accent');
      ctx.fillText('1항목', sx + 12, sy + sh - 12);
      ctx.restore();
    }

    // ── 파이프 ────────────────────────────────────
    const pipeY = sy + sh / 2;
    const px0 = sx + sw + 8, px1 = w - M - 214;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(px0, pipeY); ctx.lineTo(px1, pipeY); ctx.stroke();

    // 연속 모션 : 흐름 표식 — 명세가 비면 파이프 입구에서 되돌아온다
    {
      const PR = 2.2, k = (t % PR) / PR;
      const reach = kSpec > 0.35 ? 1 : 0.22;
      const fx = lerp(px0, px0 + (px1 - px0) * reach, ease(k));
      ctx.fillStyle = kSpec > 0.35 ? tone('accent') : tone('sub');
      ctx.beginPath(); ctx.arc(fx, pipeY, 5, 0, Math.PI * 2); ctx.fill();
    }

    // 파이프 중간의 문 — 명세가 없으면 닫혀 있다
    {
      const gx = px0 + (px1 - px0) * 0.3;
      const open = clamp(kSpec * 1.4);
      ctx.strokeStyle = open > 0.6 ? tone('track') : tone('ink');
      ctx.lineWidth = open > 0.6 ? 3 : 5;
      const gap = lerp(0, 16, ease(open));
      ctx.beginPath();
      ctx.moveTo(gx, pipeY - 26); ctx.lineTo(gx, pipeY - gap / 2);
      ctx.moveTo(gx, pipeY + gap / 2); ctx.lineTo(gx, pipeY + 26);
      ctx.stroke();
    }

    // ── 오른쪽 : COMMIT 칸 셋 ──────────────────────
    const cx0 = w - M - 200, cw = 62, ch = 40, gap = 7;
    ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('커밋', cx0, sy - 12);
    for (let i = 0; i < ITEMS.length; i++) {
      const cx = cx0 + i * (cw + gap);
      const cy = pipeY - ch / 2;
      const k = clamp((kOne - i * 0.22) / 0.5);
      ctx.strokeStyle = k > 0.1 ? tone('accent') : tone('track');
      ctx.lineWidth = 3;
      roundRect(ctx, cx, cy, cw, ch, 3); ctx.stroke();
      ctx.font = mono(700, 12);
      ctx.fillStyle = k > 0.1 ? tone('accent') : tone('sub');
      const s = ITEMS[i], sww = ctx.measureText(s).width;
      ctx.fillText(s, cx + (cw - sww) / 2, cy + 25);
      if (k > 0.02) {
        ctx.fillStyle = tone('accent');
        ctx.fillRect(cx + 8, cy + ch - 10, (cw - 16) * ease(k), 4);
      }
    }

    // ── 아래 : 빌드 상태 ───────────────────────────
    if (kBuild > 0.02) {
      ctx.save(); ctx.globalAlpha = clamp(kBuild);
      ctx.font = disp(700, 11); ctx.fillStyle = tone('ink');
      const s = '커밋마다 돌아가는 상태로';
      const sw2 = ctx.measureText(s).width;
      ctx.fillText(s, Math.min(cx0 - 12, w - M - sw2), h - 16);
      ctx.restore();
    }
  },
};
