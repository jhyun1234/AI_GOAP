import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* buffer — 떨어지는 값을 아래 문턱 앞에서 먼저 잡는다.

   포만감 하나짜리 세로 계기다. 값이 위에서 내려오다가 35 에서 Goal_Snack 이 붙잡고(catchCue),
   그 아래 20(긴급 배고픔)에는 닿지 않는다(floorCue). 두 문턱 사이의 띠가 '완충'이다 —
   원문이 말하는 "긴급 배고픔 바로 앞 단계의 완충 goal"이 그 띠 자체다.

   오른쪽의 흩어진 표식 넷은 주민마다 다른 초기 포만감(±15)이다. 같은 때 한꺼번에
   배고파지지 않게 만든 장치라, 한 점이 아니라 흩어진 점으로 그려야 뜻이 산다.

   🔴 값을 한 번 떨어뜨리고 멈추지 않았다. 완충은 한 번 작동하고 끝나는 장치가 아니라
   계속 도는 순환이므로, 잡힌 뒤에도 값이 35 위에서 오르내린다. 정적 구간을 메우려고
   넣은 장식이 아니라 이 goal 이 실제로 하는 일이다.

   계속 도는 것 = 35 위에서 오르내리는 포만감 값. */

const CYCLE = 5.2;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));

    const ck = ease(cue(spec.catchCue ?? 0, 0.15, 0.62));
    const fk = ease(cue(spec.floorCue ?? 1, 0.15, 0.6));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const gx = 60, gw = 56, gTop = 34, gBot = 252;
    const yOf = v => gBot - (v / 100) * (gBot - gTop);

    const snackAt = spec.snack?.at ?? 35;
    const urgentAt = spec.urgent?.at ?? 20;

    /* ── 계기 ─────────────────────────────────────── */
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    roundRect(ctx, gx, gTop, gw, gBot - gTop, 4); ctx.stroke();

    ctx.textAlign = 'right';
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('100', gx - 7, gTop + 5);
    ctx.fillText('0', gx - 7, gBot + 4);

    ctx.textAlign = 'left';
    ctx.font = disp(800, 11); ctx.fillStyle = tone('sub');
    ctx.fillText(spec.axis || '포만감', gx, gTop - 12);

    /* ── 완충 띠 ──────────────────────────────────── */
    if (fk > 0.05) {
      const k = clamp(fk * 1.5);
      ctx.globalAlpha = k * 0.18;
      ctx.fillStyle = tone('accent');
      ctx.fillRect(gx + 3, yOf(snackAt), gw - 6, yOf(urgentAt) - yOf(snackAt));
      ctx.globalAlpha = 1;
    }

    /* ── 현재 값 ──────────────────────────────────── */
    const fall = lerp(82, snackAt, ease(ck));
    const held = clamp((ck - 0.82) / 0.18);
    const wave = 26 * (0.5 - 0.5 * Math.cos(frac(t / CYCLE) * Math.PI * 2));
    const v = fall + wave * held;
    {
      const vy = yOf(v);
      ctx.fillStyle = tone('ink');
      ctx.globalAlpha = 0.28;
      ctx.fillRect(gx + 3, vy, gw - 6, gBot - 3 - vy);
      ctx.globalAlpha = 1;
      setShadow(ctx, GLOW, 10, 0);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.beginPath(); ctx.moveTo(gx + 1, vy); ctx.lineTo(gx + gw - 1, vy); ctx.stroke();
      clearShadow(ctx);
    }

    /* ── 문턱 둘 ──────────────────────────────────── */
    const LX = gx + gw + 20;                     // 라벨 열 — 계기 오른쪽
    const rowLabel = (y, name, num, on, strong, dir) => {
      ctx.globalAlpha = on;
      ctx.strokeStyle = strong ? tone('accent') : tone('ink');
      ctx.lineWidth = 3;
      ctx.setLineDash(strong ? [] : [6, 5]);
      ctx.beginPath(); ctx.moveTo(gx - 12, y); ctx.lineTo(gx + gw + 12, y); ctx.stroke();
      ctx.setLineDash([]);
      ctx.textAlign = 'left';
      const ny = dir < 0 ? y - 25 : y + 18;
      const vy = dir < 0 ? y - 7 : y + 37;
      const fs = fit(name, 800, 12.5, w - LX - 8);
      ctx.font = disp(800, fs);
      ctx.fillStyle = strong ? tone('accent') : tone('ink');
      ctx.fillText(name, LX, ny);
      ctx.font = mono(700, 15);
      ctx.fillStyle = strong ? tone('accent') : tone('ink');
      ctx.fillText(String(num), LX, vy);
      ctx.globalAlpha = 1;
    };

    rowLabel(yOf(snackAt), spec.snack?.name || 'Goal_Snack', snackAt, clamp(ck * 1.8), true, -1);
    rowLabel(yOf(urgentAt), spec.urgent?.name || '긴급 배고픔', urgentAt, clamp(fk * 1.8), false, 1);

    if (fk > 0.45 && spec.floorNote) {
      const k = clamp((fk - 0.45) / 0.55);
      ctx.globalAlpha = k;
      ctx.textAlign = 'left';
      const fs = fit(spec.floorNote, 800, 11, w - LX - 8, 8);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.floorNote, LX, yOf(urgentAt) + 56);
      ctx.globalAlpha = 1;
    }
    if (ck > 0.5 && spec.snackNote) {
      const k = clamp((ck - 0.5) / 0.5);
      ctx.globalAlpha = k * 0.95;
      ctx.textAlign = 'left';
      const fs = fit(spec.snackNote, 700, 10.5, w - 16, 7.5);
      ctx.font = disp(700, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.snackNote, 8, 284);
      ctx.globalAlpha = 1;
    }

    /* ── 초기 포만감 ±15 ──────────────────────────── */
    if (fk > 0.15 && spec.spread) {
      const k = clamp((fk - 0.15) / 0.55);
      const bx = 262, top = 56, bot = 108;
      ctx.globalAlpha = k;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(bx, top); ctx.lineTo(bx, bot);
      ctx.moveTo(bx, top); ctx.lineTo(bx + 6, top);
      ctx.moveTo(bx, bot); ctx.lineTo(bx + 6, bot);
      ctx.stroke();

      const offs = [0, 0.34, 0.62, 1];
      offs.forEach((u, i) => {
        const y = lerp(top, bot, u);
        ctx.fillStyle = tone('ink');
        ctx.globalAlpha = k * (0.75 + 0.25 * (i % 2));
        ctx.beginPath(); ctx.arc(bx + 16 + (i % 2) * 7, y, 4, 0, Math.PI * 2); ctx.fill();
      });
      ctx.globalAlpha = k;

      ctx.textAlign = 'left';
      const fs = fit(spec.spread, 800, 11, w - (bx - 6) - 8, 8);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(spec.spread, bx - 6, top - 14);
      if (spec.spreadNote) {
        const nfs = fit(spec.spreadNote, 700, 9.5, w - (bx - 6) - 8, 7);
        ctx.font = disp(700, nfs); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.spreadNote, bx - 6, bot + 20);
      }
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
