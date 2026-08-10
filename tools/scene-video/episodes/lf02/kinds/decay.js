import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* decay — 같은 출발점에서 갈라지는 두 감쇠 곡선.
   세로선 하나가 겨울이 끝나는 자리(4일)이고, 20%/일 곡선만 그 선을 한참 넘어서까지
   살아 있다. 「13일은 사실상 영구 페널티」를 문장이 아니라 선 하나로 말한다.
   찍는 값은 원문에 있는 것만이다 — 5일 뒤 32(20%/일) · 겨울 4일 · 0까지 13일 ·
   막혔던 문제 8건 중 셋.
   곡선 자체는 0.8^d · 0.5^d 로 그린다(원문이 곱셈이라고 밝힌 그 계산이다).

   🔴 2차 개정 (검수 R1): ①`DAY 5` 가 큰 `32` 와 마커 원 위에 겹쳐 있었다 → **`32` 위로
   올려** 곡선 밖에 뒀다. ②오른쪽 아래 `DAY 13` 이 x축 라벨 `DAY` 와 겹쳐 `DAY 13AY` 로
   읽혔다(증거 `build/stills/450s.png`) → **x축 라벨을 없앴다.** 단위는 두 눈금 라벨
   (`DAY 5`·`DAY 13`)이 이미 지고 있어 잃는 정보가 없다.

   🔴 2차 개정 (검수 C6): 연속 모션이 반지름 4px 작도점 둘뿐이라 문턱 아래였다 →
   **곡선을 훑는 44px 폭 작도 띠**로 바꿨다(축·곡선보다 먼저 그린다). */

