import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* samedots — 표면 위만 있다.

   원문: "제가 마을을 내려다봐도, 저 동그라미가 농사꾼인지 떠돌이인지 알 방법이 없었습니다.
   클릭해도 아무 정보가 뜨지 않았습니다."

   🔴 이 회차의 기본 도형은 **표면선**이다 — 안에서 갈라진 것이 화면 위로 올라오는가.
   네 그림이 전부 같은 선 하나(화면 프레임의 아랫변)를 두고 위/아래 관계만 바꾼다.
   여기 S1 에서는 그 선의 **위쪽만** 존재한다. 아래에 무엇이 있는지는 아직 안 보여준다 —
   그게 이 샷의 뜻이기 때문이다(보는 사람에게는 겉면밖에 없었다).

   🔴 동그라미 넷은 서로 다른 주기로 저마다 다르게 돌아다닌다(= 속은 다 다르다).
   그런데 반지름·선폭·색이 한 픽셀도 안 다르다(= 겉은 다 같다). 이 대비가 샷 전부다.
   🔴 개수 넷은 **도형 파라미터**다. 원문에 M7 시점 주민 수가 없어 화면에 숫자로 안 적었고
   자막도 인원을 한 번도 안 부른다.

   🎨 색의 뜻을 이 회차 안에서 하나로 고정한다 — **강조색 = 화면에서 읽히는 것.**
   그래서 여기서 강조색인 것은 '내가 클릭했다'는 표시(고리)뿐이고, 열린 정보줄은
   끝까지 track(빈 것)이다. */

const FY0 = 22, FY1 = 190;      // 화면 프레임
const DPAD = 22;                // 동그라미가 도는 안쪽 여백
const SY = 148, SH = 34;        // 정보줄 상자
const R = 9;                    // 동그라미 반지름 — 넷이 전부 같다

/* 궤도 상수 — S2·S3 와 **일부러 같은 값**이다(같은 마을이 이어진다는 뜻).
   공용 파일로 빼지 않고 각 kind 안에 둔다. 주기가 서로 안 맞아떨어져 배열이 반복되지 않는다.

   🔴 **넷을 2×2 칸에 가두고 흔들리는 폭을 칸 간격보다 작게 잡아, 어느 프레임에서도 서로
   안 겹치게 했다.** 처음엔 중심을 흩뿌려 놨는데 궤도 전수 스윕에서 S3 이름표가 92프레임 중
   50프레임에서 포개졌다(태그 둘이 3.6px 차이 = 화면이 「이름표 셋」으로 읽힌다). 확률을
   낮추는 조정이 아니라 **못 겹치게 만드는 상수**로 바꾼 이유가 이것이다.
   검산 — x: 중심 간격 0.58 − 흔들림 합 최대 0.26 = 0.32 × 폭 280 = **89.6px**(이름표 46px)
          y: 중심 간격 0.62 − 흔들림 합 최대 0.20 = 0.42 × 높이 84~88 = **35~37px**
             (이름표 높이 18px · 동그라미 지름 18px · S1 선택 고리 반지름 23px)
   🔑 주기(PX·PY)와 위상(PH)이 서로 다른 것은 그대로라, 넷이 저마다 다르게 움직이는 것은
   하나도 안 잃었다. 가둔 것은 **어디를 도는가**이지 **어떻게 도는가**가 아니다. */
const PX = [7.3, 9.1, 6.2, 10.4];
const PY = [5.1, 4.3, 6.7, 3.9];
const PH = [0.00, 0.37, 0.68, 0.15];
const CX = [0.20, 0.78, 0.20, 0.78];
const CY = [0.20, 0.20, 0.82, 0.82];
const AX = [0.13, 0.12, 0.12, 0.13];
const AY = [0.10, 0.09, 0.09, 0.10];

