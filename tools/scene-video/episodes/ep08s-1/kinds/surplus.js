import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* surplus — 필요한 만큼 위에 똑같은 만큼이 한 번 더 쌓여 있다.

   원문: "겨울을 나는 데 필요한 포만이 562인데, 주민들이 실제로 보유한 건 1,110이었습니다.
   필요량의 딱 2배를 쌓아두고 겨울을 맞고 있었던 겁니다."

   🔴 「2배」를 숫자로 안 적었다. **두 막대의 길이가 같은 것을 눈이 보게** 만드는 쪽이
   이 편의 도형에 맞다 — 아래 막대의 남는 절반이 위 막대와 정확히 같은 길이이고, 그 아래
   대괄호 둘이 같은 폭으로 놓인다. 라벨을 붙이면 화면이 자막을 옮겨 적는 것이 된다.

   🔴 값은 둘뿐이다(562 · 1,110). 원문에 없는 단위 환산·일수·1인당 소비량은 만들지 않았다.
   막대 길이가 정확히 1:2 인 것은 원문이 「딱 2배」라고 못박았기 때문이고, 562×2 = 1,124 와
   1,110 의 1.3% 차이를 그리려 들면 오히려 원문에 없는 정밀도를 주장하게 된다.

   🔴 이 샷의 남는 절반이 이 편의 기본 도형(여유)의 정면이다. 앞 두 샷에서 '비어 있는 칸'
   으로 나왔던 것이 여기서 '넘치는 양'으로 뒤집혀 나오고, 다음 샷에서 그대로 조여진다.

   ── 글자 자리 (2026-08-06 검수로 옮김) ─────────────────────────
   값 둘은 **막대 밖**, 각 막대가 끝나는 자리 **위**에 오른쪽 정렬로 놓는다. 「여유」는
   **대괄호 아래**다. 세 문자열이 서로도, 빗금과도 한 픽셀도 안 겹친다.
   전에는 값을 막대 **안쪽**에 넣었는데 아래 막대의 그 자리가 초록 빗금이라 ①흰 숫자가
   빗금 위에 얹혀 팔레트 규약을 깨고 ②바로 위 「여유」와 붙어 **「여유 = 1,110」 으로
   오독**됐다. 실제 여유는 548 이고 그 값은 원문에 없다 — 그래서 여기도 숫자를 안 붙이고
   대괄호가 가리키는 구간만으로 뜻을 지운다. */

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

    const gk = ease(cue(spec.gapCue ?? 0, 0.15, 0.62));

    const X0 = 16, X1 = w - 16, FULL = X1 - X0, HALF = FULL / 2;
    const AY = 74, BY = 172, BH = 40;

    const aw = HALF * ease(clamp(gk / 0.42));
    const bw = FULL * ease(clamp((gk - 0.28) / 0.72));

    /* ── 단위 ────────────────────────────────────── */
    ctx.globalAlpha = clamp(gk * 3);
    ctx.textAlign = 'left';
    ctx.font = disp(800, 11.5); ctx.fillStyle = tone('sub');
    ctx.fillText(spec.unitLabel ?? '', X0, 30);
    ctx.globalAlpha = 1;

    /* ── 필요한 만큼 ─────────────────────────────── */
    if (aw > 4) {
      ctx.globalAlpha = clamp(gk * 3);
      ctx.font = disp(800, 12); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.needLabel ?? '', X0, 66);

      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, X0, AY, aw, BH, 5); ctx.stroke();

      /* 🔴 값은 막대 **밖**, 막대가 끝나는 자리 바로 위에 둔다(2026-08-06 검수).
         전에는 막대 안쪽에 넣었는데, 아래 막대에서는 그 자리가 초록 빗금이라 흰 숫자가
         빗금 위에 얹혔다 — 팔레트 규약("흰색과 강조색을 같은 픽셀 위에서 안 섞는다")도
         깨고, 「여유」 라벨 바로 아래라 「여유 = 1,110」 으로도 읽혔다.
         두 막대가 같은 규칙(왼쪽에 이름, 끝나는 자리에 값)으로 읽히는 덤이 있다. */
      if (aw > 104) {                       // 이름표(NEEDED)와 겹치지 않을 만큼 자란 뒤에
        ctx.textAlign = 'right';
        const v = String(spec.needValue ?? '');
        ctx.font = disp(900, 22); ctx.fillStyle = tone('ink');
        ctx.fillText(v, X0 + aw - 6, AY - 8);   // 갈림선(점선)에 붙지 않게 6px 띄운다
        ctx.textAlign = 'left';
      }
      ctx.globalAlpha = 1;
    }

    /* ── 쌓아둔 만큼 ─────────────────────────────── */
    if (bw > 4) {
      const a = clamp((gk - 0.28) * 3.4);
      ctx.globalAlpha = a;
      ctx.font = disp(800, 12); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.stockLabel ?? '', X0, 164);

      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, X0, BY, bw, BH, 5); ctx.stroke();
      ctx.globalAlpha = 1;

      /* 남는 절반 — 빗금이 계속 흐른다(t 의 함수, 문턱 없음) */
      if (bw > HALF + 6) {
        const sx = X0 + HALF, sw = bw - HALF;
        ctx.save();
        roundRect(ctx, sx, BY + 2, sw - 2, BH - 4, 4); ctx.clip();
        ctx.globalAlpha = a * 0.9;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        const off = (t * 15) % 16;
        for (let g = -BH; g < sw + BH; g += 16) {
          ctx.beginPath();
          ctx.moveTo(sx + g + off, BY + BH); ctx.lineTo(sx + g + off + BH, BY);
          ctx.stroke();
        }
        ctx.restore();
        ctx.globalAlpha = 1;

        /* 남는 절반의 이름 — 판정이라 한국어다.
           🔴 **대괄호 아래**에 둔다(2026-08-06 검수). 전에는 막대 위 baseline 164 에
           있었고, 바로 아래(막대 안)에 흰 1,110 이 있어서 「여유 = 1,110」 으로 읽혔다.
           실제 여유는 548 이고 그 값은 원문에 없으므로 **어떤 숫자도 붙이지 않는다** —
           대괄호가 가리키는 구간이 곧 뜻이고, 라벨은 그 구간의 캡션이다.
           🔴 자리가 날 때까지 안 띄운다. 폭에 맞춰 매 프레임 줄이면 글자가 자라 보인다. */
        if (sw >= 60) {
          ctx.globalAlpha = clamp((gk - 0.58) * 3);   // 대괄호와 같이 뜬다
          ctx.textAlign = 'center';
          const fs = fit(spec.surplusLabel ?? '', 900, 16, sw - 10, 10);
          ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
          ctx.fillText(spec.surplusLabel ?? '', sx + sw / 2, 254);   // 대괄호 바닥 234 아래
          ctx.textAlign = 'left';
          ctx.globalAlpha = 1;
        }
      }

      /* 🔴 막대 밖, 막대가 끝나는 자리 바로 위. 빗금 위에 얹히지 않는다. */
      if (bw > 110) {
        ctx.globalAlpha = a;
        ctx.textAlign = 'right';
        const v = String(spec.stockValue ?? '');
        ctx.font = disp(900, 22); ctx.fillStyle = tone('ink');
        ctx.fillText(v, X0 + bw - 6, BY - 8);   // 캔버스 오른쪽 가장자리에서도 6px 안쪽
        ctx.textAlign = 'left';
        ctx.globalAlpha = 1;
      }

      /* 두 막대 사이를 계속 오가는 표식 — 라벨·숫자와 겹치지 않는 빈 띠에 둔다 */
      const u = frac(t / 2.8);
      const mx = lerp(X0 + 6, X0 + Math.max(12, bw) - 6, u < 0.5 ? u * 2 : 2 - u * 2);
      ctx.globalAlpha = a * 0.85;
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(mx, 132, 5, 0, Math.PI * 2); ctx.fill();
      ctx.globalAlpha = 1;
    }

    /* ── 두 막대가 갈리는 자리 ───────────────────── */
    if (bw > HALF + 6) {
      ctx.globalAlpha = clamp((gk - 0.4) * 3);
      ctx.setLineDash([7, 7]);
      ctx.lineDashOffset = -(t * 12) % 14;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(X0 + HALF, AY - 8); ctx.lineTo(X0 + HALF, BY + BH + 24); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;
    }

    /* ── 같은 폭 대괄호 둘 ───────────────────────── */
    const brk = (x, len, col, a) => {
      if (len < 8 || a <= 0.02) return;
      ctx.globalAlpha = a;
      ctx.strokeStyle = col; ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(x, 226); ctx.lineTo(x, 234); ctx.lineTo(x + len, 234); ctx.lineTo(x + len, 226);
      ctx.stroke();
      ctx.globalAlpha = 1;
    };
    brk(X0, Math.min(aw, HALF), tone('sub'), clamp((gk - 0.35) * 3));
    brk(X0 + HALF, Math.max(0, Math.min(bw, FULL) - HALF), tone('accent'), clamp((gk - 0.55) * 3));

    ctx.textAlign = 'left';
  }
};
