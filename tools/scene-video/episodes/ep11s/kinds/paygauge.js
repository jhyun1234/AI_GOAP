import {
  disp, ease, clamp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* paygauge — 훅 샷(4단 고정 구조 ②). 이 편이 약속하는 사건을 한 그림으로 낸다:
   **60초가 다 도는 동안 품삯이 그 자리에서 없어진다.**

   원문 근거 — '그렇게 60초가 지나면 보고 심부름은 타임아웃으로 취소됐고, 그 순간
   목수가 받아야 할 보상이 그냥 사라졌습니다.' / '열심히 집을 다 지어놓고 … 품삯이
   증발하는 겁니다.'

   🔴 **S1 과 일부러 다르게 그렸다.** 이 회차에서 60초를 재는 계기는 두 번 더 나오는데
   (S1 의 타임아웃, S4 의 「이번엔 아무것도 안 떨어진다」) 그 둘은 **가로 바**다.
   훅은 **원형 링**이다 — 같은 수(60)를 같은 라벨(`TIMEOUT 60s`)로 재지만 모양이 달라야
   훅이 본문의 예고편이 되지, 본문의 재방송이 되지 않는다.
   없어지는 방식도 다르다. S1 은 **아래로 빠지며 조각으로 흩어지고**, 여기서는
   **제자리에서 옅어진다.** 훅은 「없어졌다」만 약속하고, **어떻게·왜**는 S1 이 진다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — 시간과 좌표 양쪽으로 막았다.
   ① **시간** — 강조색 캡슐의 알파가 g 0.55~0.78 에 0 이 되고, 흰 점선 자국은 g 0.80 부터
      켜진다. 두 그림이 같은 자리를 쓰지만 **동시에 있는 순간이 없다**(0.02 여백).
   ② **좌표** — 링은 반지름 62, 캡슐은 96×30 이라 모서리까지가 50.3px 다. 12px 안 겹친다.
      링 밖 점선 원(72)과 `TIMEOUT 60s` 라벨(링 아래)도 링에 안 닿는다.

   계속 도는 것 = 링 바깥 점선 원의 회전 · 캡슐 **테두리 밖** 점선 후광의 흐름.
   🔴 후광은 캡슐 안이 아니라 밖이다 — 이 회차가 반려로 배운 것이다(빗금이 같은 색
   라벨을 훑으면 가장 읽혀야 할 글자가 가장 안 읽힌다). */

const R = 62, RING = 10;
const CAP_W = 96, CAP_H = 30;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const fit = (txt, weight, start, max, min = 8) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const cx = w / 2, cy = Math.min(h - 84, Math.max(R + 34, h * 0.46));

    const c0 = cue(spec.gaugeCue ?? 0, 0.15, 0.8);
    const g = ease(clamp(c0 / 0.86));                        // 60초가 돈다
    const fade = ease(clamp((g - 0.55) / 0.23));             // 품삯이 옅어진다
    const ghost = ease(clamp((g - 0.80) / 0.20));            // 빈 자국이 켜진다

    /* ── 링 바깥 점선 원 — 계속 돈다 ──────────────── */
    ctx.save();
    ctx.setLineDash([10, 12]);
    ctx.lineDashOffset = -(t * 34) % 22;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.arc(cx, cy, R + 14, 0, Math.PI * 2); ctx.stroke();
    ctx.restore();

    /* ── 60초 링 ──────────────────────────────────── */
    ctx.strokeStyle = tone('track'); ctx.lineWidth = RING;
    ctx.beginPath(); ctx.arc(cx, cy, R, 0, Math.PI * 2); ctx.stroke();

    if (g > 0.004) {
      const a0 = -Math.PI / 2, a1 = a0 + Math.PI * 2 * g;
      setShadow(ctx, GLOW, 14);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = RING; ctx.lineCap = 'round';
      ctx.beginPath(); ctx.arc(cx, cy, R, a0, a1); ctx.stroke();
      ctx.lineCap = 'butt';
      clearShadow(ctx);
    }

    /* ── 가운데 : 품삯 ─────────────────────────────
       g 0.55~0.78 에 제자리에서 옅어진다. 자리는 안 옮긴다(그건 S1 의 몫). */
    const capA = 1 - fade;
    if (capA > 0.01) {
      const x0 = cx - CAP_W / 2, y0 = cy - CAP_H / 2;

      /* 테두리 **밖** 후광 — 글자를 한 번도 안 지나간다 */
      ctx.save();
      ctx.globalAlpha = capA * 0.55;
      ctx.setLineDash([10, 8]);
      ctx.lineDashOffset = -(t * 24) % 18;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      /* 🔴 후광 바깥지름을 링 안쪽(반지름 57)보다 작게 묶어 뒀다:
         캡슐꼴의 최대 반경은 가로 절반이므로 (CAP_W + 8) / 2 = 52px 다. */
      roundRect(ctx, x0 - 4, y0 - 4, CAP_W + 8, CAP_H + 8, (CAP_H + 8) / 2);
      ctx.stroke();
      ctx.restore();

      ctx.save();
      ctx.globalAlpha = capA;
      setShadow(ctx, GLOW, 13);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, x0, y0, CAP_W, CAP_H, CAP_H / 2); ctx.stroke();
      clearShadow(ctx);
      if (spec.rewardLabel) {
        ctx.textAlign = 'center';
        ctx.font = disp(900, fit(spec.rewardLabel, 900, 15, CAP_W - 24, 9));
        ctx.fillStyle = tone('accent');
        ctx.fillText(spec.rewardLabel, cx, cy + 5);
      }
      ctx.restore();
    }

    /* ── 빈 자국 — 이름만 남는다 ──────────────────── */
    if (ghost > 0.01) {
      ctx.save();
      ctx.globalAlpha = ghost * 0.9;
      ctx.setLineDash([8, 7]);
      ctx.lineDashOffset = (t * 14) % 15;
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      roundRect(ctx, cx - CAP_W / 2, cy - CAP_H / 2, CAP_W, CAP_H, CAP_H / 2); ctx.stroke();
      if (spec.rewardLabel) {
        ctx.textAlign = 'center';
        /* 🔴 폭 66 · 기준선 cy+31 로 묶어 링 안쪽(57)을 안 넘게 했다 —
           가장 먼 글자 모서리까지가 sqrt(33² + 31²) = 45.3px 다. */
        ctx.font = disp(800, fit(spec.rewardLabel, 800, 12, CAP_W - 30, 9));
        ctx.fillStyle = tone('sub');
        ctx.fillText(spec.rewardLabel, cx, cy + CAP_H / 2 + 16);
      }
      ctx.restore();
    }

    /* ── 계기 이름 — 링 아래 ──────────────────────── */
    if (spec.timerLabel) {
      ctx.textAlign = 'center';
      ctx.font = disp(900, fit(spec.timerLabel, 900, 13, w - 48, 9));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.timerLabel, cx, Math.min(h - 12, cy + R + 30));
    }

    ctx.textAlign = 'left';
  }
};
