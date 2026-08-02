import {
  disp, ease, clamp, lerp, frac, rnd,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* swell — 기능은 하나씩 늘고, 버그는 그보다 빠르게 부푼다.

   기준선 위에 '기능' 블록이 왼쪽부터 하나씩 놓이고, 각 블록 바로 아래로 '버그' 점이
   매달린다. 블록은 1개씩 늘지만 점은 1·2·4·7·11 로 늘어난다 — 두 증가율의 차이가
   이 편의 출발점(원문 1·3행)이다. 숫자는 화면에 쓰지 않는다. 개수 자체가 도형이다.

   burstCue 에서 점 하나가 강조색으로 '고쳐지고', 그 순간 다른 열의 점 둘이 ×로 터진다
   ("고치면 다른 데가 터진다"). askCue 에서 그날 오전의 질문이 카드로 내려앉는다 —
   나레이션은 이 문장을 읽지 않는다. 읽는 것은 시청자 몫이다.

   회차의 기본 도형 안에서 이 그림의 몫은 '한 칸을 더할 때마다 밑이 더 무거워진다'.
   계속 도는 것 = 25개 버그 점이 각자 다른 위상으로 작은 궤도를 돈다. */

const DRIFT = 2.3;

function fitFont(ctx, text, maxW, make, start, min) {
  let s = start; ctx.font = make(s);
  while (s > min && ctx.measureText(text).width > maxW) { s -= 0.5; ctx.font = make(s); }
  return s;
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const bugs = spec.bugs || [1, 2, 4, 7, 11];
    const n = bugs.length;
    const fixAt = spec.fixAt ?? 7;
    const breakAt = spec.breakAt || [15, 22];

    const gk = ease(cue(spec.growCue ?? 0, 0.15, 0.72));
    const bk = ease(cue(spec.burstCue ?? 1, 0.15, 0.6));
    const ak = ease(cue(spec.askCue ?? 2, 0.15, 0.32));

    ctx.textBaseline = 'alphabetic';

    const baseY = h * 0.36;
    const padL = 34, padR = 16;
    const colW = (w - padL - padR) / n;
    const bw = Math.min(42, colW - 12);

    /* 기준선 — 위는 기능, 아래는 버그 */
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(10, baseY + 5); ctx.lineTo(w - 10, baseY + 5); ctx.stroke();

    ctx.textAlign = 'left'; ctx.font = disp(800, 10); ctx.fillStyle = tone('sub');
    ctx.fillText(spec.featLabel || '기능', 8, baseY - 8);
    ctx.fillText(spec.bugLabel || '버그', 8, baseY + 30);

    /* ── 버그 점 ────────────────────────────────────
       열 안에서 3개씩 한 줄. 줄마다 가운데 정렬해서 열이 아래로 자란다. */
    let idx = 0;
    for (let i = 0; i < n; i++) {
      const cx = padL + colW * (i + 0.5);
      const ci = clamp((gk - i * 0.16) / 0.36);          // 열 등장 진행
      const cnt = bugs[i];
      for (let j = 0; j < cnt; j++, idx++) {
        const vis = clamp(ci * (cnt + 1.2) - j);
        if (vis < 0.02) continue;
        const row = Math.floor(j / 3);
        const inRow = Math.min(3, cnt - row * 3);
        const col = j % 3;
        const ph = rnd(idx * 7 + 3) * Math.PI * 2;
        const ang = frac(t / DRIFT) * Math.PI * 2 + ph;
        const dx = cx + (col - (inRow - 1) / 2) * 14 + Math.cos(ang) * 2.4;
        const dy = baseY + 20 + row * 14 + Math.sin(ang * 1.3) * 2.0;
        const r = 4.2 * (0.6 + 0.4 * vis);

        if (idx === fixAt && bk > 0.02) {
          // 고친 자리 — 강조색으로 채우고 링이 퍼진다
          ctx.fillStyle = tone('accent');
          setShadow(ctx, GLOW, 10, 0);
          ctx.beginPath(); ctx.arc(dx, dy, r + 1.2 * bk, 0, Math.PI * 2); ctx.fill();
          clearShadow(ctx);
          ctx.globalAlpha = clamp(1 - bk) * 0.8;
          ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
          ctx.beginPath(); ctx.arc(dx, dy, lerp(r + 3, 40, bk), 0, Math.PI * 2); ctx.stroke();
          ctx.globalAlpha = 1;
          continue;
        }

        const broke = breakAt.indexOf(idx) >= 0 && bk > 0.35;
        if (broke) {
          // 터진 자리 — 점이 아니라 ×
          const k = clamp((bk - 0.35) / 0.4);
          const s = r + 2.5 * k;
          const jx = Math.cos(frac(t / 0.55) * Math.PI * 2 + ph) * 3.2 * k;
          ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
          ctx.beginPath();
          ctx.moveTo(dx - s + jx, dy - s); ctx.lineTo(dx + s + jx, dy + s);
          ctx.moveTo(dx + s + jx, dy - s); ctx.lineTo(dx - s + jx, dy + s);
          ctx.stroke();
          continue;
        }

        ctx.globalAlpha = clamp(vis * 1.4) * 0.9;
        ctx.fillStyle = tone('ink');
        ctx.beginPath(); ctx.arc(dx, dy, r, 0, Math.PI * 2); ctx.fill();
        ctx.globalAlpha = 1;
      }
    }

    /* ── 기능 블록 ─────────────────────────────────── */
    for (let i = 0; i < n; i++) {
      const ci = clamp((gk - i * 0.16) / 0.36);
      if (ci < 0.02) continue;
      const cx = padL + colW * (i + 0.5);
      const s = Math.min(1, spring(ci));
      const bwS = bw * s, bhS = 20 * s;
      ctx.globalAlpha = clamp(ci * 1.6);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, cx - bwS / 2, baseY - 22 + (20 - bhS) / 2, bwS, bhS, 3);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 그날 오전의 질문 ──────────────────────────── */
    if (ak > 0.01) {
      const q = spec.quote || [];
      const cardW = w - 26, cardH = 74;
      const cx = 13, cy = h - 28 - cardH - 26 * (1 - ease(ak));
      const s = Math.min(1, spring(ak));
      const sw = cardW * s, sh = cardH * s;
      const sx = cx + (cardW - sw) / 2, sy = cy + (cardH - sh) / 2;

      ctx.globalAlpha = clamp(ak * 1.5);
      ctx.fillStyle = '#000';
      roundRect(ctx, sx, sy, sw, sh, 6); ctx.fill();
      setShadow(ctx, GLOW, 14, 0);
      ctx.lineWidth = 4; ctx.strokeStyle = tone('accent');
      roundRect(ctx, sx, sy, sw, sh, 6); ctx.stroke();
      clearShadow(ctx);

      ctx.textAlign = 'center';
      const maxW = cardW - 28;
      const f0 = fitFont(ctx, q[0] || '', maxW, x => disp(700, x), 12.5, 8);
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, f0 * s);
      ctx.fillText(q[0] || '', cx + cardW / 2, cy + 30);
      const f1 = fitFont(ctx, q[1] || '', maxW, x => disp(900, x), 15, 9);
      ctx.fillStyle = tone('accent'); ctx.font = disp(900, f1 * s);
      ctx.fillText(q[1] || '', cx + cardW / 2, cy + 56);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