function dotAt(i, t, x0, x1, y0, y1) {
  const bw = x1 - x0, bh = y1 - y0;
  return {
    x: lerp(x0, x1, CX[i]) + bw * AX[i] * Math.sin(2 * Math.PI * (t / PX[i] + PH[i])),
    y: lerp(y0, y1, CY[i]) + bh * AY[i] * Math.cos(2 * Math.PI * (t / PY[i] + PH[i] * 1.7))
  };
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const X0 = 14, X1 = w - 14;
    const bx0 = X0 + DPAD, bx1 = X1 - DPAD, by0 = FY0 + DPAD, by1 = SY - 20;

    const sk = ease(cue(spec.screenCue ?? 0, 0.12, 0.55));   // 화면이 켜진다
    const pk = ease(cue(spec.pickCue ?? 1, 0.12, 0.70));     // 하나를 골라 본다
    const open = ease(clamp((pk - 0.30) / 0.45));            // 정보줄이 열린다

    /* ── 화면 프레임 ────────────────────────────────── */
    if (sk > 0.02) {
      ctx.globalAlpha = sk;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, X0, FY0, X1 - X0, FY1 - FY0, 8); ctx.stroke();

      ctx.textAlign = 'left';
      ctx.font = disp(800, 10.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.screenLabel ?? '', X0 + 2, FY0 - 6);
      ctx.globalAlpha = 1;

      /* 내려다보는 눈 — 계속 훑는데 아무것도 가려내지 못한다. t 의 함수라 문턱이 없다. */
      const u = frac(t / 4.6);
      const sx = lerp(X0 + 8, X1 - 8, u);
      ctx.save();
      ctx.beginPath(); ctx.rect(X0 + 3, FY0 + 3, X1 - X0 - 6, FY1 - FY0 - 6); ctx.clip();
      ctx.globalAlpha = sk * (0.18 + 0.22 * Math.sin(Math.PI * u));
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(sx, FY0 + 8); ctx.lineTo(sx, SY - 8); ctx.stroke();
      ctx.restore();
      ctx.globalAlpha = 1;
    }

    /* ── 똑같이 생긴 동그라미 넷 ─────────────────────
       서로 다른 궤도를 도는 것이 '속은 다르다', 같은 반지름·같은 선폭이 '겉은 같다'. */
    const N = spec.dots ?? 4;
    const pick = spec.pickIndex ?? 0;
    for (let i = 0; i < N; i++) {
      const a = clamp(sk * 3.2 - i * 0.5);
      if (a <= 0.02) continue;
      const p = dotAt(i, t, bx0, bx1, by0, by1);
      ctx.globalAlpha = a;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(p.x, p.y, R, 0, Math.PI * 2); ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 하나를 골라 본다 — 고리는 붙는데 ───────────── */
    if (pk > 0.02) {
      const p = dotAt(pick, t, bx0, bx1, by0, by1);
      const rr = lerp(R + 14, R + 6, ease(clamp(pk / 0.45)));
      const a = clamp(pk * 2.6);

      ctx.globalAlpha = a;
      setShadow(ctx, GLOW, 10, 0);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(p.x, p.y, rr, 0, Math.PI * 2); ctx.stroke();
      clearShadow(ctx);

      /* 고른 것에서 정보줄로 내려가는 선 — 점선이 계속 흐른다(t) */
      ctx.globalAlpha = a * 0.55;
      ctx.setLineDash([6, 6]); ctx.lineDashOffset = -(t * 14) % 12;
      ctx.beginPath(); ctx.moveTo(p.x, p.y + rr); ctx.lineTo(p.x, SY); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;
    }

    /* ── 정보줄 — 열리는데 안이 비어 있다 ─────────────
       🔴 칸을 나누지 않았다. 원문은 "클릭해도 아무 정보가 뜨지 않았습니다"이지
       "빈 칸이 세 개 있었다"가 아니다. 칸을 그리면 없던 구조를 주장하게 된다. */
    if (open > 0.02) {
      const ix = X0 + 12;
      const iw = (X1 - X0 - 24) * open;
      const a = clamp(open * 2.2);

      ctx.globalAlpha = a;
      ctx.textAlign = 'left';
      ctx.font = disp(800, 10.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.infoLabel ?? '', ix, SY - 6);

      /* 안쪽 — 흐르는 빈 줄무늬. 값이 하나도 안 들어 있다. */
      ctx.save();
      ctx.beginPath(); ctx.rect(ix + 3, SY + 3, Math.max(0, iw - 6), SH - 6); ctx.clip();
      const shift = (t * 16) % 18;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      const nS = Math.ceil(iw / 18) + 3;
      for (let k = -2; k < nS; k++) {
        const px = ix + k * 18 - shift;
        ctx.beginPath(); ctx.moveTo(px + 14, SY + 3); ctx.lineTo(px, SY + SH - 3); ctx.stroke();
      }
      ctx.restore();

      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, ix, SY, iw, SH, 6); ctx.stroke();
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
