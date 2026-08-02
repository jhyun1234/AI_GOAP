import {
  disp, ease, clamp, lerp, frac, rnd,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* rash — 행동 하나를 배울 때마다, 조건 분기가 코드 곳곳에 돋는다.

   위에서 '새 행동 하나' 카드가 내려와 여섯 갈래로 갈라지고, 그 아래 여섯 개의 파일 기둥
   안에 분기 표식이 돋아난다. 표식은 한 기둥에 몰리지 않고 여섯 기둥에 번갈아 찍힌다 —
   원문 13행의 '코드 곳곳에 흩어져 있었다'가 요점이라, 자리가 흩어져 보이는 것이 이 그림의
   전부다. 다 찍힌 뒤에 countCue 에서 세는 눈금이 0부터 55까지 굴러간다("세어 보니").

   🔴 표식이 돋는 것(condCue)과 세는 것(countCue)을 일부러 갈랐다. 나레이션이 개수를
   부르는 자막은 뒤엣것이고, 숫자가 먼저 뜨면 말보다 한 줄 앞선다.

   회차의 기본 도형 안에서 이 그림의 몫은 '하나를 더했더니 여섯 군데가 바뀐다'.
   계속 도는 것 = 여섯 기둥을 각기 다른 속도로 훑고 내려가는 스캔선. */

const SCAN = 3.1;

function fitFont(ctx, text, maxW, make, start, min) {
  let s = start; ctx.font = make(s);
  while (s > min && ctx.measureText(text).width > maxW) { s -= 0.5; ctx.font = make(s); }
  return s;
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const per = spec.per || [9, 12, 7, 11, 6, 10];
    const nf = per.length;
    const total = spec.spots ?? per.reduce((a, b) => a + b, 0);

    const dk = ease(cue(spec.dropCue ?? 0, 0.15, 0.6));
    const ck = ease(cue(spec.condCue ?? 1, 0.15, 0.7));
    const nk = ease(cue(spec.countCue ?? 2, 0.15, 0.55));

    ctx.textBaseline = 'alphabetic';

    /* ── 위에서 내려오는 새 행동 ─────────────────── */
    const cardW = 128, cardH = 30;
    const cardX = w / 2 - cardW / 2;
    const cardY = lerp(-6, 14, ease(dk));
    {
      const s = Math.min(1, spring(dk));
      ctx.globalAlpha = clamp(dk * 1.6);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      roundRect(ctx, cardX + (cardW - cardW * s) / 2, cardY, cardW * s, cardH * s, 4);
      ctx.stroke();
      ctx.textAlign = 'center'; ctx.fillStyle = tone('ink');
      ctx.font = disp(800, 11.5 * s);
      ctx.fillText(spec.newLabel || '새 행동 하나', w / 2, cardY + 20 * s);
      ctx.globalAlpha = 1;
    }

    /* ── 조건 문장 ─────────────────────────────────
       원문 13행이 서술한 문장 그대로. 자막은 '이런 조건'이라고만 부른다. */
    const condY = 66;
    if (ck > 0.02) {
      const txt = spec.cond || '';
      ctx.globalAlpha = clamp(ck * 2);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(16, condY - 11); ctx.lineTo(16, condY + 4); ctx.stroke();
      ctx.textAlign = 'left';
      const f = fitFont(ctx, txt, w - 44, x => disp(700, x), 12.5, 8.5);
      ctx.fillStyle = tone('ink'); ctx.font = disp(700, f);
      ctx.fillText(txt, 24, condY);
      ctx.globalAlpha = 1;
    }

    /* ── 여섯 개의 파일 기둥 ───────────────────────── */
    const gTop = 88, gBot = 236, gH = gBot - gTop;
    const padL = 14, padR = 14;
    const slotW = (w - padL - padR) / nf;
    const colW = Math.min(44, slotW - 10);
    const SLOTS = 16;

    for (let i = 0; i < nf; i++) {
      const cx = padL + slotW * (i + 0.5);
      const x0 = cx - colW / 2;

      // 카드에서 이 기둥으로 갈라지는 선
      const bkk = clamp(dk * 1.3 - i * 0.04);
      if (bkk > 0.02) {
        ctx.globalAlpha = 0.55 * bkk;
        ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(w / 2, cardY + cardH);
        ctx.lineTo(w / 2, cardY + cardH + 8);
        ctx.lineTo(cx, lerp(cardY + cardH + 8, gTop - 6, bkk));
        ctx.stroke();
        ctx.globalAlpha = 1;
      }

      // 기둥 테두리
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, x0, gTop, colW, gH, 3); ctx.stroke();

      // 코드 줄
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      for (let s = 0; s < SLOTS; s++) {
        const y = gTop + 8 + s * ((gH - 16) / (SLOTS - 1));
        const len = 12 + rnd(i * 41 + s) * (colW - 22);
        ctx.beginPath(); ctx.moveTo(x0 + 6, y); ctx.lineTo(x0 + 6 + len, y); ctx.stroke();
      }

      // 분기 표식
      for (let j = 0; j < per[i]; j++) {
        const ord = j * nf + i;
        const vis = clamp(ck * (total + nf * 2) - ord);
        if (vis < 0.02) continue;
        const slot = Math.floor((j + 0.5) * SLOTS / per[i]);
        const y = gTop + 8 + slot * ((gH - 16) / (SLOTS - 1));
        const x = x0 + 6 + rnd(i * 17 + j * 5) * (colW - 22);
        ctx.globalAlpha = clamp(vis * 1.5);
        ctx.fillStyle = tone('ink');
        ctx.fillRect(x, y - 3.5, 9, 7);
        ctx.globalAlpha = 1;
      }

      /* 계속 도는 것 — 기둥마다 다른 속도로 내려가는 스캔선 */
      const u = frac(t / (SCAN + i * 0.37));
      const sy = gTop + 4 + u * (gH - 8);
      ctx.globalAlpha = 0.5;
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(x0 + 3, sy); ctx.lineTo(x0 + colW - 3, sy); ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 세어 보니 ─────────────────────────────────── */
    if (nk > 0.01) {
      const cnt = Math.round(total * ease(nk));
      const y = 276;
      ctx.globalAlpha = clamp(nk * 2);

      ctx.textAlign = 'left';
      ctx.lineWidth = 3; ctx.strokeStyle = tone('sub');
      roundRect(ctx, 14, y - 20, 76, 26, 3); ctx.stroke();
      ctx.fillStyle = tone('sub'); ctx.font = disp(800, 11.5);
      ctx.fillText(spec.fileLabel || '6개 파일', 24, y - 2);

      ctx.textAlign = 'right';
      setShadow(ctx, GLOW, 12, 0);
      ctx.fillStyle = tone('accent'); ctx.font = disp(900, 30);
      ctx.fillText(String(cnt), w - 44, y + 4);
      clearShadow(ctx);
      ctx.textAlign = 'left';
      ctx.fillStyle = tone('sub'); ctx.font = disp(800, 12);
      ctx.fillText(spec.spotUnit || '곳', w - 38, y + 4);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
