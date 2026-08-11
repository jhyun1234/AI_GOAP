import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* sortedout — 같아 보이던 셋이 둘로 갈리고, 한쪽만 뭔가를 갖고 있었다.

   원문 근거:
   > "게으름뱅이는 굶어 죽는게 확인이 됐어. 순둥이와 고집쟁이가 굶어 죽었네
   >  집과 밭이 있었는데 이번 관찰은 신기하네."
   > "예상된 죽음(게으름뱅이)과 이례적인 죽음(준비가 돼 있던 주민)을 갈라서 봤고"
   왼쪽 라벨 「예상된 죽음」은 **원문 낱말 그대로**이고, 오른쪽 라벨 「집도 밭도 있었다」는
   원문이 괄호로 스스로 푼 것('준비가 돼 있던 주민')을 회상 인용의 말로 옮긴 것이다.
   「이례적인」이라는 낱말은 화면에도 자막에도 안 쓴다.

   🔴 회차 안 되받기는 **크기와 색을 함께 뒤집는 것**이다. 훅(hadgot)에서 크게 흰 선으로
   서 있던 집과 밭이, 여기서는 **작은 강조색 표시**로 줄어 표식 옆에 붙는다 —
   「갖춰져 있던 것」이 「내가 알아본 표시」가 되는 순간이 이 편의 사건이다.

   색 규약(이 회차 전체): **흰색 = 마을에 이미 있던 것 / 강조색 = 이번 관찰에서 새로 생긴 것.**
   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **세로 + 가로 두 겹으로 갈랐다.**
     ⓐ세로: 흰 표식 바닥 = 132 + 18 + 1.5 + 숨 1.2 = **152.7** / 강조색 집 아이콘 꼭대기 172 ·
       글로우(blur 6)까지 **166**. **13.3px** 뜬다. 아이콘 바닥 글로우 **206** 과 흰 칸 아래
       획의 안쪽 면 **222.5** 는 **16.5px**. 흰 라벨 글립 꼭대기(baseline 248 · 14px → 약 237)와는 31px.
     ⓑ가로: 집 아이콘 글로우 왼쪽 **200** ↔ 오른쪽 칸 왼 획 안쪽 면 **187.5** = 12.5px /
       밭 아이콘 글로우 오른쪽 **304** ↔ 오른 획 안쪽 면 **316.5** = 12.5px.
   흰 글자끼리(축 B): 「예상된 죽음」(중심 x 100 · maxW 126 → 최대 37~163)과
     「집도 밭도 있었다」(중심 x 252 · maxW 126 → 최대 189~315)가 같은 baseline 248 에 있다.
     **최소 간격 26px** 이고, 문자열이 길어져도 `maxW` 가 폰트를 줄이므로 안 붙는다.
     세로로 벌리는 대신 **가로 정렬 + 폭 상한 둘을 함께** 건 자리다.

   ⏱ 칸은 `cue(0)`, 왼쪽 하나는 `since(0)`, 오른쪽 둘과 아이콘은 `since(1)` 위에 있다.
   `since()` 는 `sfx.delayMs` 와 원점이 같아 **자막 길이가 바뀌어도 소리 자리를 다시 안 푼다.**
   아이콘 켜짐 = `ease(clamp((since(1) − 0.80 − i×0.12) / 0.24))` → 첫 아이콘 0.80~1.04초,
   `tick delayMs 880` 이 그 상승의 33% 지점이다.

   세로 예산(캔버스 307): 최저 = 라벨 글립 바닥 약 252. 55px 남는다.
   가로: 오른쪽 칸 오른 획 바깥 319.5. 좌 32.5 · 우 32.5 남는다.

   계속 도는 것(30fps 기준):
     · 칸 둘의 둘레 2×2×(132+132) = **1,056px** 이 숨을 쉰다.
       `bre = 1.6·(1 + sin(8.4 t + φ))` → 진폭 1.6 · ω 8.4 → **0.448px/프레임** → 약 **473px²**
     · 🔴 두 칸의 위상을 **1.1rad 어긋나게** 줘서 둘이 동시에 정지점을 지나지 않게 했다 —
       첫 자막 앞부분에는 표식이 아직 안 움직이므로 여기가 이 편의 정적 최대 위험 구간이다.
     · 표식 셋의 상시 숨 진폭 0.6px · 7.0rad/s → 0.14px/프레임 */

