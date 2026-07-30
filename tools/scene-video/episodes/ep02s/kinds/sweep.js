import {
  disp, mono, ease, clamp, lerp, frac, rnd,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* sweep — 뒤집힘이 파일들 사이로 흘러간다.

   가로줄 34개(총 파일 수)가 격자로 배치된다. sweepCue 에서 왼쪽 상단에서 오른쪽 하단으로
   지나가는 앞머리가 있고, 지나간 자리의 줄은 흰색으로 채워진다(뒤집혔다는 뜻). 네 지점에서
   지나가며 표식이 남는다 — 30→70(초기값), LessEq→GreaterEq(연산자), <20(임계값), ×15(에셋).
   ep01s erasure(지우는 획)와 방향은 같지만 지운다가 아니라 '바꾼다'다.

   회차의 기본 도형 안에서 이 그림의 몫은 '뒤집힘이 파일들 사이로 흘러간다'.
   계속 도는 것 = 지나가는 앞머리 뒤로 흘러내리는 잔줄. */

const TRAIL = 1.7;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const total = spec.total || 34;
    const beats = spec.beats || [];

    const sk = ease(cue(spec.sweepCue ?? 1, 0.15, 0.75));
    const bk = ease(cue(spec.beatCue ?? 2, 0.15, 0.65));

    ctx.textBaseline = 'alphabetic';

    // 격자 : 34개를 6열 x 6행(2 남음) — 대략 6x6 배치
    const cols = 6;
    const rows = Math.ceil(total / cols);
    const gridTop = 46;
    const gridBot = h - 100;
    const cellW = (w - 40) / cols;
    const cellH = (gridBot - gridTop) / rows;

    // 카운트 라벨 (상단 좌측)
    ctx.textAlign = 'left';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
    ctx.fillText(spec.totalLabel || '', 20, 24);
    ctx.textAlign = 'right';
    ctx.fillStyle = tone('ink'); ctx.font = disp(900, 22);
    ctx.fillText(String(total), w - 20, 26);

    // 앞머리 진행 : 0 → total 인덱스
    const headIdx = sk * total;

    for (let i = 0; i < total; i++) {
      const r = Math.floor(i / cols);
      const c = i % cols;
      const x = 20 + c * cellW;
      const y = gridTop + r * cellH;
      const bw = cellW - 6;
      const bh = cellH - 6;

      // 앞머리와의 거리로 상태 결정
      const dist = headIdx - i;
      let state = 'off';   // 아직 안 지남
      if (dist > 1.5) state = 'flipped';
      else if (dist > 0) state = 'flipping';

      if (state === 'off') {
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        roundRect(ctx, x, y, bw, bh, 3); ctx.stroke();
      } else if (state === 'flipping') {
        // 앞머리 근처 : 채워지는 중
        const p = clamp(dist);
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        setShadow(ctx, GLOW, 8, 0);
        roundRect(ctx, x, y, bw, bh, 3); ctx.stroke();
        clearShadow(ctx);
        // 안쪽 채움
        ctx.fillStyle = tone('accent');
        ctx.globalAlpha = 0.6 * p;
        roundRect(ctx, x + 2, y + 2, (bw - 4) * p, bh - 4, 2); ctx.fill();
        ctx.globalAlpha = 1;
      } else {
        // 지나갔다 : 흰색 채움
        ctx.fillStyle = tone('ink');
        roundRect(ctx, x, y, bw, bh, 3); ctx.fill();
      }
    }

    /* ── 네 표식(beats) : sweep 진행에 따라 나타난다 ── */
    beats.forEach((b, idx) => {
      if (sk < b.at - 0.02) return;   // 아직 도달 안 함
      const localK = clamp((sk - b.at) / 0.15);   // 페이드인
      const boxK = clamp((bk - idx * 0.15));      // beatCue 에서 강조 밑줄

      const targetIdx = Math.floor(b.at * total);
      const r = Math.floor(targetIdx / cols);
      const c = targetIdx % cols;

      // 표식은 격자 옆에 놓지 않고, 아래쪽 라벨 스트립에 순서대로 배치
      const stripY = gridBot + 14;
      const sx = 20 + idx * ((w - 40) / beats.length);
      const sw = (w - 40) / beats.length - 6;

      ctx.globalAlpha = clamp(localK * 1.4);
      // 표식 상자
      ctx.lineWidth = 3;
      ctx.strokeStyle = boxK > 0.4 ? tone('accent') : tone('sub');
      if (boxK > 0.4) setShadow(ctx, GLOW, 10, 0);
      roundRect(ctx, sx, stripY, sw, 44, 3); ctx.stroke();
      clearShadow(ctx);

      ctx.textAlign = 'center';
      /* before → after
         🔴 mono 는 ASCII 로그·코드에만 쓴다(lib.js 폰트 주석). 반전 이전 임계값처럼
         원문에 값이 없어 익명(···)으로 두는 자리는 ASCII 가 아니므로 disp 로 찍는다 —
         SceneMono 에 없는 글자를 mono 로 지정하면 머신마다 다른 폴백으로 갈린다. */
      const bt = String(b.before);
      const asciiB = /^[\x20-\x7E]*$/.test(bt);
      const mkB = s => asciiB ? mono(700, s) : disp(700, s);
      ctx.fillStyle = tone('sub'); ctx.font = mkB(10);
      let bs = 10;
      while (bs > 7 && ctx.measureText(bt).width > sw - 6) { bs -= 0.5; ctx.font = mkB(bs); }
      ctx.fillText(bt, sx + sw / 2, stripY + 14);
      // 화살표
      ctx.fillStyle = tone('accent'); ctx.font = disp(900, 11);
      ctx.fillText('→', sx + sw / 2, stripY + 26);
      // after — 같은 이유로 ASCII 가 아니면 disp('×15 뒤집힘' 처럼 한글이 섞인 자리)
      const at = String(b.after);
      const asciiA = /^[\x20-\x7E]*$/.test(at);
      const mkA = s => asciiA ? mono(700, s) : disp(700, s);
      ctx.fillStyle = tone('ink'); ctx.font = mkA(10);
      let as = 10;
      while (as > 7 && ctx.measureText(at).width > sw - 6) { as -= 0.5; ctx.font = mkA(as); }
      ctx.fillText(at, sx + sw / 2, stripY + 38);
      // note (더 아래)
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 9);
      let ns = 9;
      const nt = String(b.note);
      while (ns > 7 && ctx.measureText(nt).width > sw + 6) { ns -= 0.5; ctx.font = disp(700, ns); }
      ctx.fillText(nt, sx + sw / 2, stripY + 52);
      ctx.globalAlpha = 1;
    });

    /* ── 계속 도는 것 : 격자를 흐르는 얇은 잔줄 ─────
       앞머리가 지나간 자리에서 오른쪽 아래로 흘러내리는 잔줄. sk 가 없어도 도는 것 아님 —
       sk 가 없으면 앞머리도 없으므로 잔줄도 없다(정적 상태를 피하려면 sk 가 있어야 한다).
       그래서 t 로 앞머리 근처 잔줄이 흐르게 두고, sk 가 아직 낮을 때는 상단 라벨 옆의
       조그만 흐름으로 보완한다. */
    if (sk > 0.05) {
      const hi = clamp(headIdx / total);
      const hx = 20 + ((headIdx % cols) * cellW) + cellW / 2;
      const hy = gridTop + Math.floor(headIdx / cols) * cellH + cellH / 2;
      for (let i = 0; i < 10; i++) {
        const u = frac(t / TRAIL + i / 10);
        const dx = lerp(hx, hx + 20, u);
        const dy = lerp(hy, hy + 30, u);
        ctx.globalAlpha = 0.5 * (1 - u);
        ctx.fillStyle = tone('accent');
        ctx.fillRect(dx, dy, 3, 3);
      }
      ctx.globalAlpha = 1;
    } else {
      // 좌상단 힌트 : 곧 시작한다는 조그만 표식
      const u = frac(t / TRAIL);
      const cx = 20 + Math.sin(u * Math.PI * 2) * 4;
      ctx.fillStyle = tone('accent');
      ctx.globalAlpha = 0.55;
      ctx.beginPath(); ctx.arc(cx + 4, gridTop - 8, 3, 0, Math.PI * 2); ctx.fill();
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
