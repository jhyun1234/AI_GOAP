import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* onecount — 일곱 개의 죽음이 전부 한 칸으로 흘러 들어가고, 그 칸에는 「+1」 하나만
   되풀이해서 켜진다.

   원문 근거:
   > "누가 죽었냐고? 사망 카운터 +1. 내가 가진 건 그게 전부인데."
   ← 원문이 옛 코드의 속마음을 **사람 말로 옮겨 준** 대목이라, 「사망 카운터」라는 낱말을
      안 쓰고도 뜻이 선다(ADR-V25-13 ①풀어쓰기). 화면 라벨 「내가 가진 것」도 같은 문장에서
      글자 그대로 왔다. 화면의 「+1」은 원문 문자열 그대로다 — 총합(7 같은 수)은 원문에
      없으므로 **누적값을 숫자로 적지 않는다.**

   🔴 색 규약: 이 샷에는 **강조색이 한 점도 없다.** 이 편에서 강조색은 「이름」인데,
   여기서 남는 것은 이름이 아니라 수뿐이기 때문이다. blankstone 에는 빈 강조색 칸이라도
   있었는데 여기서는 그마저 없다 — 그것이 이 샷이 앞 샷보다 더 나빠진 자리라는 뜻이다.

   ⏱ 일곱 줄기가 한 칸으로 모이는 것은 `since(0)` 위에 세웠다. **`cue` 가 아니라 `since`
   인 것이 효과음의 전제다** — `since` 는 자막이 시작된 뒤 흐른 초라 `delayMs` 와 원점이
   같고, 자막 길이가 바뀌어도 소리 자리를 다시 안 풀어도 된다(`sfx.why` 에 산수를 적었다).
   「+1」은 `since(1)` 로 열리고 그 뒤로는 `t` 로 계속 깜빡인다.

   겹침 감사(축 A 강조↔흰 / 축 B 흰↔흰):
     축 A · 강조색 0개 — 잴 것이 없다.
     축 B · 흰 글자 둘의 최소 간격 = 「+1」 바닥 228(baseline · 내림선 없는 글자) 과
            「내가 가진 것」 글립 꼭대기 266−10 = 256 → **28px**. 그 사이를 상자 바깥
            아래변(y 245.5)이 가른다. 정렬도 둘 다 가운데(x 176)라 좌우로 어긋나 붙지 않는다.
            폭 상한도 걸었다 — 라벨은 200px 를 넘으면 글자 크기가 줄어든다.
            흰 도형끼리 = 동그라미 바깥 바닥 121.7 과 줄기 시작 124 는 **의도한 쌍**(무덤에서
            수로 흘러내리는 것)이고, 서로 다른 것을 가리키는 짝은 없다.

   세로 예산(캔버스 307): 최저 = 라벨 내림선 약 **270**. 37px 남는다.
   가로: 동그라미 왼쪽 44−16.2−1.5 = 26.3 / 오른쪽 308+16.2+1.5 = **325.7**.

   계속 도는 것(30fps 기준): 줄기의 점선이 초당 46px 로 내려간다 → **1.5px/프레임**,
   줄기 일곱의 길이 합 약 530px. 「+1」은 0.62초마다 켜졌다 꺼진다. */

const XS = [44, 88, 132, 176, 220, 264, 308];
const CY = 104, R = 15;
const BOX = { x: 116, y: 196, w: 120, h: 48 };

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const st = ease(cue(spec.flowCue ?? 0, 0.15, 0.25));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

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

    /* ── 위 : 일곱 개의 무덤 ───────────────────────── */
    for (let i = 0; i < XS.length; i++) {
      ctx.save();
      ctx.globalAlpha = st;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.arc(XS[i], CY, R + 1.2 * (0.5 + 0.5 * Math.sin(t * 4.2 + i * 0.7)), 0, Math.PI * 2);
      ctx.stroke();
      ctx.restore();
    }

    /* ── 가운데 : 일곱이 한 칸으로 흘러 내려간다 ───── */
    const s0 = since(spec.flowCue ?? 0);
    ctx.save();
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineCap = 'butt';
    ctx.setLineDash([8, 8]);
    ctx.lineDashOffset = -((t * 46) % 16);
    for (let i = 0; i < XS.length; i++) {
      const k = ease(clamp((s0 - 0.20 - i * 0.075) / 0.26));
      if (k <= 0.01) continue;
      const x0 = XS[i], y0 = CY + R + 5;
      const x1 = 176 + (i - 3) * 11, y1 = BOX.y;
      ctx.globalAlpha = st * 0.85;
      ctx.beginPath();
      ctx.moveTo(x0, y0);
      ctx.lineTo(lerp(x0, x1, k), lerp(y0, y1, k));
      ctx.stroke();
    }
    ctx.setLineDash([]);
    ctx.restore();

    /* ── 아래 : 내가 가진 것 — 칸 하나 ─────────────── */
    const box = ease(clamp((s0 - 0.75) / 0.30));
    if (box > 0.01) {
      ctx.save();
      ctx.globalAlpha = st * box;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, BOX.x, BOX.y, BOX.w, BOX.h, 8);
      ctx.stroke();
      ctx.restore();
    }

    const open = ease(clamp(since(spec.plusCue ?? 1) / 0.30));
    if (open > 0.01) {
      const u = frac(t / 0.62);
      const on = ease(clamp(u / 0.10)) * (1 - ease(clamp((u - 0.55) / 0.14)));
      if (on > 0.01) {
        ctx.save();
        ctx.globalAlpha = st * open * on;
        ctx.textAlign = 'center'; ctx.fillStyle = tone('ink');
        ctx.font = mono(700, 28);
        ctx.fillText(spec.markLabel ?? '+1', 176, 228);
        ctx.restore();
      }
    }

    if (spec.haveLabel) ctext(spec.haveLabel, 176, 266, 900, 13, tone('sub'), 200, st * box);

    ctx.textAlign = 'left';
  }
};
