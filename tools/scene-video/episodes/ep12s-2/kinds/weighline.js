import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, tone, setShadow, clearShadow, GLOW, GLOW_INK
} from '../../../engine/lib.js';

/* weighline — 훅 샷(4단 고정 구조 ②). **먼저 그어 둔 선 위에 값이 내려와 앉는다.**

   원문: "판정 기준은 미리 정해뒀습니다. … 30을 넘으면 \"안락 실패\"로 보고 재조정하기로요."
   / "결과는 40이었습니다."

   🔴 이 회차 그림의 축은 「선 하나와, 그 선에 걸린 값」이다. 훅은 그 축의 **가장 벌거벗은
   형태**다 — 계기도 트랙도 없이 **선 하나와 숫자 하나**뿐이다. 본문 S1(overshoot)이 같은
   말을 세로 계기와 쐐기로 하고, S3 이 눕힌 한계선으로, S4 가 두 값이 서야 할 높이로 한다.
   훅에서 계기를 지운 이유는 **훅이 「얼마나·왜」를 말하면 본문의 재방송**이 되기 때문이다.
   훅은 「값이 선 위에 남았다」까지만 약속하고, 무엇이 그 값을 못 깎았는지(재해 두 방)는
   3초 뒤 S1 이 진다.

   ① landCue — ⓐ 강조색 기준선이 **왼쪽에서 오른쪽으로 먼저 그어진다**(제목이 말하는
      「미리 정해놨거든요」가 이 순서다) → ⓑ 위에서 흰 40 이 두 레인을 타고 내려와
      선 위에 앉는다 → ⓒ 선이 그 무게에 **눌렸다가 되돌아오는데, 다 안 펴지고 눌린 채로
      남는다.** 선은 30 을 가리키고 값은 그 위에 있다.

   🔴 흰 것과 강조색을 같은 픽셀에 겹치지 않는다. 흰 것은 전부 **선 위쪽**(라벨 y 44 ·
   숫자 baseline 164), 강조색은 전부 **선과 그 아래**(선 y 208 · 눈금 214~230 · 라벨 y 252).
   ⚠️ 좌표가 아니라 **좌표 + 글로우 반경**으로 잰다 — 숫자의 흰 글로우(blur 10)가 아래로
   ~174 까지, 선의 강조색 글로우(blur 14)가 위로 ~192 까지 번지므로 18px 이 빈다.
   숫자는 처음부터 끝까지 흰색이다 — 선을 넘었다고 색을 바꾸지 않는다(위치로만 말한다).

   🔴 **원문에 값이 없는 것은 그리지 않았다.** 눈금이 없다 — 눈금을 그리면 선과 숫자
   사이의 거리가 「10」으로 읽히는데 원문은 두 수만 줬지 그 사이를 재라고 하지 않았다.
   화면에 뜨는 수는 `FAIL LINE 30` 과 `40` 둘뿐이다.

   계속 도는 것 = 낙하 레인 두 줄의 점선 흐름(세로) · 기준선 점선의 흐름(가로) ·
   숫자의 느린 상하 흔들림과 그에 맞춰 숨 쉬는 선의 눌림. */

