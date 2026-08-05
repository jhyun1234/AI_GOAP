import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* tilt — 비용 하나가 낮아지자 계획이 스스로 그쪽으로 기운다.

   원문 2단계의 뒷문장이 이 샷이다: "농사꾼 아키타입은 밭 수확 비용이 낮아져서 플래너가
   자연스럽게 밭으로 향하는 계획을 세웁니다." 중요한 것은 아무도 밭으로 가라고 말하지
   않았다는 점이다. 그래서 화면 아래쪽은 지시가 아니라 후보들로 시작한다 — 주민에게서
   부챗살처럼 뻗은 점선 다섯. costCue 에서 비용 막대가 짧아지고, planCue 에서 나머지 살이
   사라지며 하나만 굵어져 밭까지 이어진다. 표식이 그 길을 실제로 걸어간다.

   🔴 숫자를 한 개도 안 쓴다. 원문은 배율 값도 비용 값도 준 적이 없다 — 준 것은
   ①배율 필드가 네 계열(채집·농사·건설·탐험)이라는 것 ②농사꾼은 밭 수확 비용이 낮아진다는
   것 둘뿐이다. 그래서 나머지 세 칸은 이름만 있는 빈 칸으로 두고(값을 주장하지 않는다),
   막대는 눈금 없이 짧아지는 것만 보여준다. 눈금을 그리는 순간 그건 지어낸 수치다.

   🔴 색을 섞지 않는다. 농사 칸의 흰색→강조색 전환은 알파 구간을 겹치지 않게 나눴고,
   밭도 같은 방식이다.

   계속 도는 것 = 막대 안을 흐르는 토막들, 후보 점선의 흐름, 밭 이삭의 흔들림,
   도착 뒤 밭 테두리의 맥동. */

