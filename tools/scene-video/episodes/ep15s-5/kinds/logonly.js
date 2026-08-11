import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* logonly — 화면에는 걸어다니다 쓰러진 것 하나뿐인데, 로그에는 일대기가 다 적혀 있다.

   원문 근거:
   > "화면에서는 그냥 어떤 주민이 걸어다니다 쓰러졌을 뿐이고, 그가 게을러서 죽었다는
   >  사실은 콘솔 로그 안에만 있었습니다."
   > "게으름뱅이 둘은 평생 일하지 않았고(노동 2%), 집을 짓지 않았고, 늘 배고팠고
   >  (상위 목표가 여가·배고픔·간식), 겨울을 못 넘겼습니다."
   > "여섯 중 둘을 뽑는 모든 조합에서 '무엇을 주로 하며 사는가'가 달랐다는 뜻입니다."
     ← 「주로 한 것」 라벨의 근거

   🔴 **두 지표를 한 줄에 섞지 않았다.** 「노동 2%」는 비중이고 「여가·배고픔·간식」은
   상위 목표 목록이라 축이 다르다. 그래서 위는 **가로 트랙 하나**, 아래는 **칸 셋**으로
   모양 자체를 갈랐다(있는 수를 틀린 슬롯에 붙이는 사고를 모양으로 막는다).

   🔴 화면에 수를 한 개도 안 올렸다. 2% 는 **트랙 길이의 2%**(204px 중 4.1px)짜리 조각으로만
   그린다 — 자막이 「이 퍼센트」라고 부르고, 화면은 「거의 안 했다」를 길이로 말한다.

   색 규약(SH 에서 세운 것 그대로): **강조색 = 로그 안에 다 있는 것 / 흰색 = 화면에 보이는 것.**
   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **두 겹으로 갈랐다.**
     ⓐ **세로**: 흰 것은 전부 y ≤ 159(구분선 바닥), 강조색은 전부 y ≥ 169.5(「로그」 글립 꼭대기).
        10.5px 뜬다. 걸어가다 쓰러지는 표식의 최악값은 **143.9**(검수 재계산 — 반너비 7 · 반높이 17 을
        기울였을 때 √(7²+17²) = 18.38 이 최대이고 그때 중심 y 123.5 + 흔들림 1.5, 선 반폭 1.5 포함)라
        구분선(156)과 12px 뜬다. 예외가 하나 있어 ⓑ 로 따로 막았다.
     ⓑ **가로(같은 y 대역을 쓰는 유일한 곳 = 노동 트랙)**: 강조색 채움은 x 96~100.1
        (글로우 blur 8 까지 88~108), 흰 12% 점선은 **x 118 부터** 그린다. 10px 뜬다.
        왼쪽 「노동」 라벨(강조색)은 오른쪽 끝이 x 78 이라 점선과 40px 뜬다.

   ⏱ 로그 쪽은 `since(0)`, 노동·칸 셋은 `since(1)` 위에 세웠다. 마지막 칸이
   `since(1)` 1.19초에 완성되므로 그 자막 앞쪽 절반 안에서 끝난다.

   세로 예산(캔버스 307): 최저 = 칸 셋의 바닥 258. 49px 남는다.
   가로: 오른쪽 끝 300(트랙·칸), 왼쪽 끝 = 「주로 한 것」 글립 왼쪽 약 26.

   계속 도는 것(30fps 기준):
     · 표식 걷기 2.6초 루프 — 220px 을 1.82초에 → **4.0px/프레임**, 실루엣 14×34
     · 트랙 점선 흐름 55px/s → **1.8px/프레임**
     · 칸 셋 가장자리 숨 진폭 1.5px · 7.4rad/s → **0.37px/프레임** × 둘레 188px */

