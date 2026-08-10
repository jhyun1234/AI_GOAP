import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* humanonly — 표준 작업 흐름을 한 줄로 편 뒤, 그 줄 가운데 한 칸만 사람 색으로 둔다.
   흐름 표식은 그 칸 앞에서 멈춰 대기하고, 사람 칸이 켜져야 다음으로 넘어간다.
   칸 이름은 Docs/개발_방법론_명세서.md §4 표준 작업 흐름의 실제 단계명이고,
   `[사용자 Play 검증]` 은 그 문서에 대괄호까지 그대로 있는 문자열이다.
   연속 모션 = 사람 칸 앞까지 밀려와 멈추는 **대기 띠** 하나뿐이다(멈춰 있는 게
   아니라 기다리고 있다는 뜻이라, 대기 자체가 움직인다). 3차 개정에서 이 띠와
   겹쳐 돌던 대기 점 둘을 지웠다 — 아래 주석 참조. */

const STEPS = [
  { s: 'spec-write', ai: true },
  { s: 'spec-implement', ai: true },
  { s: 'spec-review', ai: true },
  { s: '사용자 Play 검증', ai: false },
  { s: 'devlog', ai: true },
];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    const kChain = ease(cue(0));   // AI가 못 하는 일도 있다
    const kPlay = ease(cue(1));    // 정식 단계로 박혀 있다
    const kFour = ease(cue(2));    // 규칙이 하루에 네 번 밟혔다
    const kAll = ease(cue(3));     // 전부 Play가 먼저 잡았다 (v2: 인용을 두 줄로 나눴다)

    const gap = 8;
    const cw = (w - M * 2 - gap * 4) / 5;
    const cy = 62, ch = 58;
    const HUMAN = 3;

    /* 🔴 2차 개정 (검수 C6): 연속 모션이 반지름 5px 대기 표식 둘뿐이라 문턱 아래였다.
       사람 칸 앞에서 **띠째로** 밀려와 멈추게 바꿨다 — 대기 줄이 두꺼워질수록
       「사람이 봐 주기 전에는 못 넘어간다」가 세진다. 칸보다 먼저 그린다. */
    {
      const gateX = M + HUMAN * (cw + gap) - 6;
      const open = kPlay > 0.5;
      const BR = 2.2, bk = (t % BR) / BR;
      const bx0 = lerp(M + cw * 0.3, open ? w - M - cw * 0.3 : gateX - 26, ease(bk));
      ctx.fillStyle = tone('track');
      ctx.fillRect(bx0 - 24, cy - 14, 48, ch + 28);
    }

    ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('표준 작업 흐름', M, 34);

    for (let i = 0; i < STEPS.length; i++) {
      const cx = M + i * (cw + gap);
      const human = !STEPS[i].ai;
      const kIn = human ? kPlay : clamp((kChain - i * 0.08) / 0.4);
      const passed = i < HUMAN ? kIn : (kPlay > 0.4 ? kIn : 0);

      ctx.strokeStyle = kIn > 0.1 ? (human ? tone('ink') : tone('accent')) : tone('track');
      ctx.lineWidth = human ? 4 : 3;
      roundRect(ctx, cx, cy, cw, ch, 4); ctx.stroke();

      // 이름 (폭을 재서 칸 안에)
      let fs = human ? 15 : 13;
      ctx.font = disp(human ? 900 : 700, fs);
      while (ctx.measureText(STEPS[i].s).width > cw - 14 && fs > 9) {
        fs -= 1; ctx.font = disp(human ? 900 : 700, fs);
      }
      const sw = ctx.measureText(STEPS[i].s).width;
      ctx.fillStyle = kIn > 0.1 ? (human ? tone('ink') : tone('accent')) : tone('sub');
      ctx.fillText(STEPS[i].s, cx + (cw - sw) / 2, cy + 34);

      if (human) {
        ctx.font = disp(700, 10); ctx.fillStyle = tone('ink');
        const s2 = '사람';
        const w2 = ctx.measureText(s2).width;
        ctx.fillText(s2, cx + (cw - w2) / 2, cy + ch - 8);
      } else if (passed > 0.05) {
        ctx.fillStyle = tone('accent');
        ctx.fillRect(cx + 8, cy + ch - 13, (cw - 16) * ease(passed), 4);
      }

      // 이음선
      if (i < STEPS.length - 1) {
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(cx + cw, cy + ch / 2); ctx.lineTo(cx + cw + gap, cy + ch / 2);
        ctx.stroke();
      }
    }

    /* 🔴 3차 개정 (검수 R-2c): 여기 있던 **r5 대기 점 둘**을 지웠다.
       점의 y 가 `cy + ch/2`(= 91) 이고 칸 이름의 baseline 이 `cy + 34`(= 96) 이라
       점이 라벨 글자 위를 지나며 `spec-review●●` · `dev●og` 로 읽혔다 —
       21.8초 샷 내내 계속되는 겹침이었다(증거 `build/stills/352s.png`·`366s.png`).
       2차 개정이 「점 → 띠 교체」라고 자기보고했지만 실제로는 **띠를 더하고 점은
       남겨 둔 것**이었다(검수 C11). 대기의 뜻은 위의 track 띠가 이미 진다 —
       띠가 사람 칸 앞(`gateX - 26`)에서 멈췄다가 kPlay 가 열리면 오른쪽 끝까지
       나간다. 그러니 점은 뜻을 더하지 않고 판독만 깎았다. */

    // ── 아래 : 하루에 네 번 ────────────────────────
    if (kFour > 0.05) {
      const by = h - 46;
      ctx.save(); ctx.globalAlpha = clamp(kFour);
      ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText('하루에 밟힌 규칙', M, by - 6);
      const cx0 = M, bw = 30, bg = 9;
      for (let i = 0; i < 4; i++) {
        const k = clamp((kFour - i * 0.14) / 0.4);
        const bx = cx0 + i * (bw + bg);
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        roundRect(ctx, bx, by + 6, bw, 22, 3); ctx.stroke();
        if (k > 0.05) {
          ctx.fillStyle = tone('ink');
          ctx.fillRect(bx + 5, by + 12, (bw - 10) * ease(k), 10);
        }
      }
      /* 판정 문구는 그 말을 하는 자막(cue 3)에서 뜬다 — 네 칸이 먼저 차고, 그 다음에 온다. */
      if (kAll > 0.05) {
        ctx.globalAlpha = clamp(kAll);
        ctx.font = disp(900, 17); ctx.fillStyle = tone('ink');
        const s = '전부 Play가 먼저 잡았다';
        const sw = ctx.measureText(s).width;
        ctx.fillText(s, Math.min(w - M - sw, cx0 + 4 * (bw + bg) + 14), by + 24);
      }
      ctx.restore();
    }
  },
};
