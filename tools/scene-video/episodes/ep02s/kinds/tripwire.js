import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* tripwire — 방향이 다시 뒤집히면 걸린다.

   왼쪽에서 오른쪽으로 실선 하나가 그어져 있다. 실선의 오른쪽 끝은 '커밋' 이라는 도장.
   화살표가 정방향(왼→오)으로 지나가면 실선이 그대로 있고 커밋 도장에 초록 등이 들어온다.
   역방향(오→왼)으로 지나가려는 순간, 실선이 팽팽해지며 붙잡는다 — '커밋 전에 잡힘' 표식이
   붙는다. 실선은 T14_GoalThresholdConsistency 라는 이름의 문턱이다. ep00s handoff(신호를
   넘겨받는 자리)와 다르다 — 여기서는 잡아채는 자리다.

   회차의 기본 도형 안에서 이 그림의 몫은 '방향이 다시 뒤집히면 걸린다'.
   계속 도는 것 = ① 실선을 오가는 화살표(정방향은 통과, 역방향은 걸림)
                  ② 실선 자신을 훑고 지나가는 검사 신호(사건이 아니라 재질 — 아래 재작업 2차).

   🔴 재작업 추가(원문 71행) — 안전망은 **테스트 4종**이었다. 실선 위에 자리가 넷 있고
   그중 하나에만 이름이 붙는다. 나머지 셋의 이름은 원문에 없으므로 만들지 않고
   익명 자리(···)로 둔다 — wardline 이 6종 성격에 쓴 것과 같은 규칙이다.
   "넷 중 하나가 방향까지 본다"는 원문의 규모와, 이름을 지어내지 않는다는 규칙을 동시에 지킨다.

   🔴 재작업 2차 — 실선이 스스로 훑는다(정적 구간 3.6s 해소).
   자막이 3줄로 늘면서 샷이 8.43초가 됐고, 화살표가 "그냥 지나가기만 하는" 구간이 한 번 더
   들어와 3.6초 동안 기계 눈에 정지 화면이 됐다. 화살표는 실제로 움직이지만 화살촉이
   약 84px² 라 바뀌는 잉크가 판정 임계에 못 미친다 — 즉 **이동 거리가 아니라 면적** 문제다.
   그래서 계속 도는 것을 하나 더 심되, 새 사건이 아니라 **실선의 재질**로 심었다:
   실선을 따라 훑고 지나가는 검사 신호. 원문 71행이 T14 를 "자동으로 검증합니다"라고
   쓴 그 상시성이다. 굵기가 부푸는 것이지 실선 위에 얹힌 별개 물체가 아니고(색도 실선과
   같은 색을 따라간다), 걸림 상태에서는 휘어진 실선을 그대로 타고 넘는다. 커밋 도장에
   닿기 전에 사라지도록 오른쪽 끝에서 먼저 꺼진다 — 도착은 화살표의 몫이고 신호의 몫이 아니다. */

const CYCLE = 5.2;   // 한 번 왕복하는 초 : 정방향 → 역방향(걸림) → 정방향
const SWEEP = 2.4;   // 검사 신호가 실선을 한 번 훑는 초
const NSWEEP = 4;    // 실선 위에 동시에 떠 있는 신호 수

