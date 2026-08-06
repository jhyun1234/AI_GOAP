import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* noinput — 주민은 계속 도는데 촌장 칸은 끝까지 비어 있고, 그 아래에 눈금이 생긴다.

   원문: "그런데 저는 그 30분 내내 아무것도 할 게 없었습니다. 주민들이 너무 알아서 잘
   살았거든요." / "저는 여기서 감으로 밸런스를 만지는 대신, 겨울 수지를 직접 계산해봤습니다."

   🔴 이 샷의 도형도 **빈 칸**이다. 앞 샷(flatline)의 빈 칸은 '사건이 안 들어온 자리'였고
   여기는 '내가 손댈 자리'다 — 같은 여유가 화면 반대편에서 한 번 더 나온다.

   🔴 마지막에 그어지는 눈금이 이 편의 전환점이다. 「감으로 만진다」는 눈금 없는 손잡이이고
   「계산한다」는 눈금이다. 그래서 cue 1 에서 자가 통째로 그어진다 — 값을 적지는 않는다.
   조정 뒤의 수치는 원문에 없으므로 눈금에 숫자를 붙이면 지어낸 값이 된다.

   🔴 주민 표식은 다섯이다. 조정 **전** 주민 수가 원문에 5명으로 적혀 있어서 그렇고
   (「주민 수를 5명에서 10명으로 늘렸습니다」), 다음 샷에서 그 수가 실제로 바뀐다.

   🔴 **이 샷은 자막이 한 줄이다**(2026-08-06 길이 실측 뒤 두 줄을 한 줄로 합쳤다).
   그래서 두 단계를 **같은 자막의 앞뒤 구간**으로 나눈다 — 앞 절('삼십 분을 봤는데 제가
   할 게 없어서')에서 창과 주민과 빈 칸이 서고, 뒤 절('감 대신 계산해 봤어요')에서 눈금이
   그어진다. cue 를 안 쓰는 등장 조건은 여전히 하나도 없다. 오히려 자막 안의 어느 어절에서
   무엇이 터지는지가 더 정확해졌다. */

const HOMES = [           // fx, fy, 궤도 주기(초), 궤도 반지름
  [0.14, 0.30, 3.1, 13],
  [0.38, 0.66, 4.2, 10],
  [0.58, 0.24, 2.7, 15],
  [0.76, 0.58, 3.7, 11],
  [0.92, 0.36, 4.8, 9]
];

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

    /* 🔴 자막 한 줄을 앞뒤로 나눠 쓴다. c0 는 그 한 줄의 raw 진행률이고,
       ik = 앞 절(관측 창·주민·빈 칸), sk = 뒤 절(눈금)이다.
       split 은 그 자막에서 「감 대신」이 시작되는 지점으로 잡았다. */
    const c0 = cue(spec.idleCue ?? 0, 0.15, 0.9);
    const split = spec.scaleSplit ?? 0.60;
    const ik = ease(clamp(c0 / 0.55));
    const sk = ease(clamp((c0 - split) / (1 - split)));

    const X0 = 16, X1 = w - 16;
    const WY0 = 34, WY1 = 168;

    /* ── 관측 창 ─────────────────────────────────── */
    const a0 = clamp(ik * 2.4);
    if (a0 > 0.02) {
      ctx.globalAlpha = a0;
      ctx.textAlign = 'left';
      ctx.font = disp(800, 11.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.windowLabel ?? '', X0, 24);

      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.rect(X0, WY0, X1 - X0, WY1 - WY0); ctx.stroke();
      ctx.globalAlpha = 1;

      /* 지켜보는 중 — 세로 훑개가 창을 계속 가로지른다.
         t 의 함수라 문턱이 없다. 면적이 큰 것이 이 샷의 유일한 큰 움직임이다. */
      const su = frac(t / 4.1);
      const sx = lerp(X0 + 2, X1 - 2, su);
      ctx.globalAlpha = a0 * (0.25 + 0.55 * Math.sin(Math.PI * su));
      ctx.fillStyle = tone('ink');
      ctx.fillRect(sx - 2, WY0 + 3, 4, WY1 - WY0 - 6);
      ctx.globalAlpha = 1;

      /* 주민 다섯 — 각자 제 궤도를 계속 돈다. 아무도 나를 부르지 않는다. */
      for (let i = 0; i < HOMES.length; i++) {
        const [fx, fy, per, rr] = HOMES[i];
        const a = clamp(ik * 2.6 - i * 0.22);
        if (a <= 0.02) continue;
        const hx = X0 + 22 + fx * (X1 - X0 - 44);
        const hy = WY0 + 20 + fy * (WY1 - WY0 - 40);
        const ang = frac(t / per) * Math.PI * 2 + i * 1.7;
        const px = hx + Math.cos(ang) * rr, py = hy + Math.sin(ang) * rr * 0.66;

        ctx.globalAlpha = a * 0.5;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.ellipse(hx, hy, rr, rr * 0.66, 0, 0, Math.PI * 2); ctx.stroke();

        ctx.globalAlpha = a;
        ctx.fillStyle = tone('ink');
        ctx.beginPath(); ctx.arc(px, py, 5.5, 0, Math.PI * 2); ctx.fill();
        ctx.globalAlpha = 1;
      }
    }

    /* ── 내가 손댄 칸 — 끝까지 비어 있다 ──────────── */
    const a1 = clamp(ik * 2.2 - 0.5);
    if (a1 > 0.02) {
      const BY = 202, BH = 26;
      ctx.globalAlpha = a1;
      ctx.textAlign = 'left';
      ctx.font = disp(800, 11.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.inputLabel ?? '', X0, 194);

      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, X0, BY, X1 - X0, BH, 5); ctx.stroke();

      /* 빈 것을 빈 채로 보인다 — 안쪽은 흐르는 점선 하나뿐 */
      ctx.setLineDash([8, 9]);
      ctx.lineDashOffset = -(t * 13) % 17;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(X0 + 10, BY + BH / 2); ctx.lineTo(X1 - 34, BY + BH / 2);
      ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;

      ctx.textAlign = 'right';
      ctx.font = disp(900, 17); ctx.fillStyle = tone('ink');
      ctx.fillText(String(spec.inputValue ?? '0'), X1 - 11, BY + BH - 7);
      ctx.globalAlpha = 1;
    }

    /* ── 감 대신 눈금 ────────────────────────────── */
    if (sk > 0.02) {
      const AY = 264, len = (X1 - X0) * ease(sk);
      ctx.globalAlpha = clamp(sk * 2.4);
      ctx.textAlign = 'left';
      ctx.font = disp(800, 11.5); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.scaleLabel ?? '', X0, 240);

      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(X0, AY); ctx.lineTo(X0 + len, AY); ctx.stroke();

      for (let x = 0; x <= len; x += 14) {
        const big = Math.round(x / 14) % 5 === 0;
        ctx.beginPath();
        ctx.moveTo(X0 + x, AY); ctx.lineTo(X0 + x, AY + (big ? 13 : 7));
        ctx.stroke();
      }
      ctx.globalAlpha = 1;

      /* 재는 중 — 캐럿이 그어진 만큼을 계속 오간다 */
      if (len > 24) {
        const cu = frac(t / 2.9);
        const cx = X0 + len * (cu < 0.5 ? cu * 2 : 2 - cu * 2);
        ctx.globalAlpha = clamp(sk * 2.4);
        ctx.fillStyle = tone('accent');
        ctx.beginPath();
        ctx.moveTo(cx, AY - 4); ctx.lineTo(cx - 6, AY - 13); ctx.lineTo(cx + 6, AY - 13);
        ctx.closePath(); ctx.fill();
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