const DMAX = 14;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    const kWrote = ease(cue(1));   // 5일 뒤 사라진다고 적었는데
    const kReal = ease(cue(2));    // 32가 남고 13일
    const kWin = ease(cue(3));     // 겨울이 4일인데
    const kFix = ease(cue(4));     // 50%로 정정
    const kTally = ease(cue(5));   // 막혔던 문제 8건 중 셋

    const px = M + 40, py = 44, pw = w - M - 24 - px, ph = h - py - 52;
    const X = d => px + (d / DMAX) * pw;
    const Y = v => py + ph - (v / 100) * ph;

    // 연속 모션 : 곡선을 훑는 작도 띠 (축·곡선보다 먼저)
    const PR = 3.6;
    const headD = ((t % PR) / PR) * DMAX;
    {
      ctx.fillStyle = tone('track');
      ctx.fillRect(X(headD) - 22, py, 44, ph);
    }

    // 축
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(px, py); ctx.lineTo(px, py + ph); ctx.lineTo(px + pw, py + ph);
    ctx.stroke();
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('100', M, Y(100) + 4);
    ctx.fillText('0', M + 22, Y(0) + 4);

    // 겨울 끝 세로선
    if (kWin > 0.03) {
      ctx.save(); ctx.globalAlpha = clamp(kWin);
      ctx.setLineDash([8, 6]);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(X(4), py - 6); ctx.lineTo(X(4), py + ph); ctx.stroke();
      ctx.setLineDash([]);
      ctx.font = disp(700, 10); ctx.fillStyle = tone('ink');
      ctx.fillText('겨울 끝 · 4일', X(4) + 6, py + 4);
      ctx.restore();
    }

    // 곡선 두 개
    const drawCurve = (rate, alpha, color, upTo) => {
      if (alpha <= 0.02) return;
      ctx.save(); ctx.globalAlpha = clamp(alpha);
      ctx.strokeStyle = color; ctx.lineWidth = 3;
      ctx.beginPath();
      for (let i = 0; i <= 140; i++) {
        const d = (i / 140) * DMAX;
        if (d > upTo) break;
        const v = 100 * Math.pow(rate, d);
        if (i === 0) ctx.moveTo(X(d), Y(v)); else ctx.lineTo(X(d), Y(v));
      }
      ctx.stroke();
      ctx.restore();
    };

    const grown = ease(clamp(kWrote + kReal));
    drawCurve(0.8, Math.max(0.25, kWrote), tone('ink'), lerp(4, DMAX, grown));
    drawCurve(0.5, kFix, tone('accent'), DMAX);

    // 곡선 라벨 (v2: 한국어. `%` 는 기호라 그대로 둔다)
    ctx.font = disp(700, 10);
    if (kWrote > 0.05) {
      ctx.save(); ctx.globalAlpha = clamp(kWrote);
      ctx.fillStyle = tone('ink');
      const s20 = '하루 -20%';
      const sw20 = ctx.measureText(s20).width;
      ctx.fillText(s20, Math.min(X(9), w - M - sw20), Y(100 * Math.pow(0.8, 9)) - 12);
      ctx.restore();
    }
    if (kFix > 0.05) {
      ctx.save(); ctx.globalAlpha = clamp(kFix);
      ctx.fillStyle = tone('accent');
      /* 🔴 3차 개정 (검수 N6): x 를 `X(2.6)`(= 159) 로 못 박아 뒀는데 이 문자열의
         폭이 61px 이라 오른쪽 끝이 `X(4)`(= 214, WINTER 4 점선)를 관통했다
         (증거 `build/stills/462s.png` — `-50% / DA|Y`). → **점선 왼쪽으로 물린다.**
         폭을 재서 밀기 때문에 폰트·문자열이 바뀌어도 다시 뚫리지 않는다. */
      const s = '하루 -50%';
      const sw = ctx.measureText(s).width;
      ctx.fillText(s, Math.min(X(2.6), X(4) - 10 - sw), Y(100 * Math.pow(0.5, 2.6)) - 12);
      ctx.restore();
    }

    // 5일 뒤 32 — 라벨을 값 위로 올려 곡선·마커 밖에 둔다
    if (kReal > 0.05) {
      ctx.save(); ctx.globalAlpha = clamp(kReal);
      const cx = X(5), cy = Y(32);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(cx, cy, 7, 0, Math.PI * 2); ctx.stroke();
      ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText('5일째', cx + 13, cy - 32);
      ctx.font = disp(900, 19); ctx.fillStyle = tone('ink');
      ctx.fillText('32', cx + 13, cy - 10);
      ctx.restore();
    }

    // 13일 자리 — x축 라벨을 없앴으므로 이 눈금만 남는다
    if (kReal > 0.4) {
      ctx.save(); ctx.globalAlpha = clamp((kReal - 0.4) / 0.5);
      const cx = X(13);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(cx, py + ph); ctx.lineTo(cx, py + ph - 16); ctx.stroke();
      ctx.font = disp(700, 10); ctx.fillStyle = tone('ink');
      const s = '13일째';
      const sw = ctx.measureText(s).width;
      ctx.fillText(s, Math.min(cx - sw / 2, w - M - sw), py + ph + 18);
      ctx.restore();
    }

    // 판정
    if (kWin > 0.4) {
      ctx.save(); ctx.globalAlpha = clamp((kWin - 0.4) / 0.5);
      ctx.font = disp(900, 15); ctx.fillStyle = tone('ink');
      const s = '사실상 영구 페널티';
      const sw = ctx.measureText(s).width;
      ctx.fillText(s, Math.min(X(6), w - M - sw), py + 22);
      ctx.restore();
    }

    /* 막혔던 문제 8건 중 셋 — 여덟 칸 중 셋이 AI 칸(강조)으로 찬다.
       차트 오른쪽 위는 곡선이 지나가지 않는 빈 자리다(0.8^d 는 x>4 에서 아래로 내려간다). */
    if (kTally > 0.03) {
      ctx.save(); ctx.globalAlpha = clamp(kTally);
      const sq = 13, gp = 4, n = 8;
      const tw = n * sq + (n - 1) * gp;
      const tx = w - M - tw, ty = py + 52;
      ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
      const lab = '막혔던 문제 8 · AI 3';
      const lw = ctx.measureText(lab).width;
      ctx.fillText(lab, w - M - lw, ty - 8);
      for (let i = 0; i < n; i++) {
        const k = clamp((kTally - i * 0.05) / 0.45);
        const mine = i < 3;
        ctx.strokeStyle = mine ? tone('accent') : tone('track');
        ctx.lineWidth = 3;
        ctx.strokeRect(tx + i * (sq + gp), ty, sq, sq + 4);
        if (mine && k > 0.1) {
          ctx.fillStyle = tone('accent');
          ctx.fillRect(tx + i * (sq + gp) + 4, ty + 4, sq - 8, (sq - 4) * ease(k));
        }
      }
      ctx.restore();
    }
  },
};