/** 실선을 따라 x0→x1 구간을 긋는다. 휘어 있으면 휜 대로 탄다(yAt 가 실선의 세로 위치). */
function strokeAlong(ctx, x0, x1, yAt, lo, hi) {
  const a = clamp(x0, lo, hi), b = clamp(x1, lo, hi);
  if (b - a < 0.5) return;
  ctx.beginPath();
  const N = 6;
  for (let k = 0; k <= N; k++) {
    const x = lerp(a, b, k / N);
    if (k) ctx.lineTo(x, yAt(x)); else ctx.moveTo(x, yAt(x));
  }
  ctx.stroke();
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const ok = ease(cue(spec.okCue ?? 1, 0.15, 0.5));
    const sk = ease(cue(spec.testCue ?? 1, 0.15, 0.5));
    const tk = ease(cue(spec.trapCue ?? 2, 0.15, 0.5));

    const cy = h / 2 - 10;
    const xL = 36, xR = w - 60;
    const PAD = 12;

    // 자리 넷 — 하나만 이름이 붙는다
    const nTest = Math.max(1, spec.tests || 4);
    const namedAt = Math.min(spec.namedAt ?? 1, nTest - 1);
    const slotX = i => xL + (xR - xL) * (i + 0.5) / nTest;

    ctx.textBaseline = 'alphabetic';

    // 몇 종이었나 (원문 71행) — 좌상단
    if (sk > 0.02) {
      ctx.globalAlpha = clamp(sk * 1.4);
      ctx.textAlign = 'left';
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
      ctx.fillText(spec.testsLabel || '', PAD, 16);
      ctx.globalAlpha = 1;
    }

    // 문턱 라벨 — 이름이 붙은 그 하나의 위에 선다
    ctx.textAlign = 'center';
    ctx.fillStyle = tone('accent'); ctx.font = mono(700, 11);
    let ls = 11;
    const lbl = spec.gateLabel || '';
    while (ls > 8 && ctx.measureText(lbl).width > w - 2 * PAD) { ls -= 0.5; ctx.font = mono(700, ls); }
    const lw = ctx.measureText(lbl).width;
    const lcx = lw < w - PAD * 2
      ? clamp(slotX(namedAt), PAD + lw / 2, w - PAD - lw / 2)
      : w / 2;
    ctx.fillText(lbl, lcx, 40);

    // 커밋 도장 (오른쪽 끝)
    const commitCx = xR + 20;
    const rC = 22;
    if (ok > 0.3) {
      setShadow(ctx, GLOW, 12, 0);
      ctx.fillStyle = tone('accent');
      ctx.beginPath(); ctx.arc(commitCx, cy, rC, 0, Math.PI * 2); ctx.fill();
      clearShadow(ctx);
      ctx.fillStyle = '#000'; ctx.font = disp(900, 10);
      ctx.textAlign = 'center';
      ctx.fillText(spec.commitLabel || '커밋', commitCx, cy + 4);
    } else {
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(commitCx, cy, rC, 0, Math.PI * 2); ctx.stroke();
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
      ctx.textAlign = 'center';
      ctx.fillText(spec.commitLabel || '커밋', commitCx, cy + 4);
    }

    /* ── 왕복 화살표 : 계속 도는 것 ─────────────
       위상 u 를 0~1 로 잡고
         u < 0.35 : 정방향 진행 (xL → xR), 실선 곧게
         0.35 <= u < 0.5 : 커밋 도장 근처에서 잠깐 머묾 (통과)
         0.5 <= u < 0.85 : 역방향 진행 (xR → xL)이 시도되지만 실선에 걸림
         u >= 0.85 : 다시 정방향으로 리셋
       실선은 역방향 구간에서 팽팽하게 위로 휘어진다 — 잡아채는 그림. */
    const u = frac(t / CYCLE);
    let ax, forward, trapped = false, latchDepth = 0;
    if (u < 0.35) {
      ax = lerp(xL, xR, u / 0.35); forward = true;
    } else if (u < 0.5) {
      ax = xR; forward = true;
    } else if (u < 0.85) {
      // 역방향 시도 — 왼쪽으로 조금 가다가 실선에 걸려 튕긴다
      const rv = (u - 0.5) / 0.35;
      // 처음 20%는 뒤로 가는 척, 이후는 실선에 걸린 자리에서 진동
      if (rv < 0.2) {
        ax = lerp(xR, xR - 60, rv / 0.2);
      } else {
        ax = xR - 60 + Math.sin((rv - 0.2) * Math.PI * 6) * 6;
        trapped = true;
        latchDepth = ease(clamp((rv - 0.2) / 0.3));
      }
      forward = false;
    } else {
      // 리셋 : 스냅 백해서 왼쪽 시작으로
      const rr = (u - 0.85) / 0.15;
      ax = lerp(xR - 60, xL, ease(rr));
      forward = true;
    }

    /* ── 실선의 세로 위치 ─────────────────────────
       걸림 상태에서 실선은 화살표에 걸린 지점(pinX)을 꼭짓점으로 위로 당겨진다.
       아래 quadratic 은 제어점이 중점이라 실제로는 곧은 두 도막이다 — 그래서 이 식이
       실선 위 임의의 x 에서의 정확한 세로 위치다. 검사 신호가 이 위를 그대로 탄다. */
    const pinX = ax;
    const pull = trapped ? 22 * latchDepth : 0;
    const wireY = x => {
      if (!trapped) return cy;
      const xc = clamp(x, xL, xR);
      return xc <= pinX
        ? cy - pull * (xc - xL) / Math.max(1, pinX - xL)
        : cy - pull * (xR - xc) / Math.max(1, xR - pinX);
    };

    /* ── 실선의 재질 : 훑고 지나가는 검사 신호 ─────
       계속 도는 것 ②. 실선과 같은 색으로, 실선 밑에 깔아 굵기가 부푸는 것처럼 보이게 한다
       (실선의 또렷한 심은 위에 남는다). 별개 물체가 아니라 실선 자신의 상태다.
       오른쪽 끝(커밋 도장)에 닿기 전에 꺼진다 — 도착은 화살표의 몫이다. */
    ctx.lineCap = 'round';
    ctx.strokeStyle = trapped ? tone('accent') : tone('ink');
    setShadow(ctx, trapped ? GLOW : 'rgba(255,255,255,0.20)', 8, 0);
    for (let i = 0; i < NSWEEP; i++) {
      const su = frac(t / SWEEP + i / NSWEEP);   // 왕복 위상 u 와 다른 축이다
      const hx = lerp(xL, xR, su);
      const env = clamp(su / 0.14) * clamp((1 - su) / 0.22);
      if (env < 0.02) continue;
      /* 굵기 13 : 실선(5)의 위아래로 4px 씩 부푼다. 이 부푼 띠가 0.2초마다 통째로
         자리를 옮기는 잉크다(신호 하나당 약 200px² — 화살촉 84px² 의 두 배 넘는다). */
      ctx.globalAlpha = env;
      ctx.lineWidth = 13;
      strokeAlong(ctx, hx - 16, hx, wireY, xL, xR);       // 머리
      ctx.globalAlpha = env * 0.55;
      ctx.lineWidth = 8;
      strokeAlong(ctx, hx - 38, hx - 15, wireY, xL, xR);  // 꼬리
    }
    ctx.globalAlpha = 1;
    clearShadow(ctx);
    ctx.lineCap = 'butt';

    /* ── 실선 : 걸림 상태이면 팽팽해진다 ─────────
       실선은 두 앵커 사이의 곡선. 정상이면 직선, 걸리면 위로 살짝 휘어 잡아채는 느낌. */
    ctx.lineWidth = 5;
    setShadow(ctx, trapped ? GLOW : 'rgba(255,255,255,0.20)', trapped ? 14 : 8, 0);
    ctx.strokeStyle = trapped ? tone('accent') : tone('ink');
    ctx.beginPath();
    if (trapped) {
      // 실선을 두 부분으로 나눠서 화살표에 걸린 지점을 위로 당긴다
      ctx.moveTo(xL, cy);
      ctx.quadraticCurveTo((xL + pinX) / 2, cy - pull * 0.5, pinX, cy - pull);
      ctx.quadraticCurveTo((pinX + xR) / 2, cy - pull * 0.5, xR, cy);
    } else {
      ctx.moveTo(xL, cy); ctx.lineTo(xR, cy);
    }
    ctx.stroke();
    clearShadow(ctx);

    /* ── 자리 넷 : 하나만 이름이 붙는다 ──────────── */
    if (sk > 0.02) {
      ctx.globalAlpha = clamp(sk * 1.4);
      for (let i = 0; i < nTest; i++) {
        const x = slotX(i);
        if (i === namedAt) {
          // 이름 붙은 하나 — 위의 라벨과 선으로 이어진다
          ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
          ctx.beginPath(); ctx.moveTo(x, 50); ctx.lineTo(x, cy - 12); ctx.stroke();
          setShadow(ctx, GLOW, 12, 0);
          ctx.fillStyle = tone('accent');
          ctx.beginPath(); ctx.arc(x, cy, 9, 0, Math.PI * 2); ctx.fill();
          clearShadow(ctx);
        } else {
          // 원문에 이름이 없는 자리 — 만들지 않고 익명으로 둔다
          ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
          ctx.beginPath(); ctx.arc(x, cy, 6, 0, Math.PI * 2); ctx.stroke();
          ctx.textAlign = 'center';
          ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
          ctx.fillText(spec.anon || '···', x, cy + 28);
        }
      }
      ctx.globalAlpha = 1;
    }

    // 화살표
    const arrY = trapped ? cy - 22 * latchDepth : cy;
    ctx.fillStyle = forward ? tone('ink') : tone('accent');
    setShadow(ctx, forward ? 'rgba(255,255,255,0.25)' : GLOW, 10, 0);
    ctx.beginPath();
    if (forward) {
      // 오른쪽 화살촉
      ctx.moveTo(ax + 10, arrY);
      ctx.lineTo(ax - 4, arrY - 6);
      ctx.lineTo(ax - 4, arrY + 6);
    } else {
      // 왼쪽 화살촉
      ctx.moveTo(ax - 10, arrY);
      ctx.lineTo(ax + 4, arrY - 6);
      ctx.lineTo(ax + 4, arrY + 6);
    }
    ctx.closePath(); ctx.fill();
    clearShadow(ctx);

    /* ── 걸림 표식 : trapCue 로 확정 ────────────
       "커밋 전에 잡힘" — 실선에 붙는 라벨. */
    if (tk > 0.02 && trapped) {
      const tx = ax;
      const ty = cy - 22 * latchDepth - 14;
      const label = spec.trapLabel || '커밋 전에 잡힘';
      ctx.font = disp(800, 11);
      const tw = ctx.measureText(label).width + 14;
      ctx.globalAlpha = clamp(tk * 1.4);
      ctx.fillStyle = tone('accent');
      setShadow(ctx, GLOW, 10, 0);
      roundRect(ctx, tx - tw / 2, ty - 14, tw, 20, 3); ctx.fill();
      clearShadow(ctx);
      ctx.textAlign = 'center';
      ctx.fillStyle = '#000'; ctx.font = disp(800, 11);
      ctx.fillText(label, tx, ty + 1);
      ctx.globalAlpha = 1;
    }

    // 하단 : 정합 라벨 (okCue 로 밝아짐)
    if (ok > 0.02) {
      ctx.globalAlpha = clamp(ok);
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
      ctx.fillText(spec.okLabel || '정합', w / 2, cy + 50);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
