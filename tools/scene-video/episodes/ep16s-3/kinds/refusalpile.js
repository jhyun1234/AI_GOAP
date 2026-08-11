import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* refusalpile — 사람마다 기록이 하나씩 남기 시작하는데, 가운데 한 사람에게서만
   같은 기록이 아홉 개까지 줄줄이 늘어나더니 위아래 사람 자리를 통째로 먹는다.

   원문 근거:
   > "주민별 생애 기록을 담는 서비스 하나입니다. 담기는 건 이름·성격·직업·등장일·
   >  퇴장일·퇴장 사유입니다."
   > "게으름뱅이 한 명의 '명령 거부(피로)' 아홉 연발이 명부를 통째로 덮었습니다."
   화면 라벨 「명령 거부(피로)」는 원문 문자열 그대로다 — "거부에는 사유가 괄호로
   붙습니다 — '(배고픔)' · '(피로)' · '(부상)'."

   🔴 **아홉은 세어지는 수다.** 자막이 「9번」이라고 말할 때 화면에는 같은 칸이 정확히
   아홉 개 놓인다(23px 간격이라 눈으로 셀 수 있다). 원문에 있는 수만 그린다 —
   다른 두 사람의 기록은 각각 하나씩이고, 이건 「한 사람만 유별나게 많다」는 대비를
   세우는 최소 수량이지 원문이 센 값이 아니다(그래서 글자로 안 적었다).

   🔴 색 규약(훅에서 세운 것 그대로): **흰색 = 사람(이름 칸) / 강조색 = 그 사람의 기록.**
   🔴 훅과 같은 재료를 쓰되 **축을 뒤집었다** — 훅은 세로로 밀어내고, 여기는 가로로
   늘어나다가 세로로 부푼다. 같은 사건을 두 번 같은 그림으로 보여주지 않는다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **가로로 한 번, 세로로 한 번 갈랐다.**
     ① 이름 칸은 x 24~96, 기록 칸은 x 110 부터다. 글로우(blur 6)를 세도 104 → **8px** 뜬다.
     ② 가운데 기록이 부풀면 y 118~194(글로우 112~200)가 되는데, 위 이름 칸 바닥은 102,
        아래 이름 칸 꼭대기는 210 이다 → 위아래 **10px** 씩 뜬다.
        (부푸는 동안 위아래 사람은 이미 흐려지고 있지만, 알파와 무관하게 좌표로 뜬다.)

   ⏱ `cue(0)` = 사람마다 기록이 하나씩 남는다 / `since(1)` = 아홉이 늘어나고 자리를 먹는다.
     🔑 소리(riser)의 `delayMs` 도 같은 원점(`since(1)`)에서 풀었다 — 자막 길이가 바뀌어도
     안 움직인다. 늘어나는 구간은 since(1) 0.39~1.16 이고 부푸는 구간은 1.10~1.75 다.

   세로 예산(캔버스 307): 라벨 기준선 34, 맨 아래 이름 칸 바닥 230. 아래로 77px 남는다.
   가로: 24~314(마지막 기록 칸 글로우까지). 좌 24 · 우 38 남는다.

   계속 도는 것(30fps 기준): 기록 칸이 칸마다 다른 위상으로 밝아졌다 어두워지고
   (진폭 0.28), 이름 칸도 숨을 쉰다. 늘어나는 구간에는 칸 하나가 **3.3px/프레임**으로
   커지고, 부푸는 구간에는 가운데 띠의 높이가 **3.2px/프레임**으로 자란다. */

const ROW_Y = [92, 156, 220];
const CHIP_X = 24, CHIP_W = 72, CHIP_H = 20;
const TRK_X0 = 108, TRK_X1 = 330;
const REC_X0 = 110, REC_P = 23, REC_S = 14;
const PILE_H = 76;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const mk = ease(cue(spec.makeCue ?? 0, 0.15, 0.34));
    if (mk <= 0.02) { ctx.textAlign = 'left'; return; }
    const rec0 = ease(cue(spec.makeCue ?? 0, 0.15, 0.80));

    const s1 = since(spec.pileCue ?? 1);
    const n = spec.pileN ?? 9;
    const grow = k => ease(clamp((s1 - (0.30 + k * 0.09)) / 0.14));   // k = 1…n-1
    const bury = ease(clamp((s1 - 1.10) / 0.65));
    const gone = ease(clamp((s1 - 1.05) / 0.55));                     // 나머지 둘이 흐려진다

    /* ── 라벨 : 무엇이 아홉 번 찍혔는지 (원문 문자열) ── */
    if (spec.refuseLabel) {
      const la = ease(cue(spec.pileCue ?? 1, 0.15, 0.35));
      if (la > 0.02) {
        ctx.save();
        ctx.globalAlpha = la; ctx.textAlign = 'left';
        ctx.font = disp(800, 13); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.refuseLabel, CHIP_X, 34);
        ctx.restore();
      }
    }

    for (let r = 0; r < 3; r++) {
      const cy = ROW_Y[r];
      const mid = r === 1;
      const rowA = mk * (mid ? 1 : 1 - gone);
      if (rowA <= 0.02) continue;

      /* 이름 칸 (흰색) */
      ctx.save();
      ctx.globalAlpha = rowA * (0.88 + 0.12 * (0.5 + 0.5 * Math.sin(t * 2.4 - r * 1.1)));
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, CHIP_X, cy - CHIP_H / 2, CHIP_W, CHIP_H, 6); ctx.stroke();
      ctx.restore();

      /* 기록이 놓이는 자리 */
      ctx.save();
      ctx.globalAlpha = rowA * 0.9;
      ctx.fillStyle = tone('track');
      ctx.fillRect(TRK_X0, cy - 1.5, TRK_X1 - TRK_X0, 3);
      ctx.restore();

      /* 기록 칸 (강조색) — 가운데 줄만 아홉까지 늘어나고, 늘어난 뒤 세로로 부푼다 */
      const count = mid ? n : 1;
      for (let k = 0; k < count; k++) {
        const kk = k === 0 ? rec0 : grow(k);
        if (kk <= 0.02) continue;
        const h = mid ? lerp(REC_S, PILE_H, bury) : REC_S;
        const w = REC_S * (0.4 + 0.6 * kk);
        ctx.save();
        ctx.globalAlpha = rowA * kk * (0.72 + 0.28 * (0.5 + 0.5 * Math.sin(t * 3.6 - k * 0.55)));
        setShadow(ctx, GLOW, 6);
        ctx.fillStyle = tone('accent');
        roundRect(ctx, REC_X0 + k * REC_P + (REC_S - w) / 2, cy - h / 2, w, h, 3); ctx.fill();
        clearShadow(ctx);
        ctx.restore();
      }
    }

    ctx.textAlign = 'left';
  }
};
