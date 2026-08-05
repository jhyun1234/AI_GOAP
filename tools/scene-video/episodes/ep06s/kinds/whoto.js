import {
  disp, ease, clamp, lerp, frac, rnd,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* whoto — 두 번째 샷에서 멈추지 못하던 표식이 여기서 처음으로 멈춘다.

   이 편의 결말은 문장이 아니라 **동작**이다. anyone 에서 「누구에게?」 표식은 다섯 칸 위를
   끝까지 왕복했다 — 어디에 멈추든 아래가 똑같았으니 멈출 이유가 없었다. 여기서는 pickCue
   동안 그 왕복이 한 사람 위로 끌려와 선다. 아래 다섯 칸의 내용이 서로 다르기 때문이다.
   그것이 원문의 "'누구에게 명령하느냐'가 처음으로 중요해지는 순간"이다.

   multCue 에서 다섯 사람 아래 「×」 다섯이 한꺼번에 뜬다 — 사람을 가른 개입이 곱셈 하나였다는
   사실을 마지막으로 한 번 더 화면에 세운다. nextCue 에서 판 전체가 아래로 미끄러지고 위쪽에
   새 축 하나가 그어진다. 이 편이 얹은 축(성격) 위에 다음 편이 축을 하나 더 얹기 때문이다.

   🔴 훅은 여기서 안 그린다. 아웃트로 카드가 이 캔버스를 픽셀 동일 좌표로 덮으므로
   캔버스에 그리면 뜨자마자 가려진다(engine/index.html 의 .oc-hook 이 맡는다).
   🔴 그래서 예고 자막 구간의 움직임은 캔버스 쪽에서 따로 세워야 한다 — 칩만으로는 작다.
   판 전체(칸 다섯 + 결과 띠 다섯 + 곱셈 표식 다섯)를 20px 내리는 이유가 그것이다.
   창을 좁게(span 0.16) 잡은 것은 카드가 덮기 전에 다 끝내기 위해서다.

   🔴 결과 띠 안의 막대 길이는 rnd(정수)로 뽑는다. 값을 주장하는 것이 아니라 "다섯이 서로
   다르다"만 주장하기 위해서이고, 눈금도 숫자도 라벨도 붙이지 않았다. rnd 는 씨앗이 같으면
   같은 값이라 프레임마다 흔들리지 않는다. */

const SWEEP = 5.2;

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
    const cx = i => 46 + i * 65;
    const CW = 46, SH = 60, SY = 166;

    const pk = ease(cue(spec.pickCue ?? 0, 0.15, 0.55));
    const mk = ease(cue(spec.multCue ?? 1, 0.15, 0.7));
    const xk = ease(cue(spec.nextCue ?? 2, 0.20, 0.16));

    /* ── 다음 편 칩 (판 위) ────────────────────────── */
    if (xk > 0.02 && spec.nextLabel) {
      const k = clamp(xk * 1.5);
      const sp = Math.min(1, spring(k));
      const bw = Math.min(w - 80, 216), bx = (w - bw) / 2;
      ctx.globalAlpha = k;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
      setShadow(ctx, GLOW, 10, 0);
      roundRect(ctx, bx + (bw - bw * sp) / 2, 14 + (30 - 30 * sp) / 2, bw * sp, 30 * sp, 5);
      ctx.stroke();
      clearShadow(ctx);
      ctx.textAlign = 'center';
      const fs = fit(spec.nextLabel, 800, 13.5, bw - 24, 9);
      ctx.font = disp(800, fs * sp); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.nextLabel, w / 2, 34);
      ctx.globalAlpha = 1;
    }

    /* ── 새 축 한 줄 ───────────────────────────────── */
    if (xk > 0.02) {
      ctx.globalAlpha = clamp(xk * 1.4);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.moveTo(20, 58); ctx.lineTo(20 + (w - 40) * ease(xk), 58);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 판 전체 — nextCue 에서 아래로 밀린다 ───────── */
    ctx.save();
    ctx.translate(0, 20 * ease(xk));

    // 고르는 손 — 여기서 처음 멈춘다
    const u = frac(t / SWEEP);
    const s = u < 0.5 ? u * 2 : 2 - u * 2;
    const sweepX = lerp(cx(0), cx(N - 1), s);
    const px = lerp(sweepX, cx(clamp(spec.pickIndex ?? 2, 0, N - 1)), ease(pk));

    if (spec.askLabel) {
      ctx.textAlign = 'center';
      ctx.font = disp(800, 12); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.askLabel, px, 80);
    }
    ctx.fillStyle = tone('accent');
    setShadow(ctx, GLOW, 9, 0);
    ctx.beginPath();
    ctx.moveTo(px, 94); ctx.lineTo(px - 8, 84); ctx.lineTo(px + 8, 84);
    ctx.closePath(); ctx.fill();
    clearShadow(ctx);

    // 주민 다섯
    for (let i = 0; i < N; i++) {
      const x = cx(i), picked = i === (spec.pickIndex ?? 2);
      const col = picked && pk > 0.75 ? tone('accent') : tone('ink');
      ctx.strokeStyle = col; ctx.lineWidth = picked && pk > 0.75 ? 4 : 3;
      roundRect(ctx, x - CW / 2, 100, CW, 46, 5); ctx.stroke();
      ctx.beginPath(); ctx.arc(x, 116, 5.5, 0, Math.PI * 2); ctx.stroke();
      ctx.beginPath(); ctx.moveTo(x, 124); ctx.lineTo(x, 138); ctx.stroke();
    }

    // 곱셈 표식 다섯
    for (let i = 0; i < N; i++) {
      const k = clamp(mk * 6 - i);
      if (k <= 0.02) continue;
      const x = cx(i), r = 7 * Math.min(1, spring(k));
      ctx.globalAlpha = k;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(x - r, 158 - r); ctx.lineTo(x + r, 158 + r);
      ctx.moveTo(x + r, 158 - r); ctx.lineTo(x - r, 158 + r);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* 결과 띠 다섯 — 서로 다르고, 저마다 다른 속도로 흐른다.
       계속 도는 것이 여기 있다(예고 구간에도 살아 있어야 한다). */
    for (let i = 0; i < N; i++) {
      const k = clamp(pk * 5 - i * 0.7);
      if (k <= 0.02) continue;
      const x = cx(i) - CW / 2;

      ctx.globalAlpha = k;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, x, SY, CW, SH, 4); ctx.stroke();

      ctx.save();
      ctx.beginPath(); ctx.rect(x + 3, SY + 3, CW - 6, SH - 6); ctx.clip();
      for (let j = 0; j < 3; j++) {
        const bw = 14 + 22 * rnd(i * 13 + j * 5 + 1);
        const per = CW + bw;
        const ox = frac(t / (3.0 + i * 0.55) + j * 0.31) * per - bw;
        ctx.fillStyle = tone('ink');
        roundRect(ctx, x + ox, SY + 8 + j * 17, bw, 9, 3); ctx.fill();
      }
      ctx.restore();
      ctx.globalAlpha = 1;
    }

    ctx.restore();

    /* ── 원문 한 줄 ────────────────────────────────── */
    if (spec.meaning) {
      const k = clamp((mk - 0.4) / 0.4);
      if (k > 0.02) {
        ctx.globalAlpha = k;
        ctx.textAlign = 'center';
        const fs = fit(spec.meaning, 900, 14.5, w - 20, 10);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('ink');
        ctx.fillText(spec.meaning, w / 2, 268);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
