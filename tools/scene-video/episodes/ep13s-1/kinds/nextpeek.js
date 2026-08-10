import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone
} from '../../../engine/lib.js';

/* nextpeek — 아웃트로(SO). **다음 편(13-2)** 기능의 0.5초 테스트 장면을 루프로 돌린다.

   무엇을 그리나 — 근거는 전부 같은 원문의 13-2 몫 문단이다:
   > "카탈로그 전체에서 가장 싼 액션이 5였다가 갑자기 1짜리가 등장하면서, 그 어림값이
   >  5분의 1로 쪼그라들었습니다. 어림값이 낮아지면 플래너는 '아직 멀었나?' 하며 훨씬 더
   >  많은 경우의 수를 뒤지게 되고, 결국 탐색 한도를 넘겨 계획 실패로 떨어집니다.
   >  간호를 싸게 만들었더니 광부가 돌을 못 캐게 된 겁니다."
   > "NoSolutionFound는 버그 증상 — MAX_NODES 인상으로 해결 금지"

   → 반 초마다 이것이 되풀이된다: **최저 비용이 5 옆에 1 로 내려앉자, 뒤지는 경우의 수가
   격자를 채우며 탐색 한도선을 넘어가고, 돌 캐기 계획이 실패한다.**

   🔴 이 편 본문의 그림(마을 띠 · 쐐기 · 무덤)을 하나도 안 쓴다 — 다음 편은 마을이 아니라
   플래너 안에서 일어나는 일이라 도형이 달라야 한다. 여기서 강조색은 **비용 1** 과
   **탐색 한도선** 둘뿐이고(= 내가 잘못 적은 값과 그것이 넘긴 선), 흰색은 플래너가 실제로
   뒤진 노드다.

   🔴 `NoSolutionFound` 만 영어다 — 콘솔에 그 문자열 그대로 찍히는 **인용**이라 옮기면
   증거가 아니게 된다(개정 ADR-V-10 의 예외 ⓐ). 나머지 라벨은 전부 한국어다.

   🔴 수치는 kind 상수가 아니라 spec 이다. 격자 45칸 중 한도 27칸은 **비율이 아니라
   자리**를 정하는 값이고, 화면에 숫자로 적지 않는다 — 원문에 노드 수가 없기 때문이다.
   적는 수는 원문에 있는 **5 와 1** 뿐이다.

   ⏱ 등장만 예고 자막 `cue(peekCue)` 에 물린다. 루프는 `t` 로만 돌아, 카드가 뜨기 전
      구간에 정적이 생기지 않는다.
   🔴 소개 자막(cue 1)에는 아무것도 안 걸었다 — 마지막 3.0초는 아웃트로 카드가 `.vis` 를
      픽셀 동일 좌표로 덮으므로 그 구간에 그린 것은 한 프레임도 안 보인다.

   🔴 겹침 감사 (두 축)
   · 강조색 「1」(x 155~169 · y 44~68)에는 **후광을 안 준다** — 위 점선(34)과 10px 뿐이다.
     흰 「5」 오른끝 125 와는 30px.
   · 강조색 한도선(x 90~314 · y 159~162, 후광 없음) ↔ 격자 3행 바닥 151 = 8px,
     4행 위 172 = 10px. 왼쪽으로 「탐색 한도」 글자 오른끝 78 과 12px 이고, 격자가 x 96
     부터라 라벨과 노드는 **x 가 아예 안 겹친다**(라벨 글리프 y 144~159 가 3행과 같은 띠라
     x 로 갈라 놓은 것이다).
   · 흰↔흰: 「다음 편」 바닥 25 ↔ 점선 34 = 9 · 「최저 비용」(x 16~80) ↔ 「5」(x 111~) = 31 ·
     숫자 바닥 68 ↔ 격자 1행 106 = 38 · 격자 5행 바닥 197 ↔ 판정 글리프 위 213 = 16 ·
     판정 바닥 237 ↔ 코드 글리프 위 247 = 10. 가장 아래는 264 로 캔버스(307)에서 43px 남는다. */

