import {
  disp, ease, clamp, lerp, rnd,
  fitCanvas, mkCanvas, roundRect, tone, depthGrad, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* overshoot — 미리 그어 둔 선이 있고, 값이 그 선 위에 멈춰 선다.

   원문: "판정 기준은 미리 정해뒀습니다. 겨울이 끝나는 시점에 식량 잔량이 아슬아슬해야 하고,
   30을 넘으면 \"안락 실패\"로 보고 재조정하기로요." / "결과는 40이었습니다.
   재해까지 넣었는데도 겨울이 여전히 편했던 겁니다." / "여름이라는 계절, 폭염과 한파라는 재해"

   🔴 이 회차 네 샷이 공유하는 기본 도형은 **선 하나와 그 선에 걸린 값**이다. 여기서 그 선을
   세로 계기의 가로 기준선으로 처음 세우고, S2 는 같은 축을 네 개로 복제해 눌러 내리고,
   S3 은 그것을 눕히고, S4 는 두 값이 같은 높이에 서야 하는 선으로 쓴다.

   ① strikeCue — 계기가 서고 기둥이 이미 높이 차 있다. 하늘에서 쐐기 두 개가 차례로 떨어져
      기둥 머리를 때리는데, 기둥은 눌렸다가 **그대로 되돌아온다**. 그게 「재해를 내렸는데
      겨울이 편했다」다. 쐐기는 부딪힌 뒤 사라진다.
   ② verdictCue — 강조색 기준선(FAIL LINE 30)이 그어지고, 기둥 머리가 그 선보다 **위에서**
      멈추며 옆에 40 이 뜬다. 마지막에 「안락 실패」 판정 상자가 오른쪽 아래에 찍힌다.

   🔴 **눈금을 안 그렸다.** 원문이 준 수는 30 과 40 둘뿐이고 겨울 전 잔량에는 값이 없다.
   눈금을 그리면 그 높이가 값으로 읽혀 지어낸 수치가 된다. 화면에 뜨는 수는 둘뿐이다.

   🔴 흰 것과 강조색을 같은 픽셀에 겹치지 않는다. 기준선(강조색)은 기둥의 x 범위를 비켜
   **좌우 두 토막으로만** 긋고, 40(흰색)은 기둥 왼쪽 바깥에, 판정 상자(강조색)는 기준선보다
   아래·오른쪽에 둔다. 기둥은 처음부터 끝까지 흰색이다 — 문턱에서 색을 바꾸지 않는다.

   계속 도는 것 = 하늘에서 내려오는 낱개 표식(t 기반, 위치는 rnd(정수) 고정) · 기준선 점선의 흐름. */

const AX_TOP = 62, AX_BOT = 268;      // 계기 안쪽 세로 범위
const COL_W = 60;                     // 기둥 폭
const K_FAIL = 0.60;                  // 기준선 30 이 앉는 자리 (눈금은 안 그린다)
const K_END = 0.80;                   // 40 이 앉는 자리
const K_PRE = 0.93;                   // 겨울 전 — 값 없음(라벨 없음)

const yOf = k => AX_BOT - (AX_BOT - AX_TOP) * k;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const fit = (txt, weight, start, max, min = 8) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const cx = w * 0.32, x0 = cx - COL_W / 2, x1 = cx + COL_W / 2;
    const c0 = cue(spec.strikeCue ?? 0, 0.15, 0.75);
    const c1 = cue(spec.verdictCue ?? 1, 0.15, 0.9);

    const appear = ease(clamp(c0 / 0.30));
    if (appear <= 0.004) { ctx.textAlign = 'left'; return; }

    /* ── 하늘에서 계속 내려오는 것 ─────────────────
       자막과 무관하게 도는 배경. 기둥보다 먼저 그려 기둥이 덮게 한다.

       🔴 **낙하 구간을 캔버스 바닥에 안 닿게 잘랐다(2026-08-08 반려 수정).**
       초안은 `(t*sp + …) % (h + 44) − 22` 라 표식이 h 아래까지 내려갔고, 캔버스가 거기서
       잘리므로 **맨 아랫줄에 잉크가 남았다** — `check.mjs:500` 은 마지막 픽셀 행의 잉크를
       세고, 표식 3px 폭 × 여섯 개 = **edgeBot 18px** 로 red 였다. 세로 계기(바닥 268)가
       아니라 이 배경이 원인이다.
       이제 py 의 최댓값이 `FALL_END = h − 40` 이라 사각형 바닥이 **h − 31** 에서 멈추고,
       마지막 40px 에 걸쳐 알파가 0 으로 빠져 **끊기지 않고 사라진다**(툭 꺼지면 그것대로
       눈에 띈다). 위쪽은 −30 에서 들어오므로 캔버스 밖에서 등장한다. */
    const FALL_END = h - 40;
    ctx.fillStyle = tone('ink');
    for (let i = 0; i < 16; i++) {
      const px = 14 + rnd(i * 7 + 3) * (w - 28);
      const sp = 26 + rnd(i * 13 + 5) * 40;
      const py = ((t * sp + rnd(i * 5 + 1) * 460) % (FALL_END + 30)) - 30;
      if (py < -9) continue;
      const fade = Math.min(1, (FALL_END - py) / 46);
      ctx.globalAlpha = appear * 0.22 * fade;
      ctx.fillRect(px - 1.5, py, 3, 9);
    }
    ctx.globalAlpha = 1;

    /* ── 계기 ─────────────────────────────────── */
    ctx.globalAlpha = appear;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    roundRect(ctx, x0, AX_TOP, COL_W, AX_BOT - AX_TOP, 8); ctx.stroke();
    /* 🔴 라벨을 왼쪽이 아니라 기둥 오른쪽 바깥에 세운다 — 왼쪽 위(y 44)에 두면 떨어지는
       쐐기의 세로 획(x ≈ cx±14, y 40~57)이 글자 위를 지나간다. 오른쪽은 x1+18 부터
       비어 있고, 아래로 HEATWAVE·COLD SNAP·FAIL LINE 이 같은 열에 정렬된다. */
    if (spec.gaugeLabel) {
      ctx.textAlign = 'left';
      ctx.font = disp(900, fit(spec.gaugeLabel, 900, 11, w - 16 - (x1 + 18), 8));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.gaugeLabel, x1 + 18, 42);
    }
    ctx.globalAlpha = 1;

    /* ── 쐐기 두 개 — 때리는데 안 깎인다 ───────────
       u ∈ [0,0.72] 낙하 · 0.72 충돌 · [0.72,1] 사라짐. 충돌 뒤 기둥이 눌렸다 되돌아온다. */
    const uA = clamp((c0 - 0.28) / 0.32);
    const uB = clamp((c0 - 0.60) / 0.32);
    const dipOf = u => (u > 0.72 && u < 1) ? Math.sin(Math.PI * (u - 0.72) / 0.28) : 0;
    const dip = 10 * (dipOf(uA) + dipOf(uB));

    const settle = ease(clamp((c1 - 0.30) / 0.50));
    const topY = yOf(lerp(K_PRE, K_END, settle)) + dip;

    /* ── 기둥 ─────────────────────────────────── */
    ctx.globalAlpha = appear;
    setShadow(ctx, 'rgba(255,255,255,0.18)', 12);
    ctx.fillStyle = depthGrad(ctx, x0, topY, x1, AX_BOT, 'ink');
    roundRect(ctx, x0 + 5, topY, COL_W - 10, AX_BOT - 5 - topY, 6);
    ctx.fill();
    clearShadow(ctx);
    ctx.globalAlpha = 1;

    /* 쐐기 — 기둥 머리 위로 떨어진다 */
    const wedge = (u, wx, label, ly) => {
      if (u <= 0 || u >= 1) return;
      const wy = lerp(26, topY - 16, Math.min(1, u / 0.72));
      const a = clamp(u * 8) * clamp((1 - u) * 4.2);
      ctx.globalAlpha = a * appear;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(wx - 11, wy - 12); ctx.lineTo(wx, wy); ctx.lineTo(wx + 11, wy - 12);
      ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(wx, wy - 20); ctx.lineTo(wx, wy - 3);
      ctx.stroke();
      if (label) {
        ctx.textAlign = 'left';
        ctx.font = disp(900, fit(label, 900, 11, w - 20 - (x1 + 18), 8));
        ctx.fillStyle = tone('sub');
        ctx.fillText(label, x1 + 18, ly);
      }
      ctx.globalAlpha = 1;
    };
    wedge(uA, cx - 14, spec.hitA, 70);
    wedge(uB, cx + 14, spec.hitB, 92);

    /* ── 기준선 — 기둥의 x 범위를 비켜 두 토막으로만 ─ */
    const failA = ease(clamp((c1 - 0.15) / 0.25));
    if (failA > 0.02) {
      const fy = yOf(K_FAIL);
      ctx.globalAlpha = failA;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.setLineDash([12, 9]);
      ctx.lineDashOffset = -(t * 22) % 21;
      ctx.beginPath(); ctx.moveTo(16, fy); ctx.lineTo(x0 - 12, fy); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(x1 + 12, fy); ctx.lineTo(w - 16, fy); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      if (spec.failLabel) {
        ctx.textAlign = 'left';
        ctx.font = disp(900, fit(spec.failLabel, 900, 12, w - 16 - (x1 + 18), 8));
        ctx.fillStyle = tone('accent');
        ctx.fillText(spec.failLabel, x1 + 18, fy - 10);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 40 — 기둥 왼쪽 바깥, 흰색 ────────────────── */
    const numA = ease(clamp((c1 - 0.72) / 0.20));
    if (numA > 0.02 && spec.endLabel) {
      ctx.globalAlpha = numA;
      ctx.textAlign = 'right';
      ctx.font = disp(900, fit(spec.endLabel, 900, 30, x0 - 26, 14));
      ctx.fillStyle = tone('ink');
      ctx.fillText(spec.endLabel, x0 - 12, topY + 11);
      ctx.globalAlpha = 1;
    }

    /* ── 판정 — 기준선보다 아래·오른쪽 ─────────────── */
    const stamp = ease(clamp((c1 - 0.82) / 0.18));
    if (stamp > 0.02 && spec.verdict) {
      const bx = x1 + 26, bw = w - 18 - bx, by = 190, bh = 34;
      ctx.globalAlpha = stamp;
      setShadow(ctx, GLOW, 14);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, bx, by, bw, bh, 9); ctx.stroke();
      clearShadow(ctx);
      ctx.textAlign = 'center';
      ctx.font = disp(900, fit(spec.verdict, 900, 16, bw - 22, 9));
      ctx.fillStyle = tone('accent');
      ctx.fillText(spec.verdict, bx + bw / 2, by + bh / 2 + 6);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
