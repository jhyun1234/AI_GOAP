import {
  disp, ease, clamp, spring,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* recapempty — 마을이 전멸하면 뜨는 화면. 자리는 큰데 들어 있는 건 값 두 개뿐이고,
   그 아래로는 빈 줄만 계속 흘러간다.

   원문 근거:
   > "마을이 전멸하면 화면을 덮는 회고 화면이 이미 있었습니다. 문제는 거기에 사망 몇 명,
   >  정착 몇 명이라는 숫자만 들어 있었다는 것이었습니다. 회고할 순간은 있었는데,
   >  회고할 내용이 없었습니다."
   > 머리글 문자열은 원문 인용이다 — "헤더는 '마을의 마지막 날 — Day {N}'이고".

   🔴 **숫자를 글자로 안 적었다.** 원문에는 「사망 몇 명·정착 몇 명」이라는 **형식만** 있고
   실제 값이 없다(브리프 §6 「형식 문자열 경고」). 그래서 값 자리는 **강조색 블록**으로만
   그린다 — 「여기 수가 하나 들어 있다」까지만 말하고 그 수가 얼마인지는 말하지 않는다.
   같은 이유로 머리글에서도 「— Day {N}」 꼬리를 뗐다.

   🔴 색 규약(훅에서 세운 것): **흰색 = 그릇(화면·라벨·빈 줄) / 강조색 = 실제로 든 내용.**
   이 샷에서 강조색은 값 블록 둘뿐이다 — 그게 이 화면에 있는 전부라는 뜻이다.
   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **가로·세로 둘 다로 갈랐다.**
     값 블록은 x 210~256(글로우 blur 6 까지 204~262) · y 106~124, 140~158(글로우 100~164).
     같은 줄의 라벨은 x 44 에서 시작해 넉넉히 잡아도 x 110 안에서 끝난다 → **94px** 뜬다.
     위로는 구분선(y 82~85)에서 **15px**, 아래로는 빈 줄 영역(y 178~)에서 **14px** 뜬다.

   ⏱ 한 줄짜리 샷이라 창을 둘로 나눠 썼다(`cue` 는 순수 함수라 같은 인덱스를 두 번 부른다).
     `cue(0, 0.15, 0.26)` = 화면이 뜬다 / `cue(0, 0.15, 0.72)` = 값 둘이 들어앉는다.
     자막이 「…화면엔」에서 「숫자 두 개뿐이었죠」로 넘어가는 자리에 값이 도착한다.

   세로 예산(캔버스 307): 카드는 스프링 최대(1.05)에서 254px 이라 y 30.9~285.1.
   내용은 y 74(머리글 기준선)~262 안에 있다. 아래로 21px 남는다.
   가로: 카드 최대 폭 327.6 → x 12.2~339.8. 가장자리 띠(2px)에서 10px 넘게 뜬다.

   계속 도는 것(30fps 기준): 빈 줄 넷이 위로 22px/s → **0.73px/프레임**(길이 264px 짜리
   줄 넷의 위아래 모서리가 매 프레임 바뀐다). 값 블록 둘은 서로 다른 박자로 밝아졌다
   어두워진다. 🔑 빈 줄은 **끝까지 아무것도 안 실어 나른다** — 그게 이 샷의 논지다. */

const CW = 312, CH = 242, CX = 176, CY = 158;
const IN_X = 44;
const ROW_Y = [106, 140];
const VAL_X = 210, VAL_W = 46, VAL_H = 18;
const AREA_Y0 = 178, AREA_Y1 = 262, LINE_P = 26, CYCLE = 130;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const c0 = ease(cue(spec.cardCue ?? 0, 0.15, 0.26));
    if (c0 <= 0.02) { ctx.textAlign = 'left'; return; }
    const cv = ease(cue(spec.cardCue ?? 0, 0.15, 0.72));

    const s = spring(c0);
    ctx.save();
    ctx.translate(CX, CY); ctx.scale(s, s); ctx.translate(-CX, -CY);

    /* ── 회고 화면 카드 ── */
    ctx.save();
    ctx.globalAlpha = c0;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
    roundRect(ctx, CX - CW / 2, CY - CH / 2, CW, CH, 12); ctx.stroke();
    ctx.restore();

    ctx.textAlign = 'left';
    if (spec.title) {
      ctx.save();
      ctx.globalAlpha = c0;
      ctx.font = disp(900, 15); ctx.fillStyle = tone('ink');
      ctx.fillText(spec.title, IN_X, 74);
      ctx.restore();
    }

    ctx.save();
    ctx.globalAlpha = c0 * 0.32;
    ctx.fillStyle = tone('ink');
    ctx.fillRect(IN_X, 82, 264, 3);
    ctx.restore();

    /* ── 들어 있는 것 둘 : 라벨(흰색) + 값 자리(강조색) ── */
    const labels = [spec.deadLabel, spec.settledLabel];
    for (let i = 0; i < 2; i++) {
      const y = ROW_Y[i];
      if (labels[i]) {
        ctx.save();
        ctx.globalAlpha = c0 * 0.95;
        ctx.font = disp(800, 13); ctx.fillStyle = tone('sub');
        ctx.fillText(labels[i], IN_X, y + 13);
        ctx.restore();
      }
      const vk = ease(clamp((cv - i * 0.16) / 0.5));
      if (vk <= 0.02) continue;
      ctx.save();
      ctx.globalAlpha = vk * (0.80 + 0.20 * (0.5 + 0.5 * Math.sin(t * 3.1 - i * 1.6)));
      setShadow(ctx, GLOW, 6);
      ctx.fillStyle = tone('accent');
      roundRect(ctx, VAL_X, y, VAL_W * vk, VAL_H, 4); ctx.fill();
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 그 아래 : 회고할 내용이 들어와야 할 자리. 빈 줄만 흘러간다 ── */
    for (let i = 0; i < 6; i++) {
      const y = AREA_Y0 + (((i * LINE_P - t * 22) % CYCLE) + CYCLE) % CYCLE;
      if (y > AREA_Y1) continue;
      const edge = ease(clamp((y - AREA_Y0) / 14)) * (1 - ease(clamp((y - (AREA_Y1 - 14)) / 14)));
      const a = c0 * 0.22 * edge;
      if (a <= 0.01) continue;
      ctx.save();
      ctx.globalAlpha = a;
      ctx.fillStyle = tone('ink');
      ctx.fillRect(IN_X, y, 264, 3);
      ctx.restore();
    }

    ctx.restore();
    ctx.textAlign = 'left';
  }
};
