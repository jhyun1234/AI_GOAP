import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* recallblank — 며칠 뒤에 떠올려 보니 떠오르는 자리가 비어 있었다. 그리고 둘은 그냥 쓰러졌다.

   원문 근거:
   > "판을 지켜본 뒤 며칠 있다가 '지난번에 무슨 일 있었지?' 하고 떠올려서,
   >  장면이 하나라도 떠오르면 통과입니다."        ← 「떠오른 장면」 라벨의 근거
   > "게으름뱅이 둘 다 그냥 죽었어, 별 이야기가 없었다"

   🔴 회차 안 되받기는 **뒤집기**다. 앞 샷(pairgrid)에서 열다섯 칸을 하나도 안 남기고
   채웠던 계단이, 여기서는 위쪽에 작게 남아 있다가 **그냥 흐려져 사라진다.**
   가득 찬 것이 남기는 게 없다는 것이 이 편의 논지다.

   색 규약(SH 에서 세운 것 그대로): **강조색 = 안쪽에 만들어진 것 / 흰색 = 화면에 보이는 것.**
   사라지는 계단이 강조색, 빈 그릇과 쓰러지는 둘이 흰색이다.
   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **세로 좌표로 갈랐다.**
     강조색 계단 바닥 73 · 글로우(blur 7)까지 80 / 「떠오른 장면」 글립 꼭대기 약 **95**.
     **15px** 뜬다. 그 아래로는 강조색이 한 점도 없다.
     (1차본 주석은 글립 꼭대기를 97 로 잡아 「17px」이라고 적었다 — 검수 재계산값이 맞다.)

   ⏱ 쓰러짐은 `since(1)` 0.30~1.20초다 — `delayMs` 와 원점이 같아 자막 길이가 바뀌어도
   효과음 자리를 다시 안 풀어도 된다. 둘째 자막이 「그냥 죽었어,」로 시작하므로
   **말과 그림이 같은 구간에 있다.**

   세로 예산(캔버스 307): 최저 = **쓰러지는 도중** 세로 폭이 가장 커지는 순간이다.
   반너비 8 · 반높이 22 를 θ 만큼 기울이면 바운딩 반높이가 `22·cosθ + 8·sinθ` 이고
   최댓값은 √(22²+8²) = **23.41**(θ ≈ 20°). 그때 fall = 20/90 = 0.222 이라
   중심 y = lerp(230, 244, 0.222) = 233.1 이고 숨 2px 을 다 먹으면 235.1 →
   **바닥 258.5**. 48px 남는다. 다 누우면 252 다.
   가로: 그릇 바깥선 66~286, 누운 표식 98~142 · 210~254. 좌 52 · 우 66 남는다.

   계속 도는 것(30fps 기준):
     · 그릇 점선 흐름 60px/s → **2.0px/프레임** (둘레 612px)
     · 빈 그릇 안을 훑는 흰 막대 125px/s → **4.2px/프레임** (4×30px)
     · 표식 둘의 상시 숨 진폭 2px · 7.4rad/s → **0.49px/프레임** */

