import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* flatline — 계기는 전부 정상인데, 아무 일도 안 일어난다.

   원문: "화면 속 마을은 완벽했습니다. 에러 하나 없이, 아무도 떠나지 않고, 하루가 24일째까지
   매끄럽게 돌아갔습니다." / "다 만들고 나서 제가 받은 피드백은 「기술은 만점인데 재미가
   0점」이었습니다."

   🔴 이 회차의 기본 도형은 **여유(빈 칸)** 다. 여기서는 그 여유가 "사건이 들어와야 할
   자리인데 끝까지 비어 있는 칸"으로 나온다 — 선 위를 흘러가는 사건 슬롯이 하나도 안 채워진다.
   S3 의 남는 절반, S4 의 조여지는 띠가 같은 도형의 다른 얼굴이다.

   🔴 **선이 평평한 것이 이 샷의 뜻이라, 선을 흔들어 정적을 때우면 안 된다.**
   그래서 움직이는 것은 선이 아니라 **선 밑을 지나가는 시간**이다 — 눈금과 빈 사건 슬롯이
   계속 왼쪽으로 흘러가고, 관측 표식이 그 위를 훑는다. 화면은 계속 도는데 아무것도 안 생긴다.

   🔴 계기 숫자 셋(0 · 0 · 24)은 전부 원문 문장에 있는 값이다. 없는 계기를 만들지 않았고,
   「기술은 만점」의 '만점'은 점수가 아니라 말이라 숫자로 그리지 않았다. */

const SP = 26;          // 눈금 간격(px)
const SPD = 17;         // 눈금이 흐르는 속도(px/s)
const SLOT_EVERY = 3;   // 몇 눈금마다 사건 슬롯 하나

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

    const gk = ease(cue(spec.gaugeCue ?? 0, 0.15, 0.6));
    const vk = ease(cue(spec.verdictCue ?? 1, 0.10, 0.85));
    const land = ease(clamp(vk / 0.7));      // 판정이 자리에 앉는 지점

    const X0 = 16, X1 = w - 16;
    const LY = 148;                          // 평평한 선

    /* ── 계기 셋 ─────────────────────────────────────
       원문의 '에러 하나 없이 · 아무도 떠나지 않고 · 24일째' 를 그대로 읽는 세 칸. */
    const gauges = spec.gauges ?? [];
    const GAP = 10, BW = (X1 - X0 - GAP * (Math.max(1, gauges.length) - 1)) / Math.max(1, gauges.length);
    for (let i = 0; i < gauges.length; i++) {
      const a = clamp(gk * 2.4 - i * 0.42) * lerp(1, 0.5, land);   // 판정이 앉으면 물러난다
      if (a <= 0.02) continue;
      const x = X0 + i * (BW + GAP);
      const dy = (1 - clamp(gk * 2.4 - i * 0.42)) * -9;

      ctx.globalAlpha = a;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, x, 16 + dy, BW, 60, 6); ctx.stroke();

      ctx.textAlign = 'left';
      ctx.font = disp(800, 10.5); ctx.fillStyle = tone('sub');
      ctx.fillText(gauges[i].label ?? '', x + 11, 36 + dy);

      const v = String(gauges[i].value ?? '');
      const fs = fit(v, 900, 26, BW - 22, 12);
      ctx.font = disp(900, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(v, x + 11, 66 + dy);
      ctx.globalAlpha = 1;
    }

    /* ── 평평한 선 + 흘러가는 시간 ───────────────────
       shift 는 t 의 함수라 문턱이 없다. '언제 처음 뜨는가' 만 gaugeCue 가 정한다. */
    const on = clamp(gk * 2.2);
    if (on > 0.02) {
      const shift = t * SPD;
      ctx.save();
      ctx.beginPath(); ctx.rect(X0, LY - 34, X1 - X0, 62); ctx.clip();

      const nT = Math.ceil((X1 - X0) / SP) + 3;
      const k0 = Math.floor(shift / SP);
      for (let k = k0; k < k0 + nT; k++) {
        const px = X0 + k * SP - shift;
        ctx.globalAlpha = on * 0.9;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.moveTo(px, LY + 10); ctx.lineTo(px, LY + 20); ctx.stroke();

        /* 사건이 들어와야 할 칸 — 끝까지 비어 있다 */
        if (((k % SLOT_EVERY) + SLOT_EVERY) % SLOT_EVERY === 0) {
          ctx.globalAlpha = on * 0.85;
          ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
          roundRect(ctx, px - 7, LY - 27, 14, 14, 3); ctx.stroke();
        }
      }
      ctx.restore();
      ctx.globalAlpha = 1;

      /* 선 자체는 흔들지 않는다 */
      ctx.globalAlpha = on * lerp(1, 0.55, land);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
      ctx.beginPath(); ctx.moveTo(X0, LY); ctx.lineTo(X1, LY); ctx.stroke();
      ctx.globalAlpha = 1;

      /* 관측 표식 — 계속 훑는데 아무것도 못 만난다 */
      const u = frac(t / 3.3);
      const sx = lerp(X0, X1, u);
      ctx.globalAlpha = on * (0.35 + 0.65 * Math.sin(Math.PI * u)) * lerp(1, 0.5, land);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(Math.max(X0, sx - 26), LY); ctx.lineTo(sx, LY); ctx.stroke();
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(sx, LY, 6, 0, Math.PI * 2); ctx.fill();
      ctx.globalAlpha = 1;
    }

    /* ── 판정이 그 위에 내려앉는다 ───────────────────
       계기가 전부 정상인 화면에 사람 말 한마디가 떨어진다. */
    if (vk > 0.02) {
      const txt = spec.verdict ?? '';
      const a = clamp(vk * 2.2);
      const dy = (1 - land) * -20;

      ctx.globalAlpha = a;
      ctx.textAlign = 'center';
      const fs = fit(txt, 900, 34, w - 64, 14);
      ctx.font = disp(900, fs);
      setShadow(ctx, GLOW, 14, 0);
      ctx.fillStyle = tone('accent');
      ctx.fillText(txt, w / 2, 236 + dy);
      clearShadow(ctx);

      const bw = ctx.measureText(txt).width * land;
      ctx.fillStyle = tone('accent');
      ctx.fillRect(w / 2 - bw / 2, 248, bw, 4);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
