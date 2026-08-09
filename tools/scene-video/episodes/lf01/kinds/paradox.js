import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas } from '../../../engine/lib.js';

/* paradox — 이 영상의 답이 서는 자리. 가로축은 AI 의 유능함 하나뿐이고,
   그 위에서 두 곡선이 반대로 간다. 적대 AI 는 유능할수록 재미가 오르고,
   관리 대상 AI 는 유능할수록 플레이어가 할 일이 사라진다.
   유능도 표식이 축 위를 계속 왕복하며 두 곡선의 값을 동시에 짚는다 —
   같은 원인이 두 결과를 내는 그림이라 표식이 하나여야 한다.
   문구 출처: M13 글 33행. */

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;
    const L = 60, R = w - M - 104, TOP = 46, BOT = h - 46;

    const kAxis = ease(cue(0));
    const kFun = ease(cue(1));
    const kFoe = ease(cue(2));
    const kCare = ease(cue(3));
    const kEnd = ease(cue(4));

    const fun = x => BOT - (BOT - TOP) * Math.pow(x, 0.85);
    const job = x => BOT - (BOT - TOP) * Math.pow(1 - x, 0.85);

    // ── 축 ───────────────────────────────────────
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(L, TOP - 6); ctx.lineTo(L, BOT); ctx.lineTo(L + (R - L) * clamp(0.1 + 0.9 * kAxis), BOT);
    ctx.stroke();
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('AI COMPETENCE', L + 4, BOT + 16);

    const curve = (f, k, style) => {
      if (k <= 0.01) return;
      ctx.strokeStyle = style; ctx.lineWidth = 3;
      ctx.beginPath();
      const n = 48, last = Math.max(1, Math.round(n * k));
      for (let i = 0; i <= last; i++) {
        const x = i / n;
        const px = lerp(L, R, x), py = f(x);
        if (i === 0) ctx.moveTo(px, py); else ctx.lineTo(px, py);
      }
      ctx.stroke();
    };

    curve(fun, kFun, tone('ink'));
    curve(job, kCare, tone('accent'));

    // ── 라벨 ─────────────────────────────────────
    if (kFoe > 0.05) {
      ctx.globalAlpha = clamp(kFoe);
      ctx.font = disp(800, 13); ctx.fillStyle = tone('ink');
      ctx.fillText('COMBAT AI', R + 10, fun(1) + 4);
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText('= MORE FUN', R + 10, fun(1) + 18);
      ctx.globalAlpha = 1;
    }
    if (kCare > 0.35) {
      ctx.globalAlpha = clamp((kCare - 0.35) / 0.5);
      ctx.font = disp(800, 13); ctx.fillStyle = tone('accent');
      ctx.fillText('CARETAKER AI', R + 10, job(1) + 4);
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText('= NO JOB LEFT', R + 10, job(1) + 18);
      ctx.globalAlpha = 1;
    }

    // ── 유능도 표식 — 축 위를 계속 왕복한다 ──────────
    // 🔴 첫 자막부터 돈다. 축이 자라는 것만으로는 면적이 모자라 정적 검사에 걸린다
    //    (3px 선 하나가 프레임당 100px² 남짓 — 문턱 바로 아래다).
    {
      const P = 4.4, k = (t % P) / P;
      const x = k < 0.5 ? ease(k * 2) : ease(2 - k * 2);
      const px = lerp(L, R, x);
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(px, TOP - 6); ctx.lineTo(px, BOT); ctx.stroke();
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(px, kFun > 0.2 ? fun(x) : BOT, 7, 0, Math.PI * 2); ctx.fill();
      if (kCare > 0.2) {
        ctx.fillStyle = tone('accent');
        ctx.beginPath(); ctx.arc(px, job(x), 7, 0, Math.PI * 2); ctx.fill();
      }
    }

    // ── 판정 ─────────────────────────────────────
    if (kEnd > 0.02) {
      const s = '조용했던 기술적 근원';
      ctx.font = disp(900, 19);
      const sw = ctx.measureText(s).width;
      ctx.globalAlpha = clamp(kEnd);
      ctx.fillStyle = tone('ink');
      ctx.fillText(s, L + 12, TOP - 16);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(L + 12, TOP - 9); ctx.lineTo(L + 12 + sw * clamp(kEnd), TOP - 9);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }
  },
};
