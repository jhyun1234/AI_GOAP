import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* rebound — 이 회차의 척추.

   이 글은 "닿지 못하는" 이야기다. 그래서 이 편의 기본 도형은 **뻗었다가 되돌아오는 획**이고,
   그 획을 가장 벌거벗은 형태로 그리는 것이 이 그림이다.
   기지에서 세 개의 획이 밖으로 뻗는다. 매번 같은 자리에서 튕겨 돌아온다. 그 자리에 자국만 쌓인다.

   같은 그림을 두 번 쓴다. 그게 이 편의 구조다.
     mode:"stall"   (S1)  벽이 서 있고 셋 다 튕겨 돌아온다.
     mode:"release" (S9)  같은 세 레인에서 벽이 열리고, 획이 끊기지 않고 밖까지 흘러 나간다.
   시청자는 60초쯤 뒤 같은 화면으로 돌아와 **그 벽이 없어진 것**을 본다.
   회차 안의 되받기는 돌려막기가 아니다 — 두 모드가 서로 다른 말을 한다.

   🔴 왕복 횟수에 숫자를 안 찍는다. 원문은 "반복했다"고만 하고 횟수를 준 적이 없다.
      쌓인다는 사실만 보이면 된다.

   움직임은 t 로 계속 돈다(자막과 무관하게 계속 오간다). cue 는 벽을 여는 데만 쓴다 —
   샷이 한 번도 멎지 않는 이유이자, 0초부터 이미 고장이 벌어져 있는 이유. */

const LEAD = 1.9;      // 🔴 콜드 오픈. t=0 에 이미 한 획이 벽에 닿아 되돌아오는 중이다

