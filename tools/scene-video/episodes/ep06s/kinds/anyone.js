import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* anyone — 명령이 누구에게 가든 아래 결과 칸이 똑같은 모양으로 채워진다.

   앞 샷(lockstep)이 "다섯이 같이 움직인다"였다면 여기는 그 결과다 — **고를 이유가 없다.**
   그래서 이 그림의 주인공은 주민이 아니라 **고르는 손**이다. 「누구에게?」 표식이 다섯 칸
   위를 계속 왕복하는데, 아래 결과가 전부 같아서 어디에 멈추든 의미가 없다. 표식은 이 샷에서
   **한 번도 안 멈춘다** — 마지막 샷 whoto 에서 처음 멈추고, 그게 이 편의 결말이다.

   sameCue 에서 결과 칸 다섯이 왼쪽부터 차례로 채워지는데 다섯 모양이 완전히 같다.
   noneCue 에서 칸 사이에 등호 넷이 켜지고, 아래에 원문이 따옴표로 부른 문장이 남는다.

   🔴 등호를 고른 이유: "같다"를 그리는 데 숫자도 막대도 필요 없다. 원문은 다섯의 결과를
   숫자로 준 적이 없으므로 값처럼 보이는 것을 그리면 그 순간 지어낸 화면이 된다.
   여기 있는 것은 "같음" 하나뿐이고 그건 원문 문장 그대로다. */

const SWEEP = 5.2;   // 「누구에게?」 표식 한 왕복(초)

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
    const cx = i => 62 + i * 58;
    const CW = 50;

    const sk = ease(cue(spec.sameCue ?? 0, 0.15, 0.7));
    const nk = ease(cue(spec.noneCue ?? 1, 0.15, 0.6));

    /* ── 명령 카드 ─────────────────────────────────── */
    if (spec.orderLabel) {
      const bw = 96, bx = (w - bw) / 2;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
      setShadow(ctx, GLOW, 10, 0);
      roundRect(ctx, bx, 14, bw, 32, 5); ctx.stroke();
      clearShadow(ctx);
      ctx.textAlign = 'center';
      const fs = fit(spec.orderLabel, 900, 16, bw - 20, 11);
      ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.orderLabel, w / 2, 36);
    }

    /* ── 계속 도는 것 — 고르는 손. 멈추지 않는다 ────── */
    const u = frac(t / SWEEP);
    const s = u < 0.5 ? u * 2 : 2 - u * 2;      // 등속 왕복(양 끝에서도 안 느려진다)
    const px = lerp(cx(0), cx(N - 1), s);

    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
    ctx.globalAlpha = 0.5;
    ctx.beginPath(); ctx.moveTo(px, 46); ctx.lineTo(px, 62); ctx.stroke();
    ctx.globalAlpha = 1;

    ctx.fillStyle = tone('accent');
    ctx.beginPath();
    ctx.moveTo(px, 74); ctx.lineTo(px - 8, 62); ctx.lineTo(px + 8, 62);
    ctx.closePath(); ctx.fill();

    if (spec.askLabel) {
      ctx.textAlign = 'left';
      ctx.font = disp(800, 11.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.askLabel, 8, 40);
    }

    /* ── 주민 다섯 ─────────────────────────────────── */
    for (let i = 0; i < N; i++) {
      const x = cx(i);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, x - CW / 2, 82, CW, 42, 5); ctx.stroke();
      ctx.beginPath(); ctx.arc(x, 96, 5.5, 0, Math.PI * 2); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(x, 104); ctx.lineTo(x, 116); ctx.stroke();
    }

    /* ── 결과 칸 다섯 — 다섯이 똑같다 ───────────────── */
    if (spec.resultLabel) {
      ctx.textAlign = 'left';
      ctx.font = disp(800, 11.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.resultLabel, 8, 176);
    }

    for (let i = 0; i < N; i++) {
      const x = cx(i), k = clamp(sk * 6 - i);

      // 아직 안 채워진 칸 — 점선이 흐른다(t)
      if (k < 0.98) {
        ctx.globalAlpha = (1 - k) * 0.9;
        ctx.setLineDash([8, 6]);
        ctx.lineDashOffset = -(t * 12) % 14;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        roundRect(ctx, x - CW / 2, 148, CW, 46, 5); ctx.stroke();
        ctx.setLineDash([]); ctx.lineDashOffset = 0;
        ctx.globalAlpha = 1;
      }

      if (k <= 0.02) continue;
      const sp = Math.min(1, spring(k));
      ctx.globalAlpha = k;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, x - CW * sp / 2, 148 + (46 - 46 * sp) / 2, CW * sp, 46 * sp, 5);
      ctx.stroke();

      // 다섯 칸에 완전히 같은 자국
      ctx.fillStyle = tone('accent');
      roundRect(ctx, x - 15, 158, 30, 8, 3); ctx.fill();
      roundRect(ctx, x - 9, 174, 18, 7, 3); ctx.fill();
      ctx.globalAlpha = 1;
    }

    /* ── 등호 넷 — "같다" ──────────────────────────── */
    for (let j = 0; j < N - 1; j++) {
      const k = clamp(nk * 5 - j);
      if (k <= 0.02) continue;
      const ex = (cx(j) + cx(j + 1)) / 2;
      ctx.globalAlpha = k;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(ex - 8, 167); ctx.lineTo(ex + 8, 167);
      ctx.moveTo(ex - 8, 176); ctx.lineTo(ex + 8, 176);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 원문 한 줄 ────────────────────────────────── */
    if (spec.quote) {
      const k = clamp((nk - 0.35) / 0.45);
      if (k > 0.02) {
        ctx.globalAlpha = k;
        ctx.textAlign = 'center';
        const fs = fit(spec.quote, 800, 13.5, w - 20, 9);
        ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.quote, w / 2, 238);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
