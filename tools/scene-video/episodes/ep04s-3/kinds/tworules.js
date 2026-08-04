import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, spring
} from '../../../engine/lib.js';

/* tworules — 다가오다 멈춘 둘 사이의 틈.

   이 편의 남는 한 줄은 "규칙이 틀렸다"가 아니라 "둘 다 옳았는데 사이가 비어 있었다"다.
   그래서 두 규칙을 나란히 세우지 않고 **위아래에서 서로에게 다가오게** 한 뒤,
   맞물리기 직전에 멈춰 세운다. 남은 22px 이 사각지대다. 두 카드 모두 오른쪽에 체크와
   「옳다」가 붙어 있어서, 화면에서 잘못을 지목당하는 것이 카드가 아니라 그 사이라는 것이
   한 번에 읽힌다(rulesCue).

   foundCue 는 이 편의 두 사건을 한 목록에 넣는 자리다. E6(피로 사각지대)과 E5(명령
   목표값이 절대값)는 인과로 이어지지 않는다 — 원문이 둘을 같은 M1-C 리뷰에 매달았을 뿐이다.
   그래서 화면도 인과선을 긋지 않고 **한 목록에 두 줄**로만 둔다. 없는 인과를 그리는 것이
   지어내는 것과 같다.

   🔴 이 리포의 어느 kind 도 "두 요소가 다가오다 멈춰 틈을 남기는" 그림을 쓰지 않는다.
   비슷해 보이는 것들과의 차이: ep04s-3 S1 blindspot 은 가로 축 위의 빈 자리이고(축이 있다),
   S2 relative 는 나란한 막대 둘의 길이 차이다(둘이 마주 보지 않는다). 여기서 도형은
   마주 보는 것이 아니라 **못 만나는 것**이다.

   계속 도는 것 = 카드 위를 위에서 아래로 훑는 검사선(주기 3.4초, 폭 332).
   🔴 이것이 작으면 안 된다. 마지막 자막(예고)에는 cue 로 도는 것이 훅 카드뿐이라, 그 구간의
   움직임을 이 선 하나가 받친다. 반지름 4~5px 짜리 표식이면 정적 판정 문턱 아래로 떨어진다
   (ep05s 가 그렇게 정적 구간 6.6초를 만들었다). 그래서 전폭 3px 선이다. */