const DIV_Y = 156;
const TR_X0 = 96, TR_X1 = 300, TR_Y = 196, TR_H = 16;
const BOX_Y = 228, BOX_H = 30, BOX_W = 64, BOX_GAP = 6;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, since }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const ctext = (txt, x, y, weight, size, color, maxW, alpha) => {
      if (alpha <= 0.01) return;
      let fs = size; ctx.font = disp(weight, fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(weight, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.textAlign = 'center'; ctx.fillStyle = color;
      ctx.fillText(txt, clamp(x, 14 + tw / 2, w - 14 - tw / 2), y);
      ctx.restore();
    };

    const st = ease(cue(spec.screenCue ?? 0, 0.15, 0.25));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    /* ── 위 : 화면에 실제로 보이는 것 (흰색) ────────── */
    if (spec.screenLabel) {
      ctx.save();
      ctx.globalAlpha = st; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.screenLabel, 20, 66);
      ctx.restore();
    }

    const u = frac(t / (spec.walkSec ?? 2.6));
    const walk = clamp(u / 0.70);
    const fall = ease(clamp((u - 0.70) / 0.16));
    const gone = ease(clamp((u - 0.86) / 0.14));
    const wa = st * (1 - gone);
    if (wa > 0.01) {
      const wx = lerp(72, 292, walk);
      /* 걷는 동안만 위아래로 작게 흔든다 — 쓰러지면 멈춘다 */
      const bob = (1 - fall) * 2 * Math.sin(t * 9.2);
      ctx.save();
      ctx.globalAlpha = wa;
      ctx.translate(wx, lerp(121, 131, fall) + bob);
      ctx.rotate(fall * Math.PI / 2);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, -7, -17, 14, 34, 7); ctx.stroke();
      ctx.restore();
    }

    ctx.save();
    ctx.globalAlpha = st;
    ctx.fillStyle = tone('track');
    ctx.fillRect(24, DIV_Y, w - 48, 3);
    ctx.restore();

    /* ── 아래 : 로그에만 있는 것 (강조색) ───────────── */
    const logK = ease(clamp((since(spec.screenCue ?? 0) - 0.35) / 0.45));
    if (spec.logLabel && logK > 0.01) {
      ctx.save();
      ctx.globalAlpha = logK; ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.logLabel, 20, 182);
      ctx.restore();
    }

    /* 노동 — 트랙의 2% 만 찬다. 흰 12% 점선은 x 118 부터라 채움과 안 겹친다. */
    const P = 14, ph = ((-t * 55 % P) + P) % P;
    ctx.save();
    ctx.globalAlpha = logK * 0.9; ctx.fillStyle = tone('track');
    for (let x = 118 - P + ph; x < TR_X1; x += P) {
      const a = Math.max(118, x), b = Math.min(TR_X1, x + 8);
      if (b > a) ctx.fillRect(a, TR_Y + TR_H / 2 - 3, b - a, 6);
    }
    ctx.restore();

    const workK = ease(clamp((since(spec.logCue ?? 1) - 0.15) / 0.35));
    if (spec.workLabel) ctext(spec.workLabel, 52, 208, 900, 12.5, tone('accent'), 70, workK);
    if (workK > 0.01) {
      ctx.save();
      ctx.globalAlpha = workK;
      setShadow(ctx, GLOW, 8);
      ctx.fillStyle = tone('accent');
      ctx.fillRect(TR_X0, TR_Y, (TR_X1 - TR_X0) * 0.02, TR_H);
      clearShadow(ctx);
      ctx.restore();
    }

    /* 주로 한 것 — 칸 셋. 트랙과 모양이 달라 같은 축으로 안 읽힌다. */
    const items = spec.items ?? [];
    const k0 = ease(clamp((since(spec.logCue ?? 1) - 0.45) / 0.30));
    if (spec.itemsLabel) ctext(spec.itemsLabel, 52, 244, 900, 11.5, tone('accent'), 70, k0);
    for (let i = 0; i < items.length; i++) {
      const a = ease(clamp((since(spec.logCue ?? 1) - 0.45 - i * 0.22) / 0.30));
      if (a <= 0.01) continue;
      const x = TR_X0 + i * (BOX_W + BOX_GAP);
      const ins = 1.5 * (0.5 + 0.5 * Math.sin(t * 7.4 + i * 1.1));
      ctx.save();
      ctx.globalAlpha = a;
      setShadow(ctx, GLOW, 7);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, x + ins, BOX_Y + ins, BOX_W - ins * 2, BOX_H - ins * 2, 5); ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
      ctext(items[i], x + BOX_W / 2, BOX_Y + 20, 900, 12.5, tone('accent'), BOX_W - 12, a);
    }

    ctx.textAlign = 'left';
  }
};
