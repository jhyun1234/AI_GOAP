import {
  disp, mono, ease, clamp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* atcopy — 배율이 붙는 자리는 잡 안이 아니라 잡으로 넘어가기 직전이다.

   이 편에서 가장 오해하기 쉬운 문장이 원문 2단계다. "플래너를 안 건드렸다"는 말이
   "코드를 안 썼다"로 읽히기 쉬운데 그게 아니다 — 안 건드린 것은 A* 잡이고, 배율을 곱하는
   코드는 새로 썼다. 그래서 이 그림은 파이프라인을 옆에서 잘라 보여준다:
   왼쪽에 액션 데이터, 오른쪽에 잡, 그 사이 한 점에 「복사」가 있다.

   jobCue 에서 잡 상자에 「전혀 건드리지 않음」이 붙고 테두리가 굵어진다. 잡은 이 샷이
   끝날 때까지 그 상태 그대로다 — 안에서 탐색 표식이 계속 도는 것이 그 증거다(멈춘 게 아니라
   손을 안 댔을 뿐이다). multCue 에서 복사 지점이 흰색에서 강조색으로 넘어가고 아래로
   「× 성격 배율」이 걸린다.

   🔴 색이 갈리는 자리가 곧 논지다. 안 건드린 기계는 흰색, 새로 손댄 한 점만 강조색이다.
   그래서 흰색과 강조색을 같은 픽셀 위에서 섞지 않는다 — 노드의 색 전환은 알파 구간을
   겹치지 않게 나눴다(흰색은 mk 0.45 에서 사라지고 강조색은 0.55 부터 뜬다). 반투명으로
   겹치면 색상 120도 근처의 다른 초록이 만들어져 3색 검사에 걸린다.

   계속 도는 것 = 액션 데이터에서 잡으로 흘러 들어가는 표식 넷, 잡 안을 도는 탐색 표식. */

const FLOW = 2.6;    // 데이터 표식이 카드에서 잡까지 가는 시간(초)
const SCAN = 3.6;    // 잡 안 탐색 표식 한 바퀴(초)

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

    const jk = ease(cue(spec.jobCue ?? 0, 0.15, 0.7));
    const mk = ease(cue(spec.multCue ?? 1, 0.15, 0.75));

    const DX = 12, DW = 92, DY = 110, DH = 72;      // 액션 데이터 카드
    const JX = 206, JW = 134, JY = 92, JH = 108;    // 플래너 잡 상자
    const NX = 140, NY = 146;                        // 복사 지점
    const LANE = 146, BY = 226;

    /* ── 상단 라벨 ─────────────────────────────────── */
    if (spec.topLabel) {
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.topLabel, 8, 26);
    }

    /* ── 플래너 잡 — 이 샷 내내 안 바뀐다 ───────────── */
    if (spec.jobLabel) {
      ctx.textAlign = 'center';
      const fs = fit(spec.jobLabel, 800, 13, JW, 9);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(spec.jobLabel, JX + JW / 2, JY - 12);
    }

    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3 + 2 * jk;
    roundRect(ctx, JX, JY, JW, JH, 6); ctx.stroke();

    // 안쪽 격자 — A* 가 훑는 판
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    for (let k = 1; k <= 3; k++) {
      const gx = JX + (JW / 4) * k;
      ctx.beginPath(); ctx.moveTo(gx, JY + 8); ctx.lineTo(gx, JY + JH - 8); ctx.stroke();
    }
    for (let k = 1; k <= 2; k++) {
      const gy = JY + (JH / 3) * k;
      ctx.beginPath(); ctx.moveTo(JX + 8, gy); ctx.lineTo(JX + JW - 8, gy); ctx.stroke();
    }

    // 계속 도는 것 ① — 탐색 표식이 판 위를 돈다
    {
      const px0 = JX + 18, py0 = JY + 18, pw = JW - 36, ph = JH - 36;
      const per = 2 * (pw + ph);
      const d = frac(t / SCAN) * per;
      let sx, sy;
      if (d < pw) { sx = px0 + d; sy = py0; }
      else if (d < pw + ph) { sx = px0 + pw; sy = py0 + (d - pw); }
      else if (d < 2 * pw + ph) { sx = px0 + pw - (d - pw - ph); sy = py0 + ph; }
      else { sx = px0; sy = py0 + ph - (d - 2 * pw - ph); }
      ctx.globalAlpha = 0.85;
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(sx, sy, 6, 0, Math.PI * 2); ctx.fill();
      ctx.globalAlpha = 1;
    }

    if (spec.jobInner) {
      ctx.textAlign = 'center';
      ctx.font = mono(800, 24); ctx.fillStyle = tone('ink');
      ctx.fillText(spec.jobInner, JX + JW / 2, JY + JH / 2 + 8);
    }

    /* ── 액션 데이터 카드 ──────────────────────────── */
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, DX, DY, DW, DH, 5); ctx.stroke();
    if (spec.dataLabel) {
      ctx.textAlign = 'center';
      const fs = fit(spec.dataLabel, 900, 14, DW - 14, 9);
      ctx.font = disp(900, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(spec.dataLabel, DX + DW / 2, DY + DH / 2 + 5);
    }
    if (spec.dataSub) {
      ctx.textAlign = 'center';
      const fs = fit(spec.dataSub, 800, 11, DW + 20, 8);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.dataSub, DX + DW / 2, DY + DH + 20);
    }

    /* ── 통로 ──────────────────────────────────────── */
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(DX + DW, LANE); ctx.lineTo(JX, LANE); ctx.stroke();

    /* 계속 도는 것 ② — 데이터가 흘러 들어간다.
       복사 지점(NX)을 지난 표식만 강조 점을 얹는다. 시각이 아니라 **자리**로 갈리므로
       "몇 초에 무엇이 나타난다"는 조건이 아니다 — 배율이 붙었다는 사실 자체는 mk 가 정한다. */
    for (let k = 0; k < 4; k++) {
      const p = frac(t / FLOW + k * 0.25);
      const fx = DX + DW + p * (JX - DX - DW);
      ctx.fillStyle = tone('ink');
      roundRect(ctx, fx - 5, LANE - 5, 10, 10, 2); ctx.fill();
      if (fx > NX && mk > 0.05) {
        ctx.globalAlpha = mk;
        ctx.fillStyle = tone('accent');
        ctx.beginPath(); ctx.arc(fx, LANE - 12, 3.5, 0, Math.PI * 2); ctx.fill();
        ctx.globalAlpha = 1;
      }
    }

    /* ── 복사 지점 — 흰색에서 강조색으로 넘어간다 ───── */
    if (spec.copyLabel) {
      ctx.textAlign = 'center';
      ctx.font = disp(800, 12); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.copyLabel, NX, NY - 24);
    }
    {
      const inkA = clamp(1 - mk / 0.45);
      const accA = clamp((mk - 0.55) / 0.45);
      const r = 10 + 3 * ease(clamp(mk / 0.72));
      if (inkA > 0.02) {
        ctx.globalAlpha = inkA;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.arc(NX, NY, 10, 0, Math.PI * 2); ctx.stroke();
        ctx.globalAlpha = 1;
      }
      if (accA > 0.02) {
        ctx.globalAlpha = accA;
        setShadow(ctx, GLOW, 12, 0);
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
        ctx.beginPath(); ctx.arc(NX, NY, r, 0, Math.PI * 2); ctx.stroke();
        clearShadow(ctx);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 아래 두 배지 ──────────────────────────────── */
    const badge = (x, bw, txt, col, k, glow) => {
      if (k <= 0.02 || !txt) return;
      const sp = Math.min(1, spring(k));
      ctx.globalAlpha = k;
      ctx.lineWidth = 3; ctx.strokeStyle = col;
      if (glow) setShadow(ctx, GLOW, 10, 0);
      roundRect(ctx, x + (bw - bw * sp) / 2, BY + (32 - 32 * sp) / 2, bw * sp, 32 * sp, 5);
      ctx.stroke();
      if (glow) clearShadow(ctx);
      ctx.textAlign = 'center';
      const fs = fit(txt, 800, 13, bw - 18, 8.5);
      ctx.font = disp(800, fs * sp); ctx.fillStyle = col;
      ctx.fillText(txt, x + bw / 2, BY + 21);
      ctx.globalAlpha = 1;
    };

    // 잡 → 「전혀 건드리지 않음」
    if (jk > 0.15) {
      ctx.globalAlpha = clamp((jk - 0.15) / 0.3);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(JX + JW / 2, JY + JH); ctx.lineTo(JX + JW / 2, BY);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }
    badge(JX, JW, spec.untouched, tone('ink'), clamp((jk - 0.2) / 0.5), false);

    // 복사 지점 → 「× 성격 배율」
    if (mk > 0.35) {
      ctx.globalAlpha = clamp((mk - 0.35) / 0.3);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(NX, NY + 14); ctx.lineTo(NX, BY); ctx.stroke();
      ctx.globalAlpha = 1;
    }
    badge(NX - 56, 112, spec.mult, tone('accent'), clamp((mk - 0.4) / 0.32), true);

    ctx.textAlign = 'left';
  }
};