const SCAN = 3.4;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, scene, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));

    const rk = ease(cue(spec.rulesCue ?? 0, 0.15, 0.6));
    const gk = ease(cue(spec.foundCue ?? 1, 0.15, 0.6));
    const hk = ease(cue(spec.hookCue ?? 2, 0.15, 0.5));

    /* 마지막 자막(예고)에서 앞의 것들이 물러나고 한 줄만 남는다.
       훅 카드가 올라오는 것과 같은 cue 에서 갈라진다 — 문턱을 다른 cue 에서 만들지 않는다. */
    const dim = 1 - 0.65 * clamp(hk * 1.4);

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const L = 10, R = w - 10, CW = R - L;

    /* ── M1-C 리뷰 칩 ─────────────────────────────── */
    /* S1 blindspot · S2 relative 와 같은 자리·같은 모양이다. 세 샷에 같은 도장을 찍는 것이
       이 편의 묶음이다 — 두 사건을 잇는 것이 이 이름 하나뿐이다. */
    if (spec.review) {
      const k = clamp(rk * 2.4) * dim;
      if (k > 0.02) {
        ctx.globalAlpha = k;
        const fs = fit(spec.review, 800, 12, w - 60, 9);
        ctx.font = disp(800, fs);
        const tw = ctx.measureText(spec.review).width;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        roundRect(ctx, 8, 14, tw + 20, 26, 3); ctx.stroke();
        ctx.textAlign = 'left';
        ctx.fillStyle = tone('accent');
        ctx.fillText(spec.review, 18, 32);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 규칙 카드 둘 ─────────────────────────────── */
    const CH = 56;
    const approach = ease(clamp(rk / 0.55));
    const alpha = clamp(rk * 1.8) * dim;
    const tops = [lerp(46, 54, approach), lerp(148, 132, approach)];
    const rules = (spec.rules || []).slice(0, 2);
    const okLabel = spec.okLabel || '옳다';

    rules.forEach((r, i) => {
      const ty = tops[i];
      if (alpha <= 0.02) return;
      ctx.globalAlpha = alpha;

      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, L, ty, CW, CH, 4); ctx.stroke();

      // 오른쪽에 체크 + "옳다" — 지목당하는 것이 카드가 아니라 그 사이라는 표시
      ctx.textAlign = 'right';
      ctx.font = disp(800, 11); ctx.fillStyle = tone('accent');
      const ow = ctx.measureText(okLabel).width;
      ctx.fillText(okLabel, R - 8, ty + 30);
      const cx = R - 8 - ow - 10, cy = ty + 26;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(cx - 10, cy - 1); ctx.lineTo(cx - 5, cy + 4); ctx.lineTo(cx, cy - 7);
      ctx.stroke();

      ctx.textAlign = 'left';
      const s = r.text || '';
      const fs = fit(s, 800, 13, CW - 24 - ow - 34, 8.5);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(s, L + 12, ty + 26);

      if (r.note) {
        const fs2 = fit(r.note, 700, 10, 200, 8);
        ctx.font = disp(700, fs2); ctx.fillStyle = tone('sub');
        ctx.fillText(r.note, L + 12, ty + 46);
      }
      ctx.globalAlpha = 1;
    });

    /* ── 다물리지 않은 틈 ─────────────────────────── */
    if (rk > 0.5) {
      const k = clamp((rk - 0.5) / 0.4) * dim;
      if (k > 0.02) {
        ctx.globalAlpha = k;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        ctx.setLineDash([7, 6]);
        roundRect(ctx, L, 112, CW, 18, 3); ctx.stroke();
        ctx.setLineDash([]);
        ctx.textAlign = 'right';
        const s = spec.gapLabel || '사각지대';
        const fs = fit(s, 900, 11.5, 140, 9);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
        ctx.fillText(s, R - 8, 126);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 리뷰가 잡은 것 ───────────────────────────── */
    const found = (spec.found || []).slice(0, 2);
    if (gk > 0.05 && found.length) {
      const hk2 = clamp(gk * 2) * dim;
      if (hk2 > 0.02 && spec.foundLabel) {
        ctx.globalAlpha = hk2;
        ctx.textAlign = 'left';
        ctx.font = disp(800, 10.5); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.foundLabel, 12, 208);
        ctx.globalAlpha = 1;
      }
      found.forEach((s, i) => {
        const born = clamp(gk * 2.0 - i * 0.5);
        if (born < 0.02) return;
        const y = 228 + i * 20;
        ctx.globalAlpha = clamp(born * 1.6) * dim;

        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        roundRect(ctx, 12, y - 11, 13, 13, 2); ctx.stroke();

        // 체크 획 — born 으로 자란다. 두 항목이 0.5 만큼 어긋나 차례로 찍힌다.
        const g = clamp((born - 0.35) / 0.45);
        if (g > 0.02) {
          const a = Math.min(1, g * 2), b = Math.max(0, (g - 0.5) * 2);
          ctx.beginPath();
          ctx.moveTo(15, y - 5); ctx.lineTo(15 + 3.5 * a, y - 5 + 3.5 * a);
          if (b > 0) { ctx.moveTo(18.5, y - 1.5); ctx.lineTo(18.5 + 5 * b, y - 1.5 - 5 * b); }
          ctx.stroke();
        }

        ctx.textAlign = 'left';
        const fs = fit(s, 800, 12.5, R - 34, 9);
        ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
        ctx.fillText(s, 34, y);
        ctx.globalAlpha = 1;
      });
    }

    /* ── 계속 훑는다 ──────────────────────────────── */
    /* dim 을 곱하지 않는다. 예고 자막 구간에서도 이 선만은 원래 밝기로 계속 돌아야
       화면이 멈추지 않는다(위 머리주석 참조). */
    {
      const sy = lerp(48, 198, frac(t / SCAN));
      ctx.globalAlpha = 0.3;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(L, sy); ctx.lineTo(R, sy); ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 남는 한 줄 ───────────────────────────────── */
    /* 예고 자막에 물려 있다. 예고 자막엔 cue 가 없어 그냥 두면 그 구간이 통째로 멈춘다.
       🟡 아웃트로 카드(engine.js OUTRO_MS 2600)가 마지막 2.6초에 .vis 를 덮으므로 이 카드는
       뜨자마자 그 뒤로 들어간다. 그래도 여기에 둔 이유는 이 샷의 첫 자막이 같은 문장을 이미
       소리로 준다는 것과, 정적 구간을 막는 것이 화면이 사는 유일한 조건이라는 것 둘이다. */
    const hook = spec.hook || scene?.hook;
    if (hook && hk > 0.02) {
      const k = clamp(hk * 1.6);
      const s2 = Math.min(1, spring(k));
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(String(hook), 900, 15, w - 30, 10);
      ctx.font = disp(900, fs * s2); ctx.fillStyle = tone('ink');
      ctx.fillText(String(hook), w / 2, 292);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