/** 한 왕복의 위상 → 밖으로 나간 정도 0~1. 벽에서 잠깐 멎고, 기지에서도 잠깐 멎는다. */
function trip(ph) {
  if (ph < 0.40) return ease(ph / 0.40);
  if (ph < 0.52) return 1;
  if (ph < 0.90) return 1 - ease((ph - 0.52) / 0.38);
  return 0;
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const release = spec.mode === 'release';
    const lanes = spec.lanes || [];
    const n = Math.max(1, lanes.length);

    /* 🔴 머리·발 자리를 px 로 떼어 놓고 남은 것을 레인 수로 나눈다.
       '남은 영역의 몇 할'로 잡으면 레인 수가 바뀌는 순간 아래가 잘린다. */
    const HEAD = 34, FOOT = 30, GAP = 10;
    const laneH = (h - HEAD - FOOT - GAP * (n - 1)) / n;

    const baseX = 12, baseW = 38;
    const startX = baseX + baseW + 10;
    const outX = w - 26;
    const wallX = Math.round(lerp(startX, outX, 0.56));
    const stopX = wallX - 13;

    const openK = release ? ease(cue(spec.openCue ?? 1, 0.25, 0.5)) : 0;

    /* ── 기지 ─────────────────────────────────────── */
    ctx.lineWidth = 3;
    ctx.strokeStyle = tone('ink');
    roundRect(ctx, baseX, HEAD - 6, baseW, h - HEAD - FOOT + 12, 6);
    ctx.stroke();
    ctx.textAlign = 'center'; ctx.textBaseline = 'alphabetic';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
    ctx.fillText(spec.homeLabel || '기지', baseX + baseW / 2, h - FOOT + 16);

    if (spec.actorLabel) {
      ctx.textAlign = 'left';
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
      ctx.fillText(spec.actorLabel, baseX, HEAD - 16);
    }

    /* ── 벽 ───────────────────────────────────────
       되돌리는 자리. release 에서는 위아래로 물러나며 가운데가 열린다. */
    const wallTop = HEAD - 8, wallBot = h - FOOT + 8, wallMid = (wallTop + wallBot) / 2;
    ctx.lineWidth = 3; ctx.lineCap = 'butt';
    ctx.strokeStyle = openK > 0.7 ? tone('track') : tone('ink');
    ctx.beginPath();
    ctx.moveTo(wallX, wallTop);
    ctx.lineTo(wallX, lerp(wallMid, wallTop, ease(openK)));
    ctx.moveTo(wallX, lerp(wallMid, wallBot, ease(openK)));
    ctx.lineTo(wallX, wallBot);
    ctx.stroke();

    if (spec.wallLabel && openK < 0.98) {
      ctx.globalAlpha = 1 - openK;
      ctx.textAlign = 'left';
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
      ctx.fillText(spec.wallLabel, wallX + 7, HEAD - 16);
      ctx.globalAlpha = 1;
    }

    /* ── 밖 ───────────────────────────────────────
       release 에서만 켜지는 도착지. 획이 실제로 여기까지 온다. */
    if (release) {
      ctx.globalAlpha = clamp(0.25 + 0.75 * openK);
      ctx.textAlign = 'right';
      ctx.fillStyle = openK > 0.4 ? tone('accent') : tone('sub');
      ctx.font = disp(800, 11);
      ctx.fillText(spec.outLabel || '탐험', outX + 4, HEAD - 16);
      ctx.globalAlpha = 1;
    }

    /* ── 레인 ─────────────────────────────────────── */
    for (let i = 0; i < n; i++) {
      const L = lanes[i] || {};
      const cy = Math.round(HEAD + laneH / 2 + i * (laneH + GAP));
      const P = L.period || 3.4;
      const phase = L.phase || 0;

      // 빈 레일
      ctx.lineCap = 'round'; ctx.lineWidth = 3;
      ctx.strokeStyle = tone('track');
      ctx.beginPath(); ctx.moveTo(startX, cy); ctx.lineTo(outX, cy); ctx.stroke();

      // 멎는 모델 — 나갔다 되돌아온다
      const ph = frac((t + LEAD) / P + phase);
      const xA = lerp(startX, stopX, trip(ph));
      // 나가는 모델 — 되돌아오지 않는다
      const ph2 = frac((t + LEAD) / P + phase + 0.13);
      const xB = lerp(startX, outX - 12, ph2);
      const aB = clamp(ph2 / 0.10) * clamp((1 - ph2) / 0.10) * openK;

      // 지나온 자국
      ctx.strokeStyle = 'rgba(255,255,255,0.5)';
      if (openK < 0.98) {
        ctx.globalAlpha = 1 - openK;
        ctx.beginPath(); ctx.moveTo(startX, cy); ctx.lineTo(xA, cy); ctx.stroke();
        ctx.globalAlpha = 1;
      }
      if (openK > 0.02) {
        ctx.globalAlpha = openK;
        ctx.beginPath(); ctx.moveTo(startX, cy); ctx.lineTo(Math.max(xA, xB), cy); ctx.stroke();
        ctx.globalAlpha = 1;
      }

      /* 되돌아온 자국 — 숫자 없이 눈금만. 시간이 흐르면 늘어난다. */
      if (openK < 0.98) {
        const trips = Math.min(Math.floor((t + LEAD) / P + phase), 6);
        ctx.globalAlpha = 1 - openK;
        ctx.fillStyle = tone('ink');
        for (let j = 0; j < trips; j++) ctx.fillRect(stopX - 6 - j * 7, cy + 9, 3, 9);
        ctx.globalAlpha = 1;
      }

      // 벽에 닿는 순간 — 그 자리가 잠깐 밝아진다
      const hit = (ph > 0.38 && ph < 0.56) ? 1 : 0;
      if (hit && openK < 0.9) {
        ctx.globalAlpha = (1 - openK) * 0.9;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineCap = 'butt';
        ctx.beginPath();
        ctx.moveTo(wallX, cy - 12); ctx.lineTo(wallX, cy + 12);
        ctx.stroke();
        ctx.globalAlpha = 1;
      }

      // 표식 — 멎는 쪽(흰색)과 나가는 쪽(초록)
      if (openK < 0.98) {
        ctx.globalAlpha = 1 - openK;
        ctx.fillStyle = tone('ink');
        ctx.beginPath(); ctx.arc(xA, cy, 8, 0, Math.PI * 2); ctx.fill();
        ctx.globalAlpha = 1;
      }
      if (aB > 0.01) {
        ctx.globalAlpha = clamp(aB);
        setShadow(ctx, GLOW, 12, 0);
        ctx.fillStyle = tone('accent');
        ctx.beginPath(); ctx.arc(xB, cy, 8, 0, Math.PI * 2); ctx.fill();
        clearShadow(ctx);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left'; ctx.textBaseline = 'alphabetic'; ctx.lineCap = 'butt';
  }
};
