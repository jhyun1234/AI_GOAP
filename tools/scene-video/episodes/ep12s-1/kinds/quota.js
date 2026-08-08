import {
  disp, mono, ease, clamp, frac, spring,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* quota — 1년이 그냥 지나가고, 그다음 덩어리만 못 박힌다.

   원문: "눈이 내리고 겨울이 시작되면 — 아무 일도 일어나지 않습니다. 창고는 여전히 두둑하고,
   주민들은 배고프면 가서 밥을 먹고, 봄이 옵니다. 마을에 아무 사건도 없이 1년이 지나가는
   겁니다." / "겨울 대비 목표가 \"조리식 20개\"로 못 박혀 있었는데, 예전에 주민이 10명이던
   시절에 잡은 숫자였습니다."

   🔴 **2026-08-08 개정 — 위쪽 블록을 통째로 갈았다.** 옛 그림은 강조색 재해 쐐기(DISASTER)가
   내려오다 멈추는 것이었는데, 그건 옛 첫 자막(「재해를 내리기 전에, 이상한 걸 찾았어요」)을
   그린 것이었다. 4단 개정에서 그 줄이 사라지고(직전 편 ep11s 의 예고가 바뀌어 이어받을
   약속이 없어졌다) 새 첫 자막이 **「이 마을은 겨울이 안 힘들었어요」**가 됐으므로,
   그 문장을 그대로 그리는 것으로 바꿨다 — **1년 축을 표식이 지나가는데 그 아래 EVENTS 선이
   겨울 칸에서도 한 번도 안 튄다.** 재해는 이 편의 화면에 더 이상 없다(12-2·12-3 의 몫이다).

   🔴 이 회차의 축은 **「한 덩어리를 몇이 나누는가」** 다. 이 샷은 그 축의 첫 칸이고,
   일부러 **사람이 한 명도 없다** — 화면에 있는 것은 덩어리(20칸)와 그것을 못 박는 죔쇠뿐이다.
   S2 에서 사람이 들어오는 순간 이 그림이 무슨 뜻이었는지가 뒤늦게 읽힌다.

   ① findCue(첫 자막) — 위쪽 강조색 1년 축과 계절 이름이 들어오고, 그 아래 흰 EVENTS 선이
      왼쪽에서 오른쪽으로 곧게 뻗는다. 축 위 표식은 **자막과 무관하게** 계속 한 바퀴씩 돈다.
      표식이 WINTER 를 지날 때도 아래 선은 한 번도 안 튄다 = 「겨울이 안 힘들었어요」.
   ② fixCue(둘째 자막) — 20칸이 왼쪽 위부터 흰색으로 차오르고, 아래에 큰 흰 20 이 뜬 뒤
      흰 죔쇠 둘이 양옆에서 물려 들어와 FIXED 가 켜진다. 원문의 동사가 「못 박혀 있었는데」다.

   🔴 **c1 계열(fill · nA · nail)의 창 인자와 문턱을 개정에서 한 글자도 안 건드렸다.**
      S1 의 thud `delayMs` 2,070 이 `cue(fixCue, 0.15, 0.75)` 와 `nail` 의 문턱 0.62 에서
      나온 값이라, 여기를 만지면 소리가 다른 낱말로 밀린다. 바꾼 것은 cue 0 이 그리는
      위쪽 블록과, 그 아래 요소들의 y 좌표뿐이다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **행을 갈랐다.** 강조색은 전부 y ≤ 33
      (계절 이름 · 축 · 도는 표식)이고 흰색은 전부 y ≥ 68(EVENTS 라벨 아래로). 강조색
      후광(GLOW, 14)의 하한이 47 이라 21px 남는다.
      🟢 개정 전에는 강조색 검사 테두리의 아래 획이 격자 두 줄을 쓸고 지나갔다(검수 실측).
      지금은 격자·숫자·죔쇠 근처에 강조색이 **한 점도 없다.**

   🔴 클립을 한 군데도 안 쓴다(글자 복제 사고 예방). 텍스트는 전부 fitFont 로 폭을 재서 넣는다.

   계속 도는 것 = 1년 축을 도는 표식(주기 3.4초) · 축의 점선 흐름 · EVENTS 선 위를 같은 x 로
   따라가는 흰 표식 · 바닥 점선의 흐름. */

const NAME_Y = 14, TICK_T = 20, AXIS_Y = 28, DOT_R = 5;
const YEAR_SEC = 3.4;
const EV_Y = 78;
const CELL = 26, GAP = 6, COLS = 10;
const GRID_W = COLS * CELL + (COLS - 1) * GAP;   // 314
const R1 = 116, R2 = 148;
const NUM_Y = 232;
const CL_T = 196, CL_B = 246;
const BASE_Y = 292;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const cx = w / 2;
    const gx = cx - GRID_W / 2;

    const fitFont = (txt, weight, start, max, min = 8, fam = disp) => {
      let fs = start; ctx.font = fam(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = fam(weight, fs); }
      return fs;
    };

    const c0 = cue(spec.findCue ?? 0, 0.15, 0.70);
    const c1 = cue(spec.fixCue ?? 1, 0.15, 0.75);

    const axisOn = ease(clamp(c0 / 0.40));
    const flatK  = ease(clamp((c0 - 0.35) / 0.55));
    const fill   = ease(clamp(c1 / 0.55));
    const nail   = ease(clamp((c1 - 0.62) / 0.24));

    /* ── 바닥 점선 — 자막과 무관하게 계속 흐른다 ───── */
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.setLineDash([7, 9]);
    ctx.lineDashOffset = -(t * 16) % 16;
    ctx.beginPath(); ctx.moveTo(16, BASE_Y); ctx.lineTo(w - 16, BASE_Y); ctx.stroke();
    ctx.setLineDash([]); ctx.lineDashOffset = 0;

    /* ── 1년 축 : 표식이 계속 한 바퀴씩 돈다 ────────── */
    const AX = 20, AW = w - 40;
    const seasons = spec.seasons ?? [];
    if (axisOn > 0.02) {
      ctx.save();
      ctx.globalAlpha = clamp(axisOn * 1.4);

      /* 축 — 점선이 흘러 시간이 도는 것을 지고 있다 */
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.setLineDash([9, 7]);
      ctx.lineDashOffset = -(t * 20) % 16;
      ctx.beginPath(); ctx.moveTo(AX, AXIS_Y); ctx.lineTo(AX + AW * axisOn, AXIS_Y); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;

      /* 계절 경계 눈금 — 축 위로만 세운다(아래로 내리면 EVENTS 쪽에 가까워진다) */
      const n = Math.max(1, seasons.length);
      for (let i = 0; i <= n; i++) {
        const x = AX + (AW * i) / n;
        ctx.beginPath(); ctx.moveTo(x, TICK_T); ctx.lineTo(x, AXIS_Y); ctx.stroke();
      }
      /* 계절 이름 */
      ctx.textAlign = 'center';
      for (let i = 0; i < n; i++) {
        const nm = seasons[i];
        if (!nm) continue;
        const x = AX + (AW * (i + 0.5)) / n;
        ctx.font = mono(700, fitFont(nm, 700, 10.5, AW / n - 6, 7, mono));
        ctx.fillStyle = tone('accent');
        ctx.fillText(nm, x, NAME_Y);
      }
      ctx.restore();
    }

    /* 도는 표식 — cue 가 아니라 t 로만 돈다 */
    const dotX = AX + AW * frac(t / YEAR_SEC);
    if (axisOn > 0.02) {
      ctx.save();
      ctx.globalAlpha = clamp(axisOn * 1.6);
      setShadow(ctx, GLOW, 14);
      ctx.fillStyle = tone('accent');
      ctx.beginPath(); ctx.arc(dotX, AXIS_Y, DOT_R, 0, Math.PI * 2); ctx.fill();
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── EVENTS : 겨울 칸을 지나도 한 번도 안 튄다 ──── */
    let evX = 78;
    if (spec.yearLabel && flatK > 0.01) {
      ctx.globalAlpha = clamp(flatK * 1.6);
      ctx.textAlign = 'left';
      ctx.font = disp(800, fitFont(spec.yearLabel, 800, 12, 90));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.yearLabel, 14, EV_Y);
      evX = 14 + ctx.measureText(spec.yearLabel).width + 12;
      ctx.globalAlpha = 1;
    }
    if (flatK > 0.01) {
      const ex1 = evX, ex2 = w - 20;
      ctx.save();
      ctx.globalAlpha = clamp(flatK * 1.4);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.moveTo(ex1, EV_Y); ctx.lineTo(ex1 + (ex2 - ex1) * flatK, EV_Y);
      ctx.stroke();
      /* 축 표식과 같은 x 를 따라가는 흰 표식 — 선 위에서만 움직이고 절대 위아래로 안 튄다 */
      const fx = Math.min(ex1 + (ex2 - ex1) * flatK, Math.max(ex1, dotX));
      ctx.fillStyle = tone('ink');
      ctx.fillRect(fx - 3.5, EV_Y - 3.5, 7, 7);
      ctx.restore();
    }

    /* ── WINTER GOAL 격자 ──────────────────────────── */
    if (spec.goalLabel) {
      ctx.textAlign = 'center';
      ctx.font = disp(800, fitFont(spec.goalLabel, 800, 12, GRID_W - 20));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.goalLabel, cx, 106);
    }

    for (let i = 0; i < COLS * 2; i++) {
      const col = i % COLS, row = i < COLS ? 0 : 1;
      const x = gx + col * (CELL + GAP), y = row === 0 ? R1 : R2;

      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, x, y, CELL, CELL, 5); ctx.stroke();

      const k = clamp(fill * (COLS * 2 + 4) - i);
      if (k > 0.01) {
        const pad = 3 + (1 - k) * 9;
        ctx.globalAlpha = clamp(k * 1.6);
        ctx.fillStyle = tone('ink');
        roundRect(ctx, x + pad, y + pad, CELL - pad * 2, CELL - pad * 2, 3);
        ctx.fill();
        ctx.globalAlpha = 1;
      }
    }

    /* ── 못 박힌 숫자 ──────────────────────────────── */
    const num = spec.goalNumber ?? '';
    let bx = 62;
    const nA = ease(clamp((c1 - 0.28) / 0.30));
    if (num && nA > 0.01) {
      const fs = fitFont(num, 900, 48, w - 140);
      ctx.font = disp(900, fs);
      bx = Math.min((w - 26) / 2, Math.max(numHalf(ctx, num) + 24, 58));
      const sc = spring(nA);
      ctx.save();
      ctx.translate(cx, NUM_Y); ctx.scale(sc, sc); ctx.translate(-cx, -NUM_Y);
      ctx.globalAlpha = clamp(nA * 1.6);
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('ink');
      ctx.fillText(num, cx, NUM_Y);
      ctx.globalAlpha = 1;
      ctx.restore();
    }

    /* 죔쇠 둘이 양옆에서 물려 들어온다 */
    if (nail > 0.01) {
      const off = (1 - nail) * 46;
      ctx.globalAlpha = clamp(nail * 2);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
      for (const s of [-1, 1]) {
        const x = cx + s * (bx + off);
        ctx.beginPath();
        ctx.moveTo(x, CL_T); ctx.lineTo(x, CL_B);
        ctx.moveTo(x, CL_T); ctx.lineTo(x - s * 13, CL_T);
        ctx.moveTo(x, CL_B); ctx.lineTo(x - s * 13, CL_B);
        ctx.stroke();
      }
      ctx.globalAlpha = 1;

      if (spec.fixedLabel) {
        ctx.textAlign = 'center';
        ctx.font = disp(800, fitFont(spec.fixedLabel, 800, 12, w - 60));
        ctx.globalAlpha = clamp((nail - 0.4) / 0.4);
        ctx.fillStyle = tone('sub');
        ctx.fillText(spec.fixedLabel, cx, CL_B + 20);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};

/** 현재 ctx.font 기준 글자 폭의 절반 — 죔쇠가 숫자를 안 침범하게 재는 데만 쓴다. */
function numHalf(ctx, s) { return ctx.measureText(s).width / 2; }