const BX0 = 66, BX1 = 286, BY0 = 116, BY1 = 202;
const MARK_X = [120, 232];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const ctext = (txt, x, y, weight, size, color, maxW, alpha) => {
      if (alpha <= 0.01) return;
      let fs = size; ctx.font = disp(weight, fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(weight, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.textAlign = 'center'; ctx.fillStyle = color;
      ctx.fillText(txt, clamp(x, 14 + tw / 2, w - 14 - tw / 2), y);
      ctx.restore();
    };

    const dashRun = (x0, y0, x1, y1, seg, gap, off, alpha) => {
      const P = seg + gap;
      const len = Math.hypot(x1 - x0, y1 - y0);
      const ux = (x1 - x0) / len, uy = (y1 - y0) / len;
      const ph = ((off % P) + P) % P;
      ctx.save();
      ctx.globalAlpha = alpha; ctx.fillStyle = tone('ink');
      for (let s = -P + ph; s < len; s += P) {
        const a = Math.max(0, s), b = Math.min(len, s + seg);
        if (b <= a) continue;
        const ax = x0 + ux * a, ay = y0 + uy * a;
        const bx = x0 + ux * b, by = y0 + uy * b;
        ctx.fillRect(
          Math.min(ax, bx) - (uy ? 1.5 : 0),
          Math.min(ay, by) - (ux ? 1.5 : 0),
          Math.abs(bx - ax) + (uy ? 3 : 0),
          Math.abs(by - ay) + (ux ? 3 : 0));
      }
      ctx.restore();
    };

    const st = ease(cue(spec.boxCue ?? 0, 0.15, 0.30));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    /* ── 위 : 앞 샷에서 다 찼던 계단이 남아 있다가 사라진다 (강조색) ──
       🔴 1차 검수(2026-08-10)가 여기를 짚었다 — 1차본 `(since − 0.20) / 0.60` 은
       **최대 실효 알파 0.334 · 0.6 이상 0초 · ts 0.80 에 소멸**이었다. 이 편의 논지 절반
       (「가득 찼던 것이 아무것도 안 남긴다」)을 지는 요소가 그렇게 옅으면 안 된다.
       `0.45 / 1.20` 으로 밀어 **최대 0.725 @ ts 0.75 · 0.6 이상 약 0.45초(ts 0.52~0.97) ·
       소멸 ts 1.65** 로 올렸다. 발화 개시(0.337) 시점 0.371 에서 시작해 말하는 동안 정점에 든다.
       (실효 알파 = `st × (1 − gone) × ENTER`, `st = ease((ts+0.15)/1.178)` — 실측 자막 길이 기준)
       🔑 세로 좌표 분리는 시간과 무관하므로 「흰색·강조색 같은 픽셀 금지」는 안 바뀐다. */
    const gone = ease(clamp((since(spec.boxCue ?? 0) - 0.45) / 1.20));
    const ga = st * (1 - gone);
    if (ga > 0.01) {
      let k = 0;
      for (let r = 0; r < 5; r++) {
        const n = 5 - r;
        const xs = 176 - (n * 14 - 2) / 2;
        const y = 40 + r * 7;
        for (let c = 0; c < n; c++, k++) {
          ctx.save();
          ctx.globalAlpha = ga * (0.7 + 0.3 * (0.5 + 0.5 * Math.sin(t * 3.2 - k * 0.45)));
          setShadow(ctx, GLOW, 7);
          ctx.fillStyle = tone('accent');
          ctx.fillRect(xs + c * 14, y, 12, 5);
          clearShadow(ctx);
          ctx.restore();
        }
      }
    }

    /* ── 가운데 : 떠오른 장면이 들어와야 할 자리 (흰 점선 그릇 — 끝까지 빈다) ── */
    if (spec.boxLabel) ctext(spec.boxLabel, (BX0 + BX1) / 2, 108, 900, 13, tone('ink'), 200, st);
    const off = -t * 60;
    dashRun(BX0, BY0, BX1, BY0, 12, 10, off, st * 0.95);
    dashRun(BX1, BY0, BX1, BY1, 12, 10, off, st * 0.95);
    dashRun(BX1, BY1, BX0, BY1, 12, 10, off, st * 0.95);
    dashRun(BX0, BY1, BX0, BY0, 12, 10, off, st * 0.95);

    /* 그릇 안을 계속 훑지만 아무것도 안 걸린다 */
    const sweepU = (t / 1.6) % 1;
    const sx = lerp(BX0 + 16, BX1 - 20, sweepU < 0.5 ? sweepU * 2 : 2 - sweepU * 2);
    ctx.save();
    ctx.globalAlpha = st * 0.75;
    ctx.fillStyle = tone('ink');
    ctx.fillRect(sx, BY0 + 28, 4, 30);
    ctx.restore();

    /* ── 아래 : 둘이 그냥 쓰러진다 (흰색) ────────────── */
    const fall = ease(clamp((since(spec.fallCue ?? 1) - 0.30) / 0.90));
    for (let i = 0; i < 2; i++) {
      const br = 2 * (0.5 + 0.5 * Math.sin(t * 7.4 + i * 1.3));
      const cy = lerp(230, 244, fall) + br;
      ctx.save();
      ctx.globalAlpha = st;
      ctx.translate(MARK_X[i], cy);
      ctx.rotate(fall * Math.PI / 2);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, -8, -22, 16, 44, 8); ctx.stroke();
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