const LB = [34, 92, 132, 132];
const RB = [186, 92, 132, 132];
const START = [[158, 58], [176, 58], [194, 58]];
const END = [[100, 132], [222, 132], [282, 132]];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const ctext = (txt, x, y, weight, size, color, maxW, alpha) => {
      if (!txt || alpha <= 0.01) return;
      let fs = size; ctx.font = disp(weight, fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(weight, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.textAlign = 'center'; ctx.fillStyle = color;
      ctx.fillText(txt, clamp(x, 14 + tw / 2, w - 14 - tw / 2), y);
      ctx.restore();
    };

    const st = ease(cue(spec.boxCue ?? 0, 0.15, 0.30));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    /* ── 갈라 놓을 두 자리 (흰색) ─────────────────── */
    const boxes = [[LB, 0], [RB, 1.1]];
    for (const [b, ph] of boxes) {
      const bre = 1.6 * (1 + Math.sin(t * 8.4 + ph));
      ctx.save();
      ctx.globalAlpha = st;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, b[0] + bre, b[1] + bre, b[2] - bre * 2, b[3] - bre * 2, 10);
      ctx.stroke();
      ctx.restore();
    }

    /* ── 셋이 갈라진다 (흰색) ─────────────────────
       하나는 「그럴 만했다」 쪽(자막 0), 둘은 「집도 밭도 있었다」 쪽(자막 1). */
    const lone = ease(clamp((since(spec.loneCue ?? 0) - 0.35) / 0.60));
    const pair = ease(clamp((since(spec.pairCue ?? 1) - 0.15) / 0.60));
    for (let i = 0; i < 3; i++) {
      const k = i === 0 ? lone : pair;
      const x = lerp(START[i][0], END[i][0], k);
      const br = 0.6 * (1 + Math.sin(t * 7.0 + i * 1.4));
      const y = lerp(START[i][1], END[i][1], k) + br;
      ctx.save();
      ctx.globalAlpha = st;
      ctx.translate(x, y);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, -8, -18, 16, 36, 8);
      ctx.stroke();
      ctx.restore();
    }

    /* ── 오른쪽 둘 아래에만 켜지는 표시 (강조색) ──
       훅에서 크게 봤던 집·밭이 여기서는 작은 표시로 줄어든다. */
    const ic = i => ease(clamp((since(spec.pairCue ?? 1) - 0.80 - i * 0.12) / 0.24));

    const a0 = st * ic(0);
    if (a0 > 0.01) {                                   // 집
      ctx.save();
      ctx.globalAlpha = a0;
      setShadow(ctx, GLOW, 6);
      ctx.fillStyle = tone('accent');
      ctx.beginPath();
      ctx.moveTo(206, 184); ctx.lineTo(222, 172); ctx.lineTo(238, 184);
      ctx.closePath(); ctx.fill();
      ctx.fillRect(208, 184, 28, 16);
      clearShadow(ctx);
      ctx.restore();
    }

    const a1 = st * ic(1);
    if (a1 > 0.01) {                                   // 밭
      ctx.save();
      ctx.globalAlpha = a1;
      setShadow(ctx, GLOW, 6);
      ctx.fillStyle = tone('accent');
      ctx.fillRect(266, 176, 32, 4);
      ctx.fillRect(266, 185, 32, 4);
      ctx.fillRect(266, 194, 32, 4);
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 라벨 ─────────────────────────────────────── */
    ctext(spec.leftLabel, 100, 248, 800, 14, tone('ink'), 126, st);
    ctext(spec.rightLabel, 252, 248, 800, 14, tone('ink'), 126, st);

    ctx.textAlign = 'left';
  }
};
