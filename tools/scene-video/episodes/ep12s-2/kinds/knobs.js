import {
  disp, ease, clamp,
  fitCanvas, mkCanvas, roundRect, tone, depthGrad, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* knobs — 같은 축을 네 개로 복제해 눌러 내린다. 그중 하나에는 자물쇠가 걸린다.

   원문: "그래서 재조정이 줄줄이 이어졌습니다. 첫째, 밭 재파종을 식량 일수로 잠갔습니다. …
   둘째, 그 문턱을 관측 결과에 맞춰 한 번 더 낮췄습니다. 셋째, 조리식의 포만감을 낮췄습니다.
   넷째, 가을의 자원 재생 배율을 낮춰서 겨울로 넘어가는 저장고 자체를 줄였습니다."

   🔴 이름 넷은 원문의 서수 넷과 **하나씩 대응**하고 순서도 그대로다
   (첫째=REPLANT · 둘째=THRESHOLD · 셋째=SATIETY · 넷째=AUTUMN REGEN). 다섯째 칸은 없다.

   🔴 **눈금도 숫자도 안 그렸다.** 원문이 넷 다 "낮췄습니다"라고만 쓰고 값을 안 준다.
   손잡이가 서로 다른 깊이로 내려가는 것은 「넷을 조였다」만 말하고 「얼마나」는 말하지 않는다.
   깊이가 다른 이유는 그림이 네 번 똑같이 움직이면 하나로 보이기 때문이지 값이 아니다.

   ① tightenCue — 손잡이 넷이 왼쪽부터 차례로 내려가고, 내려간 만큼 트랙 위쪽이 흰색으로 찬다.
      첫 번째 손잡이 왼쪽에 강조색 자물쇠가 붙어 **함께 내려간다**(어느 축이 잠겼는지를
      선을 긋지 않고 말한다 — 선을 그으면 흰 트랙을 가로지른다).
      마지막에 아래로 강조색 자물쇠 상자가 들어오며 잠금 조건의 이름을 지고 온다.

   🔴 흰 것과 강조색을 같은 픽셀에 겹치지 않는다. 자물쇠(강조색)는 손잡이(흰색) 왼쪽 바깥에
   4px 이상 띄워 두고, 상자 안의 빗금은 **글자가 없는 바닥 띠에만** 흐른다.

   계속 도는 것 = 위쪽 자 눈금의 흐름 · 트랙 점선 테두리의 흐름 · 상자 바닥 빗금의 흐름. */

const TRK_TOP = 66, TRK_BOT = 210, TRK_W = 14;
const HND_W = 34, HND_H = 13;
const LABEL_Y = 228;
const BOX_T = 250, BOX_B = 296;

/* 깊이는 값이 아니라 「넷이 서로 다른 손잡이」라는 표시다 */
const DEPTH = [0.70, 0.86, 0.52, 0.64];

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

    const names = spec.knobs ?? [];
    const c0 = cue(spec.tightenCue ?? 0, 0.15, 0.75);
    const appear = ease(clamp(c0 / 0.16));
    if (appear <= 0.004) { ctx.textAlign = 'left'; return; }

    const cxOf = i => w * (0.175 + 0.217 * i);
    const travel = (TRK_BOT - HND_H - 6) - (TRK_TOP + 6);

    /* ── 위쪽 자 — 자막과 무관하게 흐른다 ────────────── */
    ctx.globalAlpha = appear * 0.9;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    const step = 26, sl = (t * 24) % step;
    for (let i = -1; i * step + 16 - sl < w - 12; i++) {
      const x = 16 + i * step - sl;
      if (x < 14 || x > w - 14) continue;
      ctx.beginPath(); ctx.moveTo(x, 44); ctx.lineTo(x, 52); ctx.stroke();
    }
    ctx.beginPath(); ctx.moveTo(14, 52); ctx.lineTo(w - 14, 52); ctx.stroke();
    ctx.globalAlpha = 1;

    /* ── 조임쇠 넷 ─────────────────────────────── */
    let lockY = TRK_TOP + 6;
    for (let i = 0; i < 4; i++) {
      const cx = cxOf(i);
      const k = ease(clamp((c0 - (0.10 + 0.13 * i)) / 0.26));
      const hy = TRK_TOP + 6 + travel * DEPTH[i] * k;
      if (i === 0) lockY = hy;

      /* 트랙 — 점선 테두리가 흐른다 */
      ctx.globalAlpha = appear;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.setLineDash([9, 7]);
      ctx.lineDashOffset = (t * 18) % 16;
      roundRect(ctx, cx - TRK_W / 2, TRK_TOP, TRK_W, TRK_BOT - TRK_TOP, TRK_W / 2);
      ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;

      /* 조인 만큼 위쪽이 찬다 */
      const fillH = hy + HND_H / 2 - (TRK_TOP + 4);
      if (fillH > 1) {
        ctx.fillStyle = tone('ink');
        roundRect(ctx, cx - 4, TRK_TOP + 4, 8, fillH, 4);
        ctx.fill();
      }

      /* 손잡이 */
      setShadow(ctx, 'rgba(255,255,255,0.18)', 10);
      ctx.fillStyle = depthGrad(ctx, cx - HND_W / 2, hy, cx + HND_W / 2, hy + HND_H, 'ink');
      roundRect(ctx, cx - HND_W / 2, hy, HND_W, HND_H, 5);
      ctx.fill();
      clearShadow(ctx);

      if (names[i]) {
        ctx.textAlign = 'center';
        /* 폭 72 = 조임쇠 간격(w×0.217 ≈ 76)보다 좁게 — 이웃 라벨과 안 붙는다 */
        ctx.font = disp(900, fit(names[i], 900, 11, 72, 8));
        ctx.fillStyle = tone('sub');
        ctx.fillText(names[i], cx, LABEL_Y);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 자물쇠 — 첫 손잡이 왼쪽 바깥에서 함께 내려간다 ── */
    const lockA = ease(clamp((c0 - 0.40) / 0.22));
    if (lockA > 0.02) {
      const lx = cxOf(0) - HND_W / 2 - 22, ly = lockY + HND_H / 2;
      ctx.globalAlpha = lockA * appear;
      setShadow(ctx, GLOW, 12);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(lx, ly - 4, 5.5, Math.PI, 0); ctx.stroke();
      roundRect(ctx, lx - 7.5, ly - 4, 15, 12, 3); ctx.stroke();
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    /* ── 잠금 상자 ─────────────────────────────── */
    const boxA = ease(clamp((c0 - 0.58) / 0.26));
    if (boxA > 0.02 && spec.lockLabel) {
      const bx = 18, bw = w - 36, bh = BOX_B - BOX_T;
      ctx.globalAlpha = boxA;
      setShadow(ctx, GLOW, 14);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, bx, BOX_T, bw, bh, 10); ctx.stroke();
      clearShadow(ctx);

      /* 자물쇠 아이콘 */
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(44, 266, 6, Math.PI, 0); ctx.stroke();
      roundRect(ctx, 36, 266, 16, 14, 3); ctx.stroke();

      /* 글자 — 빗금이 지나가지 않는 자리 */
      ctx.textAlign = 'left';
      ctx.font = disp(900, fit(spec.lockLabel, 900, 13, bx + bw - 14 - 66, 8));
      ctx.fillStyle = tone('accent');
      ctx.fillText(spec.lockLabel, 66, 277);

      /* 바닥 띠 — 글자가 없는 곳에서만 빗금이 흐른다 */
      ctx.save();
      roundRect(ctx, bx + 8, 286, bw - 16, 7, 3); ctx.clip();
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      const hs = 14, off = (t * 26) % hs;
      for (let x = bx - 12; x < bx + bw + 12; x += hs) {
        ctx.beginPath();
        ctx.moveTo(x + off, 294); ctx.lineTo(x + off + 8, 285);
        ctx.stroke();
      }
      ctx.restore();
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
