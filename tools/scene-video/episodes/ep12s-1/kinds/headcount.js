import {
  disp, mono, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* headcount — 나누는 쪽이 줄어드는데 덩어리는 안 변한다.

   원문: "그 뒤 주민을 4명으로 줄였는데도 20이라는 숫자는 그대로 남았습니다." /
   "10명이 먹을 양을 4명이 나눠 먹으니 겨울이 안락할 수밖에요." / 절 제목 "문제: 겨울이 안락했다"

   🔴 이 샷이 이 회차 축(「한 덩어리를 몇이 나누는가」)의 심장이다. 세 층이 같은 x 눈금을 쓴다.
   ① 위 — 20칸 흰 막대. **처음부터 끝까지 한 칸도 안 변한다.** 변하는 것은 양 끝 죔쇠의
      굵기뿐이고, 그게 「그대로였죠」의 그림이다.
   ② 가운데 — 주민 표식 열 개. 오른쪽부터 여섯이 차례로 꺼져 점선 윤곽만 남는다.
   ③ 아래 — PER PERSON 강조색 막대. 두 칸에서 다섯 칸으로 자란다.
      🔑 **위 막대와 같은 칸 간격(pitch)으로 그린다.** 그래야 눈이 칸을 세서 「두 칸이던 게
      다섯 칸이 됐다」를 읽는다. 숫자는 한 자도 안 적는다 — 20÷10 과 20÷4 는 원문의 두 수에서
      자동으로 나오는 폭이라 길이로만 말한다.
   마지막에 원문 절 제목 그대로 「겨울이 안락했다」가 뜬다.

   🔴 흰 것과 강조색이 같은 픽셀에 안 겹친다 — 흰색은 막대(34~62) · 죔쇠(26~70) ·
      표식(119~141) · 판정문(240~258), 강조색은 1인 몫 막대(186~220) **하나뿐**이다.
      그 위 눈금자(176~182)는 track(흰 0.12)이라 4px 떨어져 있다.

   🔴 클립은 1인 몫 막대 안 홈에만 쓰고, 그 안에는 글자가 없다.

   ⏱ 🔴 **2026-08-08 개정 — cue 둘을 하나로 접었다.** 4단 전환에서 이 샷의 첫 자막
      (「근데 4명으로 줄이고도 20은 그대로였죠」)이 훅과 겹쳐 빠졌고, 샷이 1줄이 됐다.
      `engine.js` 의 cue 는 `s.rel[Math.min(i, s.rel.length - 1)]` 이라 `shareCue: 1` 을
      그대로 뒀으면 **에러 없이 cue(0) 으로 접혀** 1인 몫 막대가 자막 첫 프레임에 다 자라
      있었을 것이다(`check.mjs` 도 못 잡는 고장이다). 그래서 한 자막 안의 **뒷구간**으로
      순서를 다시 세웠다 — 값이 먼저 움직이고 장식이 나중이라는 제약을 손 검산으로 지켰다.
        cut     0    → 0.42   표식 여섯이 꺼진다        … "열 명 몫을 네"
        clampK  0.30 → 0.60   죔쇠가 굵어진다          … "명 몫을 네 명이"
        share   0.46 → 0.78   1인 몫이 두 칸 → 다섯 칸  … "명이 먹으니"
        verdict 0.80 → 0.98   판정문이 뜬다            … "겨울이 편하죠"

   계속 도는 것 = 남은 표식의 바운스 · 꺼진 표식 윤곽의 점선 흐름 · 1인 몫 막대 안 홈의 흐름. */

const CELL = 13, GAP = 2.4, N = 20;
const PITCH = CELL + GAP;                      // 15.4
const BAR_W = N * CELL + (N - 1) * GAP;        // 305.6
const BAR_Y = 34, BAR_H = 28;
const CLAMP_T = 26, CLAMP_B = 70;
const MARK_Y = 130, MARK_R = 11, MARKS = 10, ALIVE = 4;
const RULER_Y = 176, RULER_H = 6;
const SHARE_Y = 186, SHARE_H = 34;
const VERDICT_Y = 258;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const x0 = (w - BAR_W) / 2;

    const fitFont = (txt, weight, start, max, min = 8, fam = disp) => {
      let fs = start; ctx.font = fam(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = fam(weight, fs); }
      return fs;
    };

    const c = cue(spec.beatCue ?? 0, 0.15, 0.78);

    const cut     = ease(clamp(c / 0.42));
    const clampK  = ease(clamp((c - 0.30) / 0.30));
    const share   = ease(clamp((c - 0.46) / 0.32));
    const verdict = ease(clamp((c - 0.80) / 0.18));

    /* ── 위 : 20칸. 한 칸도 안 변한다 ──────────────── */
    if (spec.goalLabel) {
      ctx.textAlign = 'left';
      ctx.font = disp(800, fitFont(spec.goalLabel, 800, 12, 150));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.goalLabel, 14, 22);
    }
    if (spec.fixedLabel && clampK > 0.02) {
      ctx.textAlign = 'right';
      ctx.font = disp(800, fitFont(spec.fixedLabel, 800, 12, 120));
      ctx.globalAlpha = clamp(clampK * 1.6);
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.fixedLabel, w - 14, 22);
      ctx.globalAlpha = 1;
    }

    ctx.fillStyle = tone('ink');
    for (let i = 0; i < N; i++) {
      roundRect(ctx, x0 + i * PITCH, BAR_Y, CELL, BAR_H, 3);
      ctx.fill();
    }

    /* 양 끝 죔쇠가 굵어진다 — 「그대로였죠」 */
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3 + 3 * clampK;
    for (const s of [-1, 1]) {
      const x = s < 0 ? x0 - 10 : x0 + BAR_W + 10;
      ctx.beginPath();
      ctx.moveTo(x, CLAMP_T); ctx.lineTo(x, CLAMP_B);
      ctx.moveTo(x, CLAMP_T); ctx.lineTo(x - s * 12, CLAMP_T);
      ctx.moveTo(x, CLAMP_B); ctx.lineTo(x - s * 12, CLAMP_B);
      ctx.stroke();
    }

    /* ── 가운데 : 주민 10 -> 4 ─────────────────────── */
    if (spec.headLabel) {
      ctx.textAlign = 'left';
      ctx.font = disp(800, fitFont(spec.headLabel, 800, 12, 140));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.headLabel, 14, 100);
    }
    ctx.textAlign = 'left';
    ctx.font = mono(700, 15);
    ctx.fillStyle = tone('sub');
    if (spec.headFrom) ctx.fillText(spec.headFrom, 186, 100);
    if (spec.headTo) {
      ctx.globalAlpha = clamp(cut * 1.8);
      ctx.fillText('->', 214, 100);
      ctx.fillText(spec.headTo, 250, 100);
      ctx.globalAlpha = 1;
    }

    const step = (w - 60) / (MARKS - 1);
    for (let j = 0; j < MARKS; j++) {
      const mx = 30 + j * step;
      if (j < ALIVE) {
        const my = MARK_Y + Math.sin(t * 5.2 + j * 1.3) * 3;
        ctx.fillStyle = tone('ink');
        ctx.beginPath(); ctx.arc(mx, my, MARK_R, 0, Math.PI * 2); ctx.fill();
      } else {
        /* 오른쪽 끝부터 차례로 꺼진다 — 살아남는 넷 쪽으로 접혀 들어오는 파도가 된다 */
        const k = ease(clamp((cut - (MARKS - 1 - j) * 0.09) / 0.42));
        if (k < 0.999) {
          ctx.globalAlpha = 1 - k;
          ctx.fillStyle = tone('ink');
          ctx.beginPath(); ctx.arc(mx, MARK_Y, MARK_R * (1 - 0.25 * k), 0, Math.PI * 2); ctx.fill();
          ctx.globalAlpha = 1;
        }
        if (k > 0.02) {
          ctx.globalAlpha = clamp(k) * 0.85;
          ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
          ctx.setLineDash([5, 6]);
          ctx.lineDashOffset = -(t * 14) % 11;
          ctx.beginPath(); ctx.arc(mx, MARK_Y, MARK_R, 0, Math.PI * 2); ctx.stroke();
          ctx.setLineDash([]); ctx.lineDashOffset = 0;
          ctx.globalAlpha = 1;
        }
      }
    }

    /* ── 아래 : 1인 몫. 위 막대와 같은 칸 간격 ─────── */
    if (spec.shareLabel) {
      ctx.textAlign = 'left';
      ctx.font = disp(800, fitFont(spec.shareLabel, 800, 12, 150));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.shareLabel, 14, 166);
    }

    /* 칸 눈금자 — 강조색 막대가 몇 칸인지 눈으로 세게 한다 */
    ctx.fillStyle = tone('track');
    for (let i = 0; i <= 6; i++) ctx.fillRect(x0 + i * PITCH - 1.5, RULER_Y, 3, RULER_H);

    const n = lerp(2, 5, share);
    const sw = n * CELL + (n - 1) * GAP;
    setShadow(ctx, GLOW, 14);
    ctx.fillStyle = tone('accent');
    roundRect(ctx, x0, SHARE_Y, sw, SHARE_H, 6); ctx.fill();
    clearShadow(ctx);

    /* 막대 안 홈이 흐른다 — 글자가 없는 도형이라 클립해도 안전하다 */
    ctx.save();
    roundRect(ctx, x0, SHARE_Y, sw, SHARE_H, 6); ctx.clip();
    ctx.fillStyle = tone('bg');
    const off = (t * 17) % PITCH;
    for (let d = -PITCH; d < sw + PITCH; d += PITCH) {
      ctx.fillRect(x0 + d + off, SHARE_Y, 3.4, SHARE_H);
    }
    ctx.restore();

    /* ── 판정 ─────────────────────────────────────── */
    if (spec.verdict && verdict > 0.01) {
      ctx.textAlign = 'center';
      ctx.font = disp(900, fitFont(spec.verdict, 900, 20, w - 56));
      ctx.globalAlpha = clamp(verdict * 1.5);
      ctx.fillStyle = tone('ink');
      ctx.fillText(spec.verdict, w / 2, VERDICT_Y);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
