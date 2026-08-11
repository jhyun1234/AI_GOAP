import {
  disp, ease, clamp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* nextpeek — 아웃트로(4단 고정 구조 ④). 다음 편(`ep16s-5`) 사건의 0.5초 테스트 장면.

   🔴 재료는 **같은 글의 마지막 절**에서 뽑았다(다음 편은 이 글의 다섯째 사건이다):
   > "작업이 끝나고, 저는 다시 한 판을 켜서 방치했습니다. 그리고 똑같이 물었습니다.
   >  무슨 일이 있었지?"
   > "앞의 \"게으름뱅이 둘 다 그냥 죽었어\"와 나란히 놓고 보면 세 가지가 달라졌습니다."
   ⚠️ 원문 맨 끝의 「다음 개발일지에서는 …」 문단은 **블로그의 다음 글** 예고라 근거로
   쓰지 않았다(ep02s·ep11s·ep12s 가 그 형태로 세 번 틀렸다).

   🔴 **글자도 숫자도 한 자 안 쓴다.** 다음 편의 두 회상은 원문에 실제 문장이 있지만
   그건 `ep16s-5` 소유이고(성격 세 종류 · 달라진 것 세 가지도 마찬가지),
   미리 올리면 형제 편의 재료를 먼저 태우는 것이 된다. 그래서 **형태로만** 그린다 —
   같은 판을 두 번 봤는데 **나온 말의 길이가 다르다.**
   (선례: `ep15s-5` 의 nextpeek 이 같은 이유로 이름과 날짜를 글자로 안 적었다.)

   0.5초 루프 : 위쪽 네모 안에서 표식 셋이 계속 오간다(판이 돌아간다) → 아래 같은 자리에
   **아주 짧은 흰 조각 하나**가 켜졌다 꺼지고 → 그 자리에 **화면을 가로지르는 긴 강조색 줄
   둘**이 차례로 켜진다 → 꺼지고 되감긴다.

   🔴 본문의 도형(양쪽에서 지워지는 두 칸 · 떨어져 나온 한 장)을 여기로 안 끌고 왔다 —
   다음 편의 사건은 「지워진다/남는다」가 아니라 「같은 걸 두 번 봤는데 말이 달라졌다」다.

   🔴 흰색과 강조색이 **같은 자리를 쓴다 — 시간으로 갈랐다.**
     흰 짧은 조각(y 226~236)은 루프 `u 0.42` 에 알파가 0 이 되고, 강조 줄(glow 212~258)은
     `u 0.52` 부터 뜬다. **한 프레임도 함께 있지 않다.** 같은 자리에 두는 것이 설계다 —
     자리가 같아야 「길이가 달라졌다」가 읽힌다.
     좌표로 갈린 축 A: 판 테두리 바닥 177.5 ↔ 강조 줄 glow 위끝 212 → **34.5px**.
   🔴 흰↔흰: 화면의 흰 글자는 「다음 편」(x 20~62 · y 16~29) 하나뿐이고, 판 테두리(y 108~176)
     와 세로 **79px** 떨어져 있다.

   세로 예산(캔버스 307): 최저 = 아래 강조 줄 glow 바닥 258. **49px** 남는다.
   가로(캔버스 352): 강조 줄 glow 88~264. 좌 88 · 우 88 남는다.

   ⏱ 등장은 예고 자막 `cue(0)` 에, 루프는 `t` 에 물렸다.
   🔴 소개 자막(cue 1)에는 아무것도 안 걸었다 — 마지막 3.0초는 아웃트로 카드가 `.vis` 를
      픽셀 동일 좌표로 덮으므로 그 구간에 그린 것은 한 프레임도 안 보인다.
      🔑 대신 **0.5초 루프와 표식 왕복이 `t` 위에 있어** 카드가 뜨기 전 구간(예고 음성이
      나가는 동안)에 정적이 안 생긴다. `ep05s` 가 여기서 정적 3.6 → 6.6초로 뛴 자리다.

   계속 도는 것(30fps 기준): 0.5초 루프 + 표식 셋의 왕복 진폭 14px · 2.2rad/s →
      **1.0px/프레임** × 셋. */

const PLATE = { x0: 88, y0: 108, x1: 264, y1: 176 };
const MARK_X = [116, 176, 236];
const MARK_CY = 142, MARK_HW = 7, MARK_HH = 18;
const SHORT = { x0: 96, x1: 140, y: 226, h: 10 };
const LONG = [
  { x0: 96, x1: 256, y: 220, h: 10 },
  { x0: 96, x1: 226, y: 240, h: 10 }
];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    if (spec.label) {
      ctx.save();
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.label, 20, 26);
      ctx.restore();
    }

    const pk = ease(cue(spec.peekCue ?? 0, 0.15, 0.30));
    if (pk <= 0.02) { ctx.textAlign = 'left'; return; }

    /* ── 판 (흰 네모 + 계속 오가는 표식 셋) ── */
    ctx.save();
    ctx.globalAlpha = pk * 0.85;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.strokeRect(PLATE.x0, PLATE.y0, PLATE.x1 - PLATE.x0, PLATE.y1 - PLATE.y0);
    ctx.restore();

    for (let i = 0; i < MARK_X.length; i++) {
      const dx = 14 * Math.sin(t * 2.2 + i * 1.9);
      ctx.save();
      ctx.globalAlpha = pk;
      ctx.translate(MARK_X[i] + dx, MARK_CY);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, -MARK_HW, -MARK_HH, MARK_HW * 2, MARK_HH * 2, 6);
      ctx.stroke();
      ctx.restore();
    }

    const u = frac(t / (spec.loopSec ?? 0.5));

    /* ── 첫 번째 대답 : 아주 짧은 흰 조각 하나 ── */
    const wa = pk * ease(clamp((u - 0.08) / 0.06)) * (1 - ease(clamp((u - 0.36) / 0.06)));
    if (wa > 0.01) {
      ctx.save();
      ctx.globalAlpha = wa;
      ctx.fillStyle = tone('ink');
      ctx.fillRect(SHORT.x0, SHORT.y, SHORT.x1 - SHORT.x0, SHORT.h);
      ctx.restore();
    }

    /* ── 두 번째 대답 : 같은 자리에 훨씬 긴 강조색 줄 둘 ── */
    for (let i = 0; i < LONG.length; i++) {
      const L = LONG[i];
      const on = ease(clamp((u - (0.52 + i * 0.08)) / 0.07))
               * (1 - ease(clamp((u - 0.94) / 0.05)));
      if (on * pk <= 0.01) continue;
      ctx.save();
      ctx.globalAlpha = pk * on;
      setShadow(ctx, GLOW, 8);
      ctx.fillStyle = tone('accent');
      ctx.fillRect(L.x0, L.y, L.x1 - L.x0, L.h);
      clearShadow(ctx);
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
