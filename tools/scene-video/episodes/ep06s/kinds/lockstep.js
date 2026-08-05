import {
  disp, ease, clamp, frac,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* lockstep — 다섯 줄이 한 줄처럼 겹쳐 흐른다.

   이 편의 0초는 "다섯 명이 한 사람처럼 움직였다"이다. 그걸 그리는 가장 정직한 방법은
   **다섯 개의 하루 트랙을 나란히 세우고, 그 위의 표식 다섯을 정확히 같은 x 에 두는 것**이다.
   다섯이 조금씩 어긋나면 그건 개인이 있는 마을이고, 한 픽셀도 안 어긋나면 개인이 없는 마을이다.
   표식들을 세로로 꿰는 옅은 연결선이 그 사실을 계속 말한다 — 이 선이 기울어질 일이 없다.

   lineCue 에서 트랙 다섯이 위에서부터 깔리고, bandCue 에서 세로 띠 셋(배고픔·쉼·일)이
   왼쪽부터 차례로 켜지면서 다섯 트랙을 통째로 관통한다. 띠가 켜질 때 각 트랙 위에 고리가
   하나씩 앉는데, 다섯 고리가 한 세로선 위에 있는 것이 "동시에"의 그림이다.

   🔴 등장은 전부 cue 에 물렸다. t 로 도는 것은 표식 다섯과 그 연결선, 트랙의 점선 흐름뿐이고
   이것들은 문턱을 가지지 않는다(무언가가 t 시각에 "나타나는" 조건이 하나도 없다).

   🔴 다른 회차의 트랙·게이지 그림과 다른 점: 여기서 값은 오르내리지 않는다. 모든 트랙이
   같은 높이의 같은 직선이고, 사건은 "다섯이 어긋나지 않는다"는 것 하나뿐이다. 막대도 눈금도
   숫자도 없다 — 원문이 배고픔·피로의 수치를 하나도 주지 않았기 때문이기도 하다. */

const CYCLE = 5.6;                 // 표식 한 바퀴(초)
const BANDS = [0.24, 0.54, 0.82];  // 트랙 위 세 순간의 자리

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const fit = (txt, weight, start, max, min = 8) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const N = spec.people ?? 5;
    const X0 = 46, X1 = w - 24, SPAN = X1 - X0;
    const rowY = i => 68 + i * 34;
    const TOP = rowY(0), BOT = rowY(N - 1);

    const lk = ease(cue(spec.lineCue ?? 0, 0.15, 0.55));
    const bk = ease(cue(spec.bandCue ?? 1, 0.15, 0.85));

    /* ── 상단 라벨 ─────────────────────────────────── */
    if (spec.testLabel) {
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.testLabel, 8, 28);
    }

    /* ── 트랙 다섯 ─────────────────────────────────── */
    const alive = i => clamp(lk * 5.6 - i * 0.9);

    for (let i = 0; i < N; i++) {
      const a = alive(i);
      if (a <= 0.02) continue;
      const y = rowY(i);

      // 트랙 — 점선이 계속 흐른다(t)
      ctx.globalAlpha = a;
      ctx.setLineDash([11, 8]);
      ctx.lineDashOffset = -(t * 11) % 19;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(X0, y); ctx.lineTo(X1, y); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;

      // 왼쪽 주민 표식
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(28, y, 5.5, 0, Math.PI * 2); ctx.fill();
      ctx.globalAlpha = 1;
    }

    /* ── 세로 띠 셋 — 같은 순간 ─────────────────────── */
    for (let j = 0; j < BANDS.length; j++) {
      const bj = clamp(bk * 3.5 - j);
      if (bj <= 0.02) continue;
      const bx = X0 + BANDS[j] * SPAN;

      ctx.globalAlpha = bj * 0.9;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.moveTo(bx, TOP - 16); ctx.lineTo(bx, BOT + 16);
      ctx.stroke();
      ctx.globalAlpha = 1;

      // 트랙마다 그 순간의 고리 — 다섯이 한 세로선 위에 있다
      for (let i = 0; i < N; i++) {
        const a = Math.min(alive(i), bj);
        if (a <= 0.02) continue;
        ctx.globalAlpha = a;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.arc(bx, rowY(i), 6, 0, Math.PI * 2); ctx.stroke();
        ctx.globalAlpha = 1;
      }

      if (spec.bands && spec.bands[j]) {
        ctx.globalAlpha = bj;
        ctx.textAlign = 'center';
        const fs = fit(spec.bands[j], 800, 13, 76, 9);
        ctx.font = disp(800, fs); ctx.fillStyle = tone('accent');
        ctx.fillText(spec.bands[j], bx, BOT + 38);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 계속 도는 것 — 표식 다섯과 그것을 꿰는 선 ────
       다섯의 x 가 완전히 같다는 것이 이 샷의 전부다. 연결선이 언제나 수직인 것이
       "한 사람처럼"의 시각적 증거다. */
    const mx = X0 + frac(t / CYCLE) * SPAN;
    const vis = clamp(lk * 2.4);

    if (vis > 0.02) {
      ctx.globalAlpha = vis * 0.28;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(mx, TOP - 10); ctx.lineTo(mx, BOT + 10); ctx.stroke();
      ctx.globalAlpha = 1;

      for (let i = 0; i < N; i++) {
        const a = alive(i);
        if (a <= 0.02) continue;
        const y = rowY(i);

        // 지나온 자국
        ctx.globalAlpha = a * 0.42;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
        ctx.beginPath();
        ctx.moveTo(Math.max(X0, mx - 34), y); ctx.lineTo(mx, y);
        ctx.stroke();
        ctx.globalAlpha = 1;

        ctx.globalAlpha = a;
        setShadow(ctx, GLOW, 8, 0);
        ctx.fillStyle = tone('accent');
        ctx.beginPath(); ctx.arc(mx, y, 6, 0, Math.PI * 2); ctx.fill();
        clearShadow(ctx);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 아래 한 줄 ────────────────────────────────── */
    if (spec.note) {
      const k = clamp((bk - 0.7) / 0.3);
      if (k > 0.02) {
        ctx.globalAlpha = k;
        ctx.textAlign = 'center';
        const fs = fit(spec.note, 900, 14, w - 30, 10);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('ink');
        ctx.fillText(spec.note, w / 2, 276);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