const RULE_Y = 34;
const COST_LBL_Y = 64, NUM_Y = 68, NUM_FROM_X = 118, NUM_TO_X = 162;
const LIMIT_LBL_Y = 156, LIMIT_Y = 160.5, LIMIT_X0 = 90;
const GRID_X = 96, GRID_STEP = 26, COLS = 9, CELL = 5;
const ROWS_Y = [106, 126, 146, 172, 192];
const VERDICT_Y = 232, CODE_Y = 260;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const fitFont = (txt, weight, start, max, min = 8, fam = disp) => {
      let fs = start; ctx.font = fam(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = fam(weight, fs); }
      return fs;
    };
    const putC = (txt, font, cx, y, color, alpha) => {
      ctx.font = font;
      const tw = ctx.measureText(txt).width;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.textAlign = 'center';
      ctx.fillStyle = color;
      ctx.fillText(txt, clamp(cx, 8 + tw / 2, w - 8 - tw / 2), y);
      ctx.restore();
    };

    const ak = ease(cue(spec.peekCue ?? 0, 0.15, 0.30));
    if (ak <= 0.02) { ctx.textAlign = 'left'; return; }

    /* ── 상단 라벨 + 구분선 ──────────────────────── */
    if (spec.label) {
      ctx.save();
      ctx.globalAlpha = ak;
      ctx.textAlign = 'left';
      ctx.font = disp(800, fitFont(spec.label, 800, 12.5, 120));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.label, 14, 22);
      ctx.restore();
    }
    ctx.save();
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(16, RULE_Y); ctx.lineTo(16 + (w - 32) * ak, RULE_Y); ctx.stroke();
    ctx.restore();

    const inner = clamp((ak - 0.35) / 0.65);
    if (inner <= 0.01) { ctx.textAlign = 'left'; return; }

    /* ── 0.5초 루프 ──────────────────────────────── */
    const loop = spec.loop ?? 0.5;
    const ph = frac(t / loop);
    const swap = ease(clamp(ph / 0.18));
    const fillK = ease(clamp((ph - 0.14) / 0.44));
    const off = ease(clamp((ph - 0.84) / 0.16));
    const live = inner * (1 - off);

    /* 최저 비용 : 5 옆에 1 이 내려앉는다 */
    if (spec.costLabel) {
      ctx.save();
      ctx.globalAlpha = inner;
      ctx.textAlign = 'left';
      ctx.font = disp(800, fitFont(spec.costLabel, 800, 12.5, 88));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.costLabel, 16, COST_LBL_Y);
      ctx.restore();
    }
    putC(String(spec.costFrom ?? '5'), mono(900, 24), NUM_FROM_X, NUM_Y,
      tone('ink'), inner * (1 - 0.6 * swap * (1 - off)));
    putC(String(spec.costTo ?? '1'), mono(900, 24), NUM_TO_X,
      lerp(NUM_Y + 16, NUM_Y, swap), tone('accent'), live * swap);

    /* 탐색 한도 — 라벨은 왼쪽 레인, 선은 격자 위에만 */
    if (spec.limitLabel) {
      ctx.save();
      ctx.globalAlpha = inner;
      ctx.textAlign = 'left';
      ctx.font = disp(800, fitFont(spec.limitLabel, 800, 12.5, 62));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.limitLabel, 16, LIMIT_LBL_Y);
      ctx.restore();
    }
    ctx.save();
    ctx.globalAlpha = inner;
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;   // 후광 없음 — 격자와 8px 이다
    ctx.beginPath();
    ctx.moveTo(LIMIT_X0, LIMIT_Y);
    ctx.lineTo(GRID_X + (COLS - 1) * GRID_STEP + CELL, LIMIT_Y);
    ctx.stroke();
    ctx.restore();

    /* 뒤지는 경우의 수 — 격자가 채워지며 한도선을 넘어간다 */
    const total = COLS * ROWS_Y.length;
    const nodes = total * fillK;
    ctx.fillStyle = tone('ink');
    for (let r = 0; r < ROWS_Y.length; r++) {
      for (let c = 0; c < COLS; c++) {
        const idx = r * COLS + c;
        const lit = clamp((nodes - idx) * 1.3) * (1 - off);
        if (lit <= 0.02) continue;
        ctx.globalAlpha = inner * lit;
        ctx.fillRect(GRID_X + c * GRID_STEP, ROWS_Y[r], CELL, CELL);
      }
    }
    ctx.globalAlpha = 1;

    /* 넘어간 뒤에야 판정이 뜬다 */
    const over = clamp((nodes - (spec.limitAt ?? 27)) / 10) * (1 - off);
    if (over > 0.02) {
      if (spec.verdict) putC(spec.verdict, disp(900, fitFont(spec.verdict, 900, 19, w - 40)),
        w / 2, VERDICT_Y, tone('ink'), inner * clamp(over * 1.5));
      if (spec.code) putC(spec.code, mono(700, fitFont(spec.code, 700, 12.5, w - 44, 8, mono)),
        w / 2, CODE_Y, tone('sub'), inner * clamp(over * 1.5));
    }

    ctx.textAlign = 'left';
  }
};