const LINE_Y = 208;        // 강조색 기준선
const NUM_BASE = 164;      // 흰 숫자 baseline (선과 44px)
const TICK_T = 214, TICK_B = 230;
const FAIL_Y = 252;
const TOP_LABEL_Y = 44;
const DROP_FROM = 96;      // 숫자가 내려오는 거리

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

    const cx = w / 2;
    const x0 = 26, x1 = w - 26;
    const c0 = cue(spec.landCue ?? 0, 0.15, 0.8);

    const appear = ease(clamp(c0 / 0.10));
    if (appear <= 0.004) { ctx.textAlign = 'left'; return; }

    /* ⓐ 선이 먼저 그어진다 → ⓑ 숫자가 내려온다 → ⓒ 선이 눌렸다 되돌아온다 */
    const drawK = ease(clamp((c0 - 0.04) / 0.26));
    const fall = ease(clamp((c0 - 0.36) / 0.24));
    const land = ease(clamp((c0 - 0.58) / 0.28));
    const breathe = Math.sin(t * 2.3);
    const dip = fall * (lerp(21, 7, land) + breathe * 1.1);
    const numY = NUM_BASE - DROP_FROM * (1 - fall) + breathe * 1.4;

    /* ── 낙하 레인 — 자막과 무관하게 흐른다 ─────────── */
    ctx.save();
    ctx.globalAlpha = appear * 0.85;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.setLineDash([10, 9]);
    ctx.lineDashOffset = -(t * 34) % 19;
    for (const dx of [-46, 46]) {
      ctx.beginPath();
      ctx.moveTo(cx + dx, 62); ctx.lineTo(cx + dx, LINE_Y - 12);
      ctx.stroke();
    }
    ctx.restore();

    /* ── 위 라벨 (흰색) ──────────────────────────── */
    if (spec.gaugeLabel) {
      ctx.globalAlpha = appear;
      ctx.textAlign = 'center';
      ctx.font = disp(800, fit(spec.gaugeLabel, 800, 13, w - 48, 9));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.gaugeLabel, cx, TOP_LABEL_Y);
      ctx.globalAlpha = 1;
    }

    /* ── 기준선 (강조색) — 왼쪽에서 오른쪽으로 그어지고, 가운데가 눌린다 ── */
    if (drawK > 0.01) {
      const xEnd = lerp(x0, x1, drawK);
      const spread = 72;
      const sagAt = x => {
        const k = clamp(1 - Math.abs(x - cx) / spread);
        return dip * (k * k * (3 - 2 * k));
      };
      ctx.save();
      ctx.globalAlpha = appear;
      setShadow(ctx, GLOW, 14);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.setLineDash([13, 8]);
      ctx.lineDashOffset = -(t * 26) % 21;
      ctx.beginPath();
      ctx.moveTo(x0, LINE_Y + sagAt(x0));
      for (let x = x0 + 4; x < xEnd; x += 4) ctx.lineTo(x, LINE_Y + sagAt(x));
      ctx.lineTo(xEnd, LINE_Y + sagAt(xEnd));
      ctx.stroke();
      clearShadow(ctx);
      ctx.restore();

      /* 선이 가리키는 자리 — 짧은 세로 눈금 하나(값이 아니라 위치 표시다) */
      if (drawK > 0.55) {
        ctx.save();
        ctx.globalAlpha = appear * ease(clamp((drawK - 0.55) / 0.45));
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(cx, TICK_T + dip); ctx.lineTo(cx, TICK_B + dip);
        ctx.stroke();
        ctx.restore();
      }
    }

    /* ── 값 (흰색) — 위에서 내려와 선 위에 앉는다 ───── */
    if (fall > 0.004 && spec.endLabel) {
      const nfs = fit(spec.endLabel, 900, 62, w * 0.46, 22);
      ctx.save();
      ctx.globalAlpha = appear * clamp(fall * 2.2);
      setShadow(ctx, GLOW_INK, 10);
      ctx.textAlign = 'center';
      ctx.font = disp(900, nfs);
      ctx.fillStyle = tone('ink');
      ctx.fillText(spec.endLabel, cx, numY);
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 기준 이름 (강조색) — 선 아래 ───────────────── */
    if (spec.failLabel && drawK > 0.3) {
      ctx.save();
      ctx.globalAlpha = appear * ease(clamp((drawK - 0.3) / 0.4));
      ctx.textAlign = 'center';
      ctx.font = disp(900, fit(spec.failLabel, 900, 15, w - 60, 10));
      ctx.fillStyle = tone('accent');
      ctx.fillText(spec.failLabel, cx, FAIL_Y + dip);
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
