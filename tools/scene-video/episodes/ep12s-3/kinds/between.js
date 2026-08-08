import {
  disp, mono, ease, clamp, lerp, spring,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* between — 판 둘 다 통과인데, 켜지는 것은 그 사이다.

   원문: "코드에는 버그가 한 줄도 없었습니다. 버그는 두 설계의 전제 사이에 있었습니다."
   "재해 시스템도 제대로 동작했고, 파종 잠금도 의도대로 동작했습니다. 각각 따로 보면 둘 다
   정답이었습니다. 그런데 붙여놓으니 서로의 존재 이유를 지워버렸습니다."

   이 회차의 기본 도형(밭 칸)이 여기서 마지막으로 변형된다 — **칸이 두 개로 커져서 두 설계가
   된다.** 앞 세 샷의 칸은 무언가를 담는 그릇이었는데, 이 샷의 칸은 담긴 것이 없고
   **그릇 사이의 틈**이 주인공이다.

   ① 두 판이 서서 각각 흰 `OK` 도장을 받는다. 둘 다 맞다.
   ② 그런데 강조색은 판 안이 아니라 **판과 판 사이 검은 틈**에서 켜진다. 틈이 사선 빗금으로
      차고 그 위에 `BUG` 가 선다. 페이오프가 여기다.
   ③ `BUG` 가 선 직후부터 자막이 끝날 때까지 두 판이 계속 벌어지고(틈 26 → 92px), 그 틈이
      아래로 46px 흘러 나간다 — 빗금 띠가 16 × 140 에서 **82 × 186** 으로 자란다.

   🔴 **2026-08-08 4단 구조 개정 — ③이 물려 있던 예고 자막이 이 샷을 떠났다.**
   개정 전에는 ③이 `teaseCue`(= `cue(1)`, 예고 줄)에 물려 있었다. 그 줄이 아웃트로 샷으로
   가면서 이 샷은 자막 한 줄짜리가 됐고, 그대로 두면 `cue(1)` 이 `Math.min(i, rel.length−1)` 로
   **0번 줄에 눌러앉아**(engine.js:293) 벌어짐과 페이오프가 한꺼번에 터진다.
   🔑 **같은 줄을 두 번, 다른 창으로 불렀다.** `cue` 는 순수 함수라 이게 된다.
       `c0 = cue(0, 0.15, 0.35)` — 좁은 창. 페이오프(`BUG`)를 자막 앞쪽에서 끝낸다.
       `cT = cue(0, 0.15, 0.95)` — 넓은 창. 벌어짐이 자막 끝까지 이어진다.
   실측 `rel` 3,763ms 로 풀면 `BUG` 완성은 자막 시작 **+1.14초**, 벌어짐은 **+1.42 ~ +3.35초**다.
   페이오프가 끝나는 자리를 벌어짐이 바로 이어받으므로 샷 꼬리가 안 빈다.
   ⚠️ 옛 교훈은 그대로 산다 — **마지막 샷의 예고 구간에 「끝까지 자라는」 연출을 두지 마라.**
   카드가 덮는 시각이 상한이고(마지막 **3.0초** — ADR-V25-10 으로 2.6 → 3.0), 거기까지 못 자란
   것은 연출이 아니라 파편이다. 그래서 예고는 이제 아웃트로 샷의 `nextpeek` 이 0.5초 루프로
   진다(카드가 덮기 전에 여러 바퀴 돈다).

   🔴 틈은 처음부터 빗금이 흐르고 있다(흰 계열, 알파 낮음). c0 0.56 에 그것이 **완전히 꺼진
   뒤** 0.58 부터 강조색 빗금이 들어온다. 두 색이 같은 픽셀에 겹치는 프레임이 한 장도 없다 —
   문턱에서 색만 갈아 끼우면 그 한 프레임에 두 색이 섞이고, 팔레트 게이트는 그걸 못 본다.
   🔴 빗금 띠는 판 테두리에서 5px 안쪽에서 시작한다(테두리 stroke 3px 는 경로 기준 ±1.5px).
   🔴 `BUG` 글자는 판보다 **위**(y ≤ 56)에 있어 좁은 틈보다 넓어도 판을 침범하지 않는다.

   🔴 페이오프(BUG)는 c0 = 0.88 에 끝난다. cue span 을 0.35 로 좁혀 잡았기 때문에 그 시각은
   마지막 자막의 앞쪽 1/3 이다 — 아웃트로 카드(마지막 **3.0초**, ADR-V25-10)가 덮기 한참 전이고,
   4단 구조에서는 그 뒤에 아웃트로 샷이 통째로 하나 더 붙어 여유가 더 크다.

   계속 도는 것 = 틈 안 빗금의 사선 흐름(t) · 두 판 아래쪽 점선의 흐름. */

const PY = 66, PH = 140;
/* 틈이 내려가는 끝점. oy 는 그때 0 이므로 이 값이 이 kind 의 최저 y 다(캔버스 307). */
const GAP_END = 252;
/* 벌어짐의 창 — 넓은 `cT` 기준. 0.42 에 시작해 0.94 에 끝나므로 페이오프(`c0` 0.88 =
   자막 시작 +1.14초)가 끝나는 자리를 이어받아 자막 끝까지 큰 움직임이 남는다. */
const TEASE_A = 0.42, TEASE_B = 0.52;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const fit = (txt, weight, start, max, min = 8, f = disp) => {
      let fs = start; ctx.font = f(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = f(weight, fs); }
      return fs;
    };

    /* 같은 줄, 다른 창 — 위 주석 참조. c0 는 페이오프용(좁다) · cT 는 벌어짐용(넓다). */
    const c0 = cue(spec.bugCue ?? 0, 0.15, 0.35);
    const cT = cue(spec.bugCue ?? 0, 0.15, 0.95);

    const cx = w / 2;
    const tease = ease(clamp((cT - TEASE_A) / TEASE_B));
    const gap = lerp(26, 92, tease);
    const pin = ease(clamp(c0 / 0.28));
    const oy = lerp(14, 0, pin);

    const panels = [
      { x0: 14, x1: cx - gap / 2, label: spec.leftLabel, k: ease(clamp((c0 - 0.12) / 0.24)) },
      { x0: cx + gap / 2, x1: w - 14, label: spec.rightLabel, k: ease(clamp((c0 - 0.28) / 0.24)) }
    ];

    /* ── 두 판 ─────────────────────────────────────── */
    for (const p of panels) {
      const pw = p.x1 - p.x0;
      ctx.globalAlpha = clamp(pin * 1.4);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, p.x0, PY + oy, pw, PH, 9); ctx.stroke();

      ctx.textAlign = 'center';
      if (p.label) {
        ctx.font = disp(900, fit(p.label, 900, 14, pw - 18, 8));
        ctx.fillStyle = tone('ink');
        ctx.fillText(p.label, p.x0 + pw / 2, PY + oy + 32);
      }
      if (spec.noBug) {
        ctx.font = mono(700, fit(spec.noBug, 700, 11, pw - 20, 8, mono));
        ctx.fillStyle = tone('sub');
        ctx.fillText(spec.noBug, p.x0 + pw / 2, PY + oy + 56);
      }

      /* OK 도장 */
      if (p.k > 0.01) {
        const sc = spring(p.k);
        ctx.save();
        ctx.globalAlpha = clamp(p.k * 1.6);
        ctx.translate(p.x0 + pw / 2, PY + oy + 96);
        ctx.scale(sc, sc);
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        roundRect(ctx, -25, -16, 50, 32, 5); ctx.stroke();
        ctx.textAlign = 'center';
        ctx.font = disp(900, 16);
        ctx.fillStyle = tone('ink');
        ctx.fillText(spec.okLabel ?? 'OK', 0, 6);
        ctx.restore();
      }

      /* 판 바닥 점선 — 계속 흐른다 */
      ctx.globalAlpha = clamp(pin * 1.4) * 0.5;
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.setLineDash([8, 7]);
      ctx.lineDashOffset = -((t * 24) % 15);
      ctx.beginPath();
      ctx.moveTo(p.x0 + 12, PY + oy + PH - 16);
      ctx.lineTo(p.x1 - 12, PY + oy + PH - 16);
      ctx.stroke();
      ctx.setLineDash([]);
      ctx.globalAlpha = 1;
    }

    /* ── 사이의 틈 ─────────────────────────────────── */
    const gx0 = cx - gap / 2 + 5, gx1 = cx + gap / 2 - 5;
    const gyTop = PY + oy;
    const gyBot = lerp(PY + PH, GAP_END, tease) + oy;

    const preA = 1 - ease(clamp((c0 - 0.44) / 0.12));    // c0 0.56 에 완전히 꺼진다
    const litA = ease(clamp((c0 - 0.58) / 0.24));        // c0 0.58 부터 들어온다

    const stripe = (alpha, color) => {
      if (alpha <= 0.01) return;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.beginPath(); ctx.rect(gx0, gyTop, gx1 - gx0, gyBot - gyTop); ctx.clip();
      ctx.strokeStyle = color; ctx.lineWidth = 3;
      const step = 14, off = (t * 17) % step;
      for (let d = -(gyBot - gyTop) - step; d < (gx1 - gx0) + step; d += step) {
        ctx.beginPath();
        ctx.moveTo(gx0 + d + off, gyBot);
        ctx.lineTo(gx0 + d + off + (gyBot - gyTop), gyTop);
        ctx.stroke();
      }
      ctx.restore();
    };
    stripe(preA * 0.5 * clamp(pin * 1.4), tone('sub'));
    if (litA > 0.01) {
      setShadow(ctx, GLOW, 14);
      stripe(litA, tone('accent'));
      clearShadow(ctx);
    }

    /* ── BUG — 이 편의 페이오프 ────────────────────── */
    const bl = ease(clamp((c0 - 0.62) / 0.26));          // c0 0.88 에 완성
    if (bl > 0.01 && spec.bugLabel) {
      const sc = spring(bl);
      ctx.save();
      ctx.globalAlpha = clamp(bl * 1.5);
      ctx.translate(cx, 46);
      ctx.scale(sc, sc);
      ctx.textAlign = 'center';
      ctx.font = disp(900, fit(spec.bugLabel, 900, 22, w - 60));
      setShadow(ctx, GLOW, 18);
      ctx.fillStyle = tone('accent');
      ctx.fillText(spec.bugLabel, 0, 0);
      clearShadow(ctx);
      ctx.restore();
      ctx.globalAlpha = 1;
    }

    /* 🔴 페이오프 뒤에 따로 그리는 것은 없다. 벌어지는 틈(gap)과 내려가는 바닥(gyBot)이
       그 자리를 진다 — 위 주석 ③ 참조. 바닥을 가로지르던 밑줄은 카드에 먹혀 파편으로
       읽혀서 예전에 걷어냈고, 되살리지 않았다. */

    ctx.textAlign = 'left';
  }
};
