import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* premise — 명세가 딛고 선 전제 일곱 칸을 코드가 하나씩 열어 본다.
   그중 둘이 X 로 뒤집히고, 그 두 칸 위에 얹혀 있던 설계 층이 함께 기운다.
   숫자는 M15 글 25·70행의 「일곱 개 중 둘」뿐이다 — 어느 전제였는지는 원문이
   본문에 안 적었으므로 칸에 이름을 붙이지 않는다(번호만).
   연속 모션 = 칸을 여는 코드 커서 대괄호. 확인은 명세마다 매번 다시 도는 절차다. */

const N = 7;
const WRONG = [4, 6];   // 일곱 칸 중 두 칸 (자리는 임의 — 수만 원문이다)

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    const kOpen = ease(cue(1));    // 하나씩 코드로 열어서 확인
    const kFall = ease(cue(2));    // 전제 하나가 틀리면 그 위가 무너진다
    const kX = ease(cue(3));       // 둘이 틀렸다
    const kAfter = ease(cue(4));   // 확인 없이 갔으면

    const gap = 7;
    const cw = (w - M * 2 - gap * (N - 1)) / N;
    const cy = h - 104, ch = 56;

    // ── 위 : 설계 층 ───────────────────────────────
    {
      const dy = 44, dh = 34;
      const tilt = lerp(0, 0.055, ease(kX));      // 두 칸이 빠지며 기운다
      ctx.save();
      ctx.translate(w / 2, dy + dh / 2);
      ctx.rotate(tilt);
      ctx.translate(-w / 2, -(dy + dh / 2));
      ctx.strokeStyle = kX > 0.4 ? tone('ink') : tone('accent');
      ctx.lineWidth = 3;
      roundRect(ctx, M + 18, dy, w - M * 2 - 36, dh, 3); ctx.stroke();
      ctx.font = mono(700, 11);
      ctx.fillStyle = kX > 0.4 ? tone('ink') : tone('accent');
      const s = 'DESIGN';
      ctx.fillText(s, M + 30, dy + 22);
      if (kAfter > 0.05) {
        ctx.save(); ctx.globalAlpha = clamp(kAfter);
        ctx.font = disp(800, 13); ctx.fillStyle = tone('ink');
        const s2 = '이 파일은 한 건짜리였네';
        const sw = ctx.measureText(s2).width;
        ctx.fillText(s2, Math.min(w - M - 30 - sw, M + 110), dy + 23);
        ctx.restore();
      }
      ctx.restore();
    }

    // 설계와 전제를 잇는 기둥
    for (let i = 0; i < N; i++) {
      const cx = M + i * (cw + gap) + cw / 2;
      const broken = WRONG.includes(i) && kX > 0.4;
      ctx.strokeStyle = broken ? tone('track') : tone('sub');
      ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(cx, 78);
      ctx.lineTo(cx, broken ? 92 : cy);
      ctx.stroke();
    }

    // ── 아래 : 전제 일곱 칸 ────────────────────────
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('PREMISE  x7', M, cy - 10);

    for (let i = 0; i < N; i++) {
      const cx = M + i * (cw + gap);
      const kIn = clamp((kOpen - i * 0.1) / 0.42);
      const bad = WRONG.includes(i);
      const kBad = bad ? clamp((kX - (i === WRONG[0] ? 0 : 0.22)) / 0.45) : 0;

      ctx.strokeStyle = kIn > 0.1 ? (kBad > 0.4 ? tone('ink') : tone('accent')) : tone('track');
      ctx.lineWidth = 3;
      roundRect(ctx, cx, cy, cw, ch, 3); ctx.stroke();

      ctx.font = mono(700, 10);
      ctx.fillStyle = tone('sub');
      ctx.fillText(String(i + 1), cx + 6, cy + 15);

      if (kIn > 0.15 && kBad < 0.4) {
        // 확인됨 : 체크
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        const mx = cx + cw / 2, my = cy + ch / 2 + 6;
        ctx.beginPath();
        ctx.moveTo(mx - 10, my - 2); ctx.lineTo(mx - 3, my + 6); ctx.lineTo(mx + 11, my - 11);
        ctx.stroke();
      }
      if (kBad > 0.4) {
        ctx.save(); ctx.globalAlpha = clamp((kBad - 0.4) / 0.5);
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
        const mx = cx + cw / 2, my = cy + ch / 2 + 4;
        ctx.beginPath();
        ctx.moveTo(mx - 10, my - 10); ctx.lineTo(mx + 10, my + 10);
        ctx.moveTo(mx + 10, my - 10); ctx.lineTo(mx - 10, my + 10);
        ctx.stroke();
        ctx.restore();
      }
    }

    // ── 연속 모션 : 칸을 여는 코드 커서 ─────────────
    {
      const PR = 3.5, k = (t % PR) / PR;
      const idx = Math.min(N - 1, Math.floor(k * N));
      const cx = M + idx * (cw + gap);
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      const pad = 5;
      ctx.beginPath();
      ctx.moveTo(cx - pad + 8, cy - pad); ctx.lineTo(cx - pad, cy - pad);
      ctx.lineTo(cx - pad, cy + ch + pad); ctx.lineTo(cx - pad + 8, cy + ch + pad);
      ctx.moveTo(cx + cw + pad - 8, cy - pad); ctx.lineTo(cx + cw + pad, cy - pad);
      ctx.lineTo(cx + cw + pad, cy + ch + pad); ctx.lineTo(cx + cw + pad - 8, cy + ch + pad);
      ctx.stroke();
    }

    // 판정 줄
    if (kFall > 0.05) {
      ctx.save(); ctx.globalAlpha = clamp(kFall);
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      const s = 'ONE WRONG PREMISE  ->  EVERYTHING ABOVE FALLS';
      const sw = ctx.measureText(s).width;
      ctx.fillText(s, Math.min(w - M - sw, M + 120), cy - 10);
      ctx.restore();
    }
  },
};
