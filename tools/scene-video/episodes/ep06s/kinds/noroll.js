import {
  disp, ease, clamp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* noroll — 판정에 들어가는 입력이 셋인 줄 알았는데, 셋째 자리가 접혀 닫힌다.

   원문 3단계의 반전은 "성격마다 다르다"가 아니라 **"그런데 랜덤은 없다"** 쪽이다.
   기분 따라 거절하는 AI 는 흔하고, 그건 협상이 아니라 운이다. 원문은 정확히 반대를 골랐다 —
   "플레이어가 패턴을 학습할 수 있어야 협상이 의미를 갖기 때문입니다."

   그래서 이 그림은 위아래 두 층이다. 위층은 결과(순둥이는 지쳐도 수락, 고집쟁이는 멀쩡해도
   거부) — 피로 게이지와 판정이 어긋나 있는 것이 눈에 바로 보인다. 아래층은 그 판정에
   무엇이 들어가는가다. 입력 셋이 나란히 서 있다가 rollCue 에서 셋째(「랜덤」)가 위아래로
   접혀 사라지고 그리로 가던 선이 되감긴다.

   🔴 ✕ 표를 안 쓴다. 지운 것이 아니라 처음부터 두지 않은 것이라 취소선이 아니라
   접힘이 맞다. 접힌 자리에는 옅은 자국만 남는다.
   🔴 게이지에 눈금과 숫자가 없다. 원문은 순둥이·고집쟁이의 피로 값을 준 적이 없고,
   준 것은 "많이 지쳐도"와 "멀쩡해 보여도"라는 방향뿐이다. 그래서 채움의 많고 적음만 그린다.

   계속 도는 것 = 입력선을 타고 판정으로 들어가는 점들, 판정 상자 안을 위아래로 훑는 선. */

const BX = 112, BW = 144, BY = 176, BH = 82;
const LANE = [192, 218, 244];
const VX = 258, VW = 76;

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

    const rk = ease(cue(spec.rowCue ?? 0, 0.15, 0.7));
    const dk = ease(cue(spec.rollCue ?? 1, 0.15, 0.7));
    const fold = ease(clamp((dk - 0.25) / 0.4));
    const foldIdx = spec.foldIndex ?? -1;

    /* ── 열 머리 ───────────────────────────────────── */
    ctx.textAlign = 'left';
    ctx.font = disp(800, 11); ctx.fillStyle = tone('sub');
    if (spec.gaugeLabel) ctx.fillText(spec.gaugeLabel, 98, 32);
    if (spec.orderLabel) ctx.fillText(spec.orderLabel, VX, 32);

    /* ── 아키타입 두 줄 ────────────────────────────── */
    const rows = spec.rows || [];
    for (let i = 0; i < rows.length; i++) {
      const r = rows[i], k = clamp(rk * 2.4 - i * 0.9);
      if (k <= 0.02) continue;
      const top = 44 + i * 58;
      const accept = !!r.accept;

      ctx.globalAlpha = k;

      // 이름
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, 8, top, 80, 28, 4); ctx.stroke();
      ctx.textAlign = 'center';
      const nf = fit(r.name, 900, 14, 68, 9);
      ctx.font = disp(900, nf); ctx.fillStyle = tone('ink');
      ctx.fillText(r.name, 48, top + 20);

      // 피로 게이지 — 눈금도 숫자도 없다
      const gx = 98, gw = 148, gy = top + 8, gh = 18;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, gx, gy, gw, gh, 4); ctx.stroke();
      const fillW = gw * clamp(r.fatigue ?? 0.5) * ease(clamp(k * 1.4));
      ctx.fillStyle = tone('ink');
      roundRect(ctx, gx + 3, gy + 3, Math.max(4, fillW - 6), gh - 6, 3); ctx.fill();

      // 판정
      const col = accept ? tone('accent') : tone('ink');
      const sp = Math.min(1, spring(k));
      ctx.lineWidth = 3; ctx.strokeStyle = col;
      if (accept) setShadow(ctx, GLOW, 9, 0);
      roundRect(ctx, VX + (VW - VW * sp) / 2, top - 2 + (32 - 32 * sp) / 2, VW * sp, 32 * sp, 5);
      ctx.stroke();
      if (accept) clearShadow(ctx);
      ctx.textAlign = 'center';
      const vf = fit(r.verdict, 900, 15, VW - 16, 10);
      ctx.font = disp(900, vf); ctx.fillStyle = col;
      ctx.fillText(r.verdict, VX + VW / 2, top + 19);

      ctx.globalAlpha = 1;
    }

    /* ── 판정 상자 ─────────────────────────────────── */
    if (spec.judgeLabel) {
      ctx.textAlign = 'center';
      const fs = fit(spec.judgeLabel, 800, 12, BW + 30, 9);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.judgeLabel, BX + BW / 2, BY - 10);
    }
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, BX, BY, BW, BH, 5); ctx.stroke();

    // 계속 도는 것 ① — 판정이 계속 돌아간다
    {
      const sy = BY + 8 + frac(t / 2.4) * (BH - 16);
      ctx.globalAlpha = 0.5;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(BX + 8, sy); ctx.lineTo(BX + BW - 8, sy); ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 입력 셋 ───────────────────────────────────── */
    const inputs = spec.inputs || [];
    for (let i = 0; i < inputs.length; i++) {
      const y = LANE[i] ?? (192 + i * 26);
      const isFold = i === foldIdx;
      const live = isFold ? 1 - fold : 1;

      if (live <= 0.02) {
        // 접히고 남은 자국
        ctx.globalAlpha = 0.9;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.moveTo(8, y); ctx.lineTo(80, y); ctx.stroke();
        ctx.globalAlpha = 1;
        continue;
      }

      const hh = 24 * live;
      ctx.globalAlpha = live;
      ctx.strokeStyle = isFold ? tone('track') : tone('ink');
      ctx.lineWidth = 3;
      roundRect(ctx, 8, y - hh / 2, 72, Math.max(4, hh), 4); ctx.stroke();
      if (live > 0.45) {
        ctx.textAlign = 'center';
        const fs = fit(inputs[i], 800, 12.5, 62, 8.5);
        ctx.font = disp(800, fs);
        ctx.fillStyle = isFold ? tone('sub') : tone('ink');
        ctx.fillText(inputs[i], 44, y + 4);
      }

      // 연결선 — 접히는 쪽은 되감긴다
      const endX = 80 + (BX - 80) * live;
      ctx.strokeStyle = isFold ? tone('track') : tone('ink');
      ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(80, y); ctx.lineTo(endX, y); ctx.stroke();
      ctx.globalAlpha = 1;

      // 계속 도는 것 ② — 판정으로 들어가는 점
      if (live > 0.25) {
        for (let d = 0; d < 2; d++) {
          const p = frac(t / 1.5 + d * 0.5 + i * 0.17);
          const dx = 80 + p * (endX - 80);
          ctx.globalAlpha = live * 0.95;
          ctx.fillStyle = isFold ? tone('sub') : tone('accent');
          ctx.beginPath(); ctx.arc(dx, y, 5, 0, Math.PI * 2); ctx.fill();
          ctx.globalAlpha = 1;
        }
      }
    }

    /* ── 남는 한 줄 ────────────────────────────────── */
    if (spec.learn) {
      const k = clamp((dk - 0.55) / 0.4);
      if (k > 0.02) {
        ctx.globalAlpha = k;
        ctx.textAlign = 'center';
        const fs = fit(spec.learn, 900, 14.5, w - 20, 10);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
        ctx.fillText(spec.learn, w / 2, 288);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
