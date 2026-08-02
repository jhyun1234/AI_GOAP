import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* pinrule — 규칙이 코드 밖으로 나와 문 앞에 선다.

   위: 옛 규칙 묶음에 취소선이 그어지고 새 묶음이 강조색으로 앉는다.
   가운데: 새 규칙 아홉 칸이 세로 눈금으로 서고, 그중 둘만 문장이 뽑혀 나온다. 나머지 일곱은
   눈금 칸으로만 둔다 — 원문이 아홉 중 둘의 문구만 적었기 때문이다. 없는 일곱을 지어내지 않는다.
   아래: 자동 게이트 다섯이 커밋 앞을 막고 서고, 옛날 방식이 다가오다 첫 게이트에서 막힌다.

   회차의 기본 도형 안에서 이 그림의 몫은 '지키는 일을 사람 손에서 뗀다'.
   계속 도는 것 = 다섯 게이트를 왼쪽에서 오른쪽으로 계속 훑는 검사 표식.
   🔴 그 표식은 t 로 돌지만 사건이 아니다. 막히는 사건은 전부 gateCue 가 몰고, 표식은
   게이트가 놀고 있지 않다는 것만 보인다. */

const SCAN = 2.2;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const rules = spec.rules || [];
    const slots = spec.slots ?? 9;
    const gates = spec.gates ?? 5;

    const wk = ease(cue(spec.swapCue ?? 0, 0.15, 0.65));
    const pk = ease(cue(spec.pinCue ?? 1, 0.15, 0.7));
    const gk = ease(cue(spec.gateCue ?? 2, 0.15, 0.6));

    ctx.textBaseline = 'alphabetic';

    /* ── 규칙 묶음 교체 ───────────────────────────── */
    {
      ctx.textAlign = 'center';
      const oy = 30;
      ctx.globalAlpha = clamp(wk * 2.5);
      ctx.font = disp(800, 12.5);
      const ot = spec.oldLabel || '';
      const ow = ctx.measureText(ot).width;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('sub');
      roundRect(ctx, 82 - ow / 2 - 12, oy - 18, ow + 24, 26, 3); ctx.stroke();
      ctx.fillStyle = tone('sub');
      ctx.fillText(ot, 82, oy);
      if (wk > 0.35) {
        const k = clamp((wk - 0.35) / 0.4);
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(82 - ow / 2 - 8, oy - 5);
        ctx.lineTo(82 - ow / 2 - 8 + (ow + 16) * k, oy - 5);
        ctx.stroke();
      }
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(150, oy - 5); ctx.lineTo(178, oy - 5);
      ctx.moveTo(178, oy - 5); ctx.lineTo(172, oy - 9);
      ctx.moveTo(178, oy - 5); ctx.lineTo(172, oy - 1);
      ctx.stroke();
      ctx.globalAlpha = 1;

      const nk = clamp(wk * 1.6 - 0.5);
      if (nk > 0.02) {
        const s = Math.min(1, spring(nk));
        ctx.globalAlpha = clamp(nk * 2);
        ctx.font = disp(900, 13);
        const nt = spec.newLabel || '';
        const nw = ctx.measureText(nt).width;
        setShadow(ctx, GLOW, 12, 0);
        ctx.lineWidth = 4; ctx.strokeStyle = tone('accent');
        roundRect(ctx, 262 - (nw + 26) * s / 2, oy - 19, (nw + 26) * s, 28 * s, 3); ctx.stroke();
        clearShadow(ctx);
        ctx.fillStyle = tone('accent'); ctx.font = disp(900, 13 * s);
        ctx.fillText(nt, 262, oy + 1);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 아홉 칸과 뽑혀 나온 둘 ───────────────────── */
    const slotX = 20, slotTop = 62, slotPitch = 12, slotH = 8;
    for (let i = 0; i < slots; i++) {
      const k = clamp(pk * (slots + 2) - i);
      if (k < 0.02) continue;
      ctx.globalAlpha = clamp(k * 2);
      ctx.fillStyle = i < rules.length ? tone('accent') : tone('sub');
      ctx.fillRect(slotX, slotTop + i * slotPitch, 14, slotH);
      ctx.globalAlpha = 1;
    }

    rules.slice(0, 2).forEach((r, i) => {
      const k = clamp(pk * 2.6 - 0.4 - i * 0.7);
      if (k < 0.02) return;
      const y = 60 + i * 40;
      ctx.globalAlpha = clamp(k * 1.6);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(slotX + 14, slotTop + i * slotPitch + slotH / 2);
      ctx.lineTo(44, y + 17);
      ctx.stroke();

      ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
      roundRect(ctx, 46, y, w - 60, 34, 3); ctx.stroke();
      ctx.textAlign = 'left';
      let fs = 11; ctx.font = disp(700, fs);
      while (fs > 8 && ctx.measureText(r).width > w - 84) { fs -= 0.5; ctx.font = disp(700, fs); }
      ctx.fillStyle = tone('ink');
      ctx.fillText(r, 58, y + 22);
      ctx.globalAlpha = 1;
    });

    /* ── 게이트와 커밋 ─────────────────────────────── */
    {
      const gy = 202, gh = 60, gx0 = 168, gpitch = 16;
      const base = clamp(gk * 2.2);
      ctx.globalAlpha = base;

      ctx.textAlign = 'left';
      ctx.fillStyle = tone('sub'); ctx.font = disp(800, 10.5);
      ctx.fillText(spec.gateLabel || '', gx0 - 4, gy - 10);

      for (let i = 0; i < gates; i++) {
        const k = clamp(gk * (gates + 2) - i);
        if (k < 0.02) continue;
        ctx.globalAlpha = base * clamp(k * 2);
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
        const x = gx0 + i * gpitch;
        const half = (gh / 2) * Math.min(1, k * 1.5);
        ctx.beginPath();
        ctx.moveTo(x, gy + gh / 2 - half); ctx.lineTo(x, gy + gh / 2 + half);
        ctx.stroke();
      }
      ctx.globalAlpha = base;

      /* 계속 도는 것 — 게이트를 훑는 검사 표식 */
      {
        const u = frac(t / SCAN);
        const x = lerp(gx0 - 8, gx0 + (gates - 1) * gpitch + 8, u);
        ctx.globalAlpha = base * 0.7 * Math.sin(Math.PI * u);
        ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.moveTo(x, gy - 2); ctx.lineTo(x, gy + gh + 2); ctx.stroke();
        ctx.globalAlpha = base;
      }

      // 커밋
      const cx0 = gx0 + (gates - 1) * gpitch + 20;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      roundRect(ctx, cx0, gy + 12, w - cx0 - 14, 36, 3); ctx.stroke();
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('ink'); ctx.font = disp(800, 12);
      ctx.fillText(spec.commitLabel || '커밋', cx0 + (w - cx0 - 14) / 2, gy + 35);

      // 옛날 방식이 다가오다 막힌다 — 0~0.55 접근, 0.55~1 되밀림
      const app = ease(clamp(gk / 0.55));
      const back = ease(clamp((gk - 0.55) / 0.45));
      const chipW = 84;
      const px = lerp(8, gx0 - chipW - 8, app) - back * 24;
      ctx.globalAlpha = clamp(gk * 3) * (1 - back * 0.8);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      roundRect(ctx, px, gy + 18, chipW, 24, 3); ctx.stroke();
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('ink'); ctx.font = disp(800, 10.5);
      ctx.fillText(spec.violation || '', px + chipW / 2, gy + 34);
      ctx.globalAlpha = 1;

      if (back > 0.12) {
        ctx.globalAlpha = clamp((back - 0.12) / 0.4);
        ctx.textAlign = 'left';
        ctx.fillStyle = tone('accent'); ctx.font = disp(900, 11);
        ctx.fillText(spec.rejectLabel || '', gx0 - 4, gy + gh + 18);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