const BAR_X = 96, BAR_R = 336, BAR_Y = 100, BAR_H = 18;
const SEG = 14, GAP = 8;
const PX = 56, PY = 250;                      // 주민
const FX = 246, FY = 156, FW = 86, FH = 50;   // 밭

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

    const ck = ease(cue(spec.costCue ?? 0, 0.15, 0.8));
    const pk = ease(cue(spec.planCue ?? 1, 0.15, 0.8));
    const ti = spec.tiltIndex ?? 1;

    /* ── 아키타입 칩 ───────────────────────────────── */
    if (spec.archetype) {
      ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
      setShadow(ctx, GLOW, 9, 0);
      roundRect(ctx, 8, 12, 84, 28, 5); ctx.stroke();
      clearShadow(ctx);
      ctx.textAlign = 'center';
      const fs = fit(spec.archetype, 900, 15, 70, 10);
      ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.archetype, 50, 32);
    }
    if (spec.fieldsLabel) {
      ctx.textAlign = 'left';
      ctx.font = disp(800, 11.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.fieldsLabel, 102, 32);
    }

    /* ── 배율 필드 네 칸 ───────────────────────────── */
    const fields = spec.fields || [];
    for (let i = 0; i < fields.length; i++) {
      const x = 12 + i * 84, cw = 76;
      const on = i === ti ? ck : 0;
      const inkA = i === ti ? clamp(1 - on / 0.45) : 1;
      const accA = i === ti ? clamp((on - 0.55) / 0.45) : 0;

      if (inkA > 0.02) {
        ctx.globalAlpha = inkA;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        roundRect(ctx, x, 52, cw, 30, 4); ctx.stroke();
        ctx.textAlign = 'center';
        const fs = fit(fields[i], 800, 13, cw - 16, 9);
        ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
        ctx.fillText(fields[i], x + cw / 2, 72);
        ctx.globalAlpha = 1;
      }
      if (accA > 0.02) {
        const sp = Math.min(1, spring(accA));
        ctx.globalAlpha = accA;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
        setShadow(ctx, GLOW, 10, 0);
        roundRect(ctx, x + (cw - cw * sp) / 2, 52 + (30 - 30 * sp) / 2, cw * sp, 30 * sp, 4);
        ctx.stroke();
        clearShadow(ctx);
        ctx.textAlign = 'center';
        const label = `${spec.multMark ?? '×'} ${fields[i]}`;
        const fs = fit(label, 900, 13, cw - 12, 9);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
        ctx.fillText(label, x + cw / 2, 72);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 비용 막대 ─────────────────────────────────── */
    if (spec.costLabel) {
      ctx.textAlign = 'left';
      ctx.font = disp(800, 11.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.costLabel, 12, 114);
    }
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    roundRect(ctx, BAR_X, BAR_Y, BAR_R - BAR_X, BAR_H, 4); ctx.stroke();

    const shrink = ease(clamp((ck - 0.30) / 0.58));
    const barLen = lerp(224, 100, shrink);

    // 계속 도는 것 ① — 막대 안을 흐르는 토막
    ctx.save();
    ctx.beginPath();
    ctx.rect(BAR_X, BAR_Y, barLen, BAR_H);
    ctx.clip();
    const off = -((t * 13) % (SEG + GAP));
    ctx.fillStyle = tone('ink');
    for (let x = BAR_X + off; x < BAR_X + barLen + SEG; x += SEG + GAP) {
      roundRect(ctx, x, BAR_Y + 3, SEG, BAR_H - 6, 2); ctx.fill();
    }
    ctx.restore();

    // 막대 끝을 짚는 표식
    ctx.strokeStyle = shrink > 0.5 ? tone('accent') : tone('ink');
    ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(BAR_X + barLen, BAR_Y - 5); ctx.lineTo(BAR_X + barLen, BAR_Y + BAR_H + 5);
    ctx.stroke();

    // 농사 칸에서 막대로 내려오는 연결
    if (ck > 0.5) {
      ctx.globalAlpha = clamp((ck - 0.5) / 0.3);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(12 + ti * 84 + 38, 82); ctx.lineTo(12 + ti * 84 + 38, BAR_Y);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 밭 ────────────────────────────────────────── */
    const arr = ease(clamp((pk - 0.72) / 0.28));
    const fInk = clamp(1 - arr / 0.45), fAcc = clamp((arr - 0.55) / 0.45);
    const pulse = 1.8 * Math.sin(frac(t / 3.2) * Math.PI * 2) * arr;

    if (fInk > 0.02) {
      ctx.globalAlpha = fInk;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, FX, FY, FW, FH, 4); ctx.stroke();
      ctx.globalAlpha = 1;
    }
    if (fAcc > 0.02) {
      ctx.globalAlpha = fAcc;
      setShadow(ctx, GLOW, 14, 0);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      roundRect(ctx, FX - pulse, FY - pulse, FW + pulse * 2, FH + pulse * 2, 4); ctx.stroke();
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    // 계속 도는 것 ② — 이삭이 흔들린다
    for (let j = 0; j < 4; j++) {
      const bx = FX + 14 + j * 21;
      const sway = 3.2 * Math.sin(frac(t / 2.8) * Math.PI * 2 + j * 0.8);
      ctx.strokeStyle = fAcc > 0.5 ? tone('accent') : tone('ink');
      ctx.globalAlpha = 0.9;
      ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(bx, FY + FH - 8); ctx.lineTo(bx + sway, FY + 10);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }
    if (spec.targetLabel) {
      ctx.textAlign = 'center';
      const fs = fit(spec.targetLabel, 900, 15, FW, 10);
      ctx.font = disp(900, fs);
      ctx.fillStyle = fAcc > 0.5 ? tone('accent') : tone('ink');
      ctx.fillText(spec.targetLabel, FX + FW / 2, FY - 10);
    }

    /* ── 주민 ──────────────────────────────────────── */
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.arc(PX, PY, 10, 0, Math.PI * 2); ctx.stroke();

    /* ── 후보 살 다섯 → 하나 ───────────────────────── */
    const base = Math.atan2(FY + FH / 2 - PY, FX - 3 - PX);
    const fade = 1 - clamp(pk * 2.2);
    if (fade > 0.02) {
      ctx.globalAlpha = fade * 0.95;
      ctx.setLineDash([9, 7]);
      ctx.lineDashOffset = -(t * 14) % 16;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      for (const d of [-0.62, -0.31, 0, 0.31, 0.62]) {
        const a = base + d;
        ctx.beginPath();
        ctx.moveTo(PX + Math.cos(a) * 14, PY + Math.sin(a) * 14);
        ctx.lineTo(PX + Math.cos(a) * 96, PY + Math.sin(a) * 96);
        ctx.stroke();
      }
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;
    }

    /* ── 굳은 계획 한 줄 ───────────────────────────── */
    const qp = k => ({
      x: (1 - k) * (1 - k) * PX + 2 * (1 - k) * k * 150 + k * k * (FX - 3),
      y: (1 - k) * (1 - k) * PY + 2 * (1 - k) * k * 196 + k * k * (FY + FH / 2)
    });
    const travel = ease(clamp((pk - 0.30) / 0.55));
    if (travel > 0.01) {
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.beginPath();
      const p0 = qp(0); ctx.moveTo(p0.x, p0.y);
      const STEPS = 28;
      for (let i = 1; i <= STEPS; i++) {
        const p = qp(travel * i / STEPS);
        ctx.lineTo(p.x, p.y);
      }
      ctx.stroke();

      const head = qp(travel);
      setShadow(ctx, GLOW, 12, 0);
      ctx.fillStyle = tone('accent');
      ctx.beginPath(); ctx.arc(head.x, head.y, 7, 0, Math.PI * 2); ctx.fill();
      clearShadow(ctx);
    }

    /* ── 원문 한 줄 ────────────────────────────────── */
    if (spec.landing) {
      const k = clamp((pk - 0.66) / 0.34);
      if (k > 0.02) {
        ctx.globalAlpha = k;
        ctx.textAlign = 'center';
        const fs = fit(spec.landing, 900, 14, w - 24, 9.5);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('ink');
        ctx.fillText(spec.landing, w / 2, 292);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
