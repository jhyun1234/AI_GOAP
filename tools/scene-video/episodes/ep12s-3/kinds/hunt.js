import {
  disp, mono, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* hunt — 다 맞는데 답이 없다. 그래서 한 겹 아래로 내려간다.

   원문: "처음엔 당연히 재해 코드를 의심했습니다. 대상을 수집하는 부분이 틀렸나, 저항 판정이
   잘못 걸렸나, 소실 개수 계산이 0으로 내려가나. 그런데 아무리 봐도 계산은 맞았습니다.
   그래서 한 겹 아래로 내려갔더니, 진짜 원인은 재해 쪽에 있지도 않았습니다."
   "바로 앞 단계에서 제가 직접 넣은 재파종 잠금 때문이었습니다."

   이 샷은 **위층에서 아래층으로 내려가는 세로 이동**이 전부다. 다른 샷과 달리 밭 칸이
   주인공이 아니라 조연으로 맨 아래에 작게 깔린다 — 원인이 밭 쪽이 아니라 그 위에 걸린
   규칙에 있었다는 말을 배치로 한다.

   ① 위 패널(DISASTER CODE)의 세 항목에 흰 체크가 하나씩 그어진다. 셋 다 통과다.
      🔴 체크는 흰색이다. 여기서 강조색을 쓰면 "이게 답이다"가 되는데, 이 세 줄은
      **답이 아니라서** 이야기가 되는 자리다.
   ② 셋이 다 채워진 뒤에야 패널 밖으로 강조색 화살표가 아래를 가리킨다.
   ③ 아래 패널이 밑에서 밀려 올라와 강조색으로 켜지고, 그 안의 밭 칸들이 **빈 채로**
      그려진다. 그러고 나서야 그 판의 이름(REPLANT LOCK)과 규칙 한 줄이 뒤늦게 켜진다.

   🔴 **2026-08-08 4단 구조 개정 — 자막 두 줄이 한 줄로 합쳐져 `cue` 를 하나로 접었다.**
   개정 전에는 `checkCue`(0) 와 `lockCue`(1) 둘이었는데, 지금 이 샷의 자막은 한 줄
   「원인은 재해가 아니라, 밭을 비운 재파종 잠금이었어요.」뿐이다.
   ⚠️ `spec.lockCue` 를 지우지 않고 두면 `cue(1)` 이 `Math.min(i, rel.length−1)` 로
   **0번 줄에 눌러앉아**(engine.js:293) 위층 체크와 아래층 패널이 한꺼번에 터진다.
   그래서 하나의 `c0` 를 어절 순서대로 쪼개 놓았다
   (🟢 실측 `rel` **4,398ms** = dur 4,248 + pause 150 · 창 W = 0.15 + 4.398 × 0.85 = **3.888s**.
    초안 산정 4.26 보다 138ms 길어 각 문턱이 가리키는 어절이 약 3% 뒤로 밀렸다 — 아래 대응은
    그 폭 안이라 그대로 둔다):
       c0 0.34  「아니라」   — 체크 셋 완성 (재해 코드는 답이 아니다)
       c0 0.44  「밭을」     — 화살표가 한 겹 아래를 가리킨다
       c0 0.58  「비운」     — 아래 패널이 다 올라온다
       c0 0.68  「비운」 끝  — 빈 밭 칸이 다 그려진다
       c0 0.86  「재파종/잠금」 — 판의 이름과 규칙이 켜진다
   🔑 이름을 맨 뒤로 미룬 이유: 나레이션이 「재파종 잠금」을 부르는 순간에 그 글자가 켜져야
   한다(계약 2). 패널만 먼저 올리고 이름을 나중에 주면 어순과 그림이 맞는다.

   🔴 **세로 예산(캔버스 높이 307 CSS px)을 층별로 못 박아 두었다. 1차본이 여기서 반려됐다.**
   아래 패널을 `oy` 만큼 밑에서 밀어 올리는 연출이었는데, **쉬는 자리(198+94=292)에 슬라이드
   오프셋 22 를 더하면 바닥이 314** 가 되어 캔버스(307) 밖으로 나갔다. 특히 패널 바닥의
   빗금 띠가 통째로 캔버스 마지막 행에 얹혀 **가장자리 검사가 870px**(= 띠 폭 288 × dpr 3.02)을
   찍었다. 잘라서 맞추지 않고 **층을 다시 재서** 넣었다:

       상단 여백  0 ~  24
       위 패널   24 ~ 150   (H 126 · 헤더 46 · 행 76/104/132 · 체크 상자 116~136)
       화살표   156 ~ 174
       아래 패널 180 ~ 280   (H 100 · 헤더 206 · 규칙 228 · 칸 236~256 · 빗금 260~269)
       슬라이드  oy 14 → 0   ⇒ **최악의 순간에도 바닥 294 · 빗금 283** (캔버스 307)

   🔑 규칙 하나로 정리하면: **움직이는 요소는 「쉬는 자리」가 아니라 「가장 밖으로 나가는
   순간」으로 여백을 잰다.** 정지 좌표만 검산하면 이 사고가 그대로 다시 난다.

   🔴 위 패널은 아래가 켜질 때 알파 0.38 로 내려앉는다. 흰색을 흐리게 한 것이므로 회색이고
   (팔레트 게이트는 채도 0.14 이하를 회색으로 통과시킨다) 강조색과 픽셀을 나눠 갖지 않는다.

   계속 도는 것 = 위 패널 점선 테두리의 흐름 · 상하로 오가는 스캔선 · 항목별 점선 리더 ·
   아래 패널 바닥의 사선 빗금. */

const P1_Y = 24, P1_H = 126;
const ROWS_Y = [76, 104, 132];
const ARROW_Y0 = 156, ARROW_Y1 = 174;
const P2_Y = 180, P2_H = 100, P2_SLIDE = 14;

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

    const c0 = cue(spec.checkCue ?? 0, 0.15, 0.85);
    const dim = lerp(1, 0.38, ease(clamp((c0 - 0.38) / 0.16)));

    /* ── 위층 : 재해 코드 ──────────────────────────── */
    ctx.globalAlpha = dim * 0.85;
    ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
    ctx.setLineDash([14, 9]);
    ctx.lineDashOffset = -((t * 30) % 23);
    roundRect(ctx, 16, P1_Y, w - 32, P1_H, 9); ctx.stroke();
    ctx.setLineDash([]);

    ctx.textAlign = 'left';
    if (spec.codeLabel) {
      ctx.globalAlpha = dim;
      ctx.font = disp(900, fit(spec.codeLabel, 900, 13, 190));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.codeLabel, 32, P1_Y + 22);
    }

    /* 스캔선 — 자막과 무관하게 계속 훑는다 */
    const sy = P1_Y + 14 + (Math.sin(t * 1.0) * 0.5 + 0.5) * (P1_H - 28);
    ctx.globalAlpha = dim * 0.5;
    ctx.strokeStyle = tone('sub'); ctx.lineWidth = 4;
    ctx.beginPath(); ctx.moveTo(28, sy); ctx.lineTo(w - 28, sy); ctx.stroke();

    const rows = spec.rows ?? [];
    rows.forEach((label, i) => {
      const y = ROWS_Y[i] ?? (ROWS_Y[0] + i * 28);
      ctx.globalAlpha = dim;
      ctx.textAlign = 'left';
      ctx.font = mono(700, fit(label, 700, 14, 160, 8, mono));
      ctx.fillStyle = tone('ink');
      ctx.fillText(label, 34, y);
      const lw = ctx.measureText(label).width;

      ctx.globalAlpha = dim * 0.6;
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.setLineDash([4, 7]);
      ctx.lineDashOffset = -((t * 18) % 11);
      ctx.beginPath();
      ctx.moveTo(34 + lw + 12, y - 5); ctx.lineTo(w - 72, y - 5); ctx.stroke();
      ctx.setLineDash([]);

      /* 체크 — 두 획이 순서대로 그어진다. 셋째가 c0 0.34(「아니라」)에 완성된다. */
      const k = ease(clamp((c0 - 0.02 - i * 0.08) / 0.16));
      if (k > 0.01) {
        const bx = w - 62, by = y - 16;
        const p1 = [bx + 3, by + 10], p2 = [bx + 8, by + 16], p3 = [bx + 19, by + 2];
        const k1 = clamp(k / 0.4), k2 = clamp((k - 0.4) / 0.6);
        ctx.globalAlpha = dim;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3.5; ctx.lineCap = 'round';
        ctx.beginPath();
        ctx.moveTo(p1[0], p1[1]);
        ctx.lineTo(lerp(p1[0], p2[0], k1), lerp(p1[1], p2[1], k1));
        if (k2 > 0) ctx.lineTo(lerp(p2[0], p3[0], k2), lerp(p2[1], p3[1], k2));
        ctx.stroke();
        ctx.lineCap = 'butt';
      }
    });
    ctx.globalAlpha = 1;

    /* ── 한 겹 아래로 ──────────────────────────────── */
    const arw = ease(clamp((c0 - 0.32) / 0.12));
    if (arw > 0.01) {
      const cx = w / 2;
      ctx.globalAlpha = arw;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(cx, ARROW_Y0); ctx.lineTo(cx, ARROW_Y1); ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(cx - 7, ARROW_Y1 - 8); ctx.lineTo(cx, ARROW_Y1); ctx.lineTo(cx + 7, ARROW_Y1 - 8);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 아래층 : 재파종 잠금 ──────────────────────────
       🔴 `top` 의 최댓값은 P2_Y + P2_SLIDE = 194 이고, 이 패널의 가장 아래 요소는
       빗금 띠 바닥(top + 89 = 283) · 테두리 바닥(top + 100 = 294) 이다. 캔버스 307 안이다.
       이 세 수를 바꿀 때는 반드시 **최악의 top** 으로 다시 검산할 것. */
    const rise = ease(clamp((c0 - 0.40) / 0.18));
    if (rise > 0.01) {
      const top = P2_Y + lerp(P2_SLIDE, 0, rise);
      ctx.globalAlpha = clamp(rise * 1.6);
      setShadow(ctx, GLOW, 16);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, 16, top, w - 32, P2_H, 9); ctx.stroke();
      clearShadow(ctx);

      /* 판의 이름과 규칙은 **맨 뒤**다 — 나레이션이 「재파종 잠금」을 부를 때 켜진다 */
      const nk = ease(clamp((c0 - 0.70) / 0.16));
      ctx.textAlign = 'left';
      if (spec.lockLabel && nk > 0.01) {
        ctx.globalAlpha = clamp(rise * 1.6) * clamp(nk * 1.4);
        ctx.font = disp(900, fit(spec.lockLabel, 900, 17, w - 76));
        ctx.fillStyle = tone('accent');
        ctx.fillText(spec.lockLabel, 32, top + 26);
      }
      if (spec.lockRule && nk > 0.01) {
        ctx.globalAlpha = clamp(rise * 1.6) * clamp(nk * 1.4);
        ctx.font = mono(700, fit(spec.lockRule, 700, 12, w - 72, 8, mono));
        ctx.fillStyle = tone('ink');
        ctx.fillText(spec.lockRule, 32, top + 48);
      }
      ctx.globalAlpha = clamp(rise * 1.6);

      /* 빈 밭 칸 — 잠금이 만든 결과가 여기 있다 (「밭을 비운」) */
      const cs = ease(clamp((c0 - 0.50) / 0.18));
      if (cs > 0.01) {
        const cw = 44, cg = 6, n = 5;
        for (let i = 0; i < n; i++) {
          const edge = (i === 0 || i === n - 1) ? 0.45 : 1;
          ctx.globalAlpha = clamp(cs * 1.4) * edge;
          ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
          roundRect(ctx, 32 + i * (cw + cg), top + 56, cw, 20, 4); ctx.stroke();
        }
      }

      /* 바닥 빗금 — 잠금이 계속 걸려 있다 (글자가 없는 띠에만) */
      ctx.save();
      ctx.globalAlpha = clamp(rise * 1.4) * 0.9;
      ctx.beginPath(); ctx.rect(32, top + 80, w - 64, 9); ctx.clip();
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      const step = 13, off = (t * 15) % step;
      for (let d = -20; d < w; d += step) {
        ctx.beginPath();
        ctx.moveTo(32 + d + off, top + 89);
        ctx.lineTo(32 + d + off + 9, top + 80);
        ctx.stroke();
      }
      ctx.restore();
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
