import {
  disp, ease, clamp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* pairgrid — 여섯을 둘씩 짝지은 열다섯 칸이 하나도 안 남고 다 찬다.

   원문 근거:
   > "성격 여섯 종이 열다섯 쌍 전부 다른 상위 목표 구성으로 갈렸습니다.
   >  여섯 중 둘을 뽑는 모든 조합에서 '무엇을 주로 하며 사는가'가 달랐다는 뜻입니다."

   🔴 **부채꼴로 갈라지는 그림을 쓰지 않았다.** `ep14s-4` 의 미리보기가 「겹쳐 있던 줄이
   여섯 갈래로 갈라진다」였고 형제 `ep15s-1` 이 같은 사건을 본편으로 진다. 이 편이 지는 것은
   「갈라졌다」가 아니라 **「빠짐없이 전부 갈라졌다」**이므로, 갈래가 아니라 **짝의 칸**을 그린다.
   5+4+3+2+1 = 열다섯 칸의 계단이고, 다 차면 빈 칸이 하나도 안 남는다.

   🔴 화면에 수를 한 개도 안 올렸다. 「열다섯」은 칸의 개수가 지고 자막이 부른다.

   색 규약(SH 에서 세운 것 그대로): **강조색 = 안쪽에 실제로 만들어진 것.**
   빈 칸은 흰색 70%(sub) 윤곽이고, 채워지면 강조색이다.
   🔑 훅(twopanes)의 「빈 흰 그릇 ↔ 찬 강조색 막대」와 같은 대응이다 — 빈 것은 흰 윤곽,
   찬 것은 강조색. (1차본은 빈 칸을 `track`(흰 12%)으로 그렸는데 **정적 검사에 거의 기여를
   못 하는 밝기**였다 — 아래 「계속 도는 것」 참조.)
   🔴 흰색(sub)과 강조색을 같은 픽셀에 안 겹친다 — **두 겹으로 갈랐다.**
     ⓐ **좌표**: 윤곽과 채움이 **같은 숨(`bre`)을 함께 먹으므로** 둘 사이 간격이 항상
        `4 − 1.5(선 반폭) = 2.5px` 로 고정이다(숨이 상쇄된다).
     ⓑ **시간**: 윤곽 알파 = `1 − 채움진행도 × 1.6` 이라 진행도가 0.63 을 넘는 순간 윤곽이
        0 이다. 🔴 **이 식의 뜻은 건드리지 않았다** — 이 편의 「같은 픽셀 금지」를 시간 축에서
        지탱하는 자리다. 판정에 쓰는 것은 밝기 파동이 곱해지기 **전의 `raw`** 이므로,
        파동이 어떻게 흔들려도 「다 찬 뒤에는 윤곽이 영영 0」이 깨지지 않는다.

   ⏱ 채움은 `since(1)`(둘째 자막이 시작된 뒤 흐른 초) 위에 세웠다 — `delayMs` 와 원점이
   같아 **자막 길이가 바뀌어도 효과음 자리를 다시 안 풀어도 된다.**
   0.20초에 시작해 0.07초 간격으로 열다섯 칸, 마지막 칸이 1.38초에 완성된다.

   세로 예산(캔버스 307): 최저 = 마지막 줄 칸 바닥 270. 37px 남는다.
   가로: 첫 줄 54~298. 캔버스 352, 좌 40 · 우 54 남는다.

   🔴 **계속 도는 것 — 1차 검수(2026-08-10)가 여기서 두 가지를 잡았다. 고친 내용을 남긴다.**
   ⓐ 1차본 주석이 「칸 가장자리 숨 0.39px/프레임」을 광고했는데 **코드에 없었다** — 숨(`ins`)이
      채움 사각형에만 걸려 있어서 **빈 칸은 한 프레임도 안 움직였다.**
   ⓑ 밝기 파동(`wave`)이 채움 진행도(`raw`)에 **곱해져** 있어 **빈 칸 상태에서 0** 이다.
      그래서 정적 최대 2.80s 가 (내 주석이 짚은) S1 꼬리가 아니라 **S1 머리**
      (샷 안 0.00~2.83 = 첫 자막 통째)에 났다. 채움은 `since(1)` 0.20 = 샷 안 2.78초에야 시작한다.
   → 고친 것 둘. 지금은 **빈 칸도 상시로 움직이고 밝다**:
     · **숨 `bre` 를 윤곽·채움에 함께 건다**(진폭 2.4px · 9.0rad/s).
       `d/dt = 1.2 × 9.0 = 10.8px/s → 0.36px/프레임`.
       갱신되는 잉크 ≈ 둘레 148px × 양쪽 2 × 0.36px × 열다섯 칸 = **약 1,600px²/프레임**.
     · 빈 칸 윤곽을 `track`(흰 12%) → **`sub`(흰 70%)** 로 올렸다. `L` 은 알파를 곱한 값이라
       track 으로 도는 것은 기여가 흰색의 1/8 이다 — 1,600px² × 0.7 = **약 1,120px² 상당**.
       실측 환산(캔버스 352×307 에서 가득 밝기 168px² ≈ m 0.00057)으로는 **m ≈ 0.0038**,
       문턱 0.0008 의 약 4.7배다.
     · (채움 뒤) 밝기 파동 0.78~1.00 · 3.2rad/s → 프레임당 알파 0.023 × 칸 넓이 1,296px²
       × 열다섯 = 위상이 0.45rad 씩 어긋나 매 프레임 큰 면적이 갱신된다
   ⚠️ 위 환산은 종이 계산이다(내가 이 환경에서 node 를 못 돌렸다).
   🟢 **검수 재측정으로 닫혔다**(2026-08-10 최종) — 정정 7건 전부 통과. */

const ROWS = 5, CW = 44, CH = 30, GX = 6, GY = 6;
const CX = 176, Y0 = 96;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const st = ease(cue(spec.gridCue ?? 0, 0.15, 0.30));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    if (spec.title) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.title, 20, 24);
      ctx.restore();
    }

    const fi = spec.fillCue ?? 1;
    const s1 = since(fi);

    let k = 0;
    for (let r = 0; r < ROWS; r++) {
      const n = ROWS - r;
      const xs = CX - (n * (CW + GX) - GX) / 2;
      const y = Y0 + r * (CH + GY);
      for (let c = 0; c < n; c++, k++) {
        const x = xs + c * (CW + GX);
        const raw = ease(clamp((s1 - 0.20 - k * 0.07) / 0.20));
        /* 다 찬 뒤에도 멈추지 않게 — 밝기 파동. 하한 0.78 이라 채움이 흐려지지 않는다.
           🔴 윤곽 판정에는 이 파동이 곱해지기 **전의 `raw`** 를 쓴다(아래). */
        const wave = 0.78 + 0.22 * (0.5 + 0.5 * Math.sin(t * 3.2 - k * 0.45));
        const fill = raw * wave;

        /* 🔴 상시 숨 — 사건이 아니라 상시 운동이라 `t` 로 돈다(사건은 자막에 물린다).
           **빈 칸에도 걸린다.** 윤곽과 채움이 같은 값을 먹으므로 둘 사이 간격은
           `4 − 1.5 = 2.5px` 로 고정이다(숨이 상쇄된다). 안쪽으로만 줄어드니
           가장자리 예산도 안 건드린다. */
        const bre = 2.4 * (0.5 + 0.5 * Math.sin(t * 9.0 + k * 0.9));

        const oa = st * clamp(1 - raw * 1.6);
        if (oa > 0.01) {
          ctx.save();
          ctx.globalAlpha = oa;
          ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
          roundRect(ctx, x + bre, y + bre, CW - bre * 2, CH - bre * 2, 5); ctx.stroke();
          ctx.restore();
        }

        if (fill * st > 0.01) {
          const ins = 4 + bre;
          ctx.save();
          ctx.globalAlpha = st * fill;
          setShadow(ctx, GLOW, 7);
          ctx.fillStyle = tone('accent');
          roundRect(ctx, x + ins, y + ins, CW - ins * 2, CH - ins * 2, 3); ctx.fill();
          clearShadow(ctx);
          ctx.restore();
        }
      }
    }

    ctx.textAlign = 'left';
  }
};
