import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect
} from '../../../engine/lib.js';

/* tworules — 멈춰 선 둘 사이의 틈.

   이 편의 남는 한 줄은 "규칙이 틀렸다"가 아니라 "둘 다 옳았는데 사이가 비어 있었다"다.
   그래서 두 규칙을 나란히 세우지 않고 **위아래에서 서로를 향해 8px·16px 만큼 당겨 세운 뒤**
   맞물리기 직전에 멈춘다. 남은 22px 이 사각지대다.
   🔴 2026-08-04 검수 3-C 정정 — 여기 원래 "다가오다 멈춘다"고 적혀 있었는데 화면에서는
   그 **동작이 안 읽힌다.** approach 가 rk/0.55 라 카드가 페이드인을 마치는 순간 이동도 끝나서,
   보는 사람에게는 처음부터 그 자리에 서 있던 것과 같다. 뜻은 전달된다(멈춘 둘 사이의 틈은
   또렷하다). 그래서 그림은 그대로 두고 **말을 그림에 맞췄다** — 이 파일도, reads 도. 두 카드 모두 오른쪽에 체크와
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
   🔴 이것이 작으면 안 된다. **마지막 자막(예고) 구간에 cue 로 새로 그려지는 것이 없다** —
   dim 페이드가 한 번 내려가고 끝이라, 그 뒤의 움직임을 이 선 하나가 받친다. 반지름 4~5px
   짜리 표식이면 정적 판정 문턱 아래로 떨어진다(ep05s 가 그렇게 정적 구간 6.6초를 만들었다).
   그래서 전폭 3px 선이다. 실측 정적 구간 최대 0.4s.
   🔴 2026-08-04 정정 — 여기 원래 "그 구간에 cue 로 도는 것은 훅 카드뿐"이라고 적혀 있었다.
   훅은 이제 캔버스에 없다(아래 draw 끝 주석). 결론(검사선이 그 구간을 받친다)은 오히려
   더 맞아졌고 이유만 고쳤다. */

const SCAN = 3.4;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));

    const rk = ease(cue(spec.rulesCue ?? 0, 0.15, 0.6));
    const gk = ease(cue(spec.foundCue ?? 1, 0.15, 0.6));
    /* 🔴 이름이 dimCue 다(2026-08-04). 원래 hookCue 였고 훅 글자를 캔버스에 띄우는 데
       같이 썼는데, 훅이 아웃트로 카드로 간 뒤에는 이 값이 하는 일이 dim 하나뿐이라
       이름을 하는 일에 맞췄다. scene.json 의 spec 키도 같이 바꿨다. */
    const dk = ease(cue(spec.dimCue ?? 2, 0.15, 0.5));

    /* 마지막 자막(예고)에서 앞의 것들이 물러나고 「리뷰가 잡은 것」 두 줄만 남는다. */
    const dim = 1 - 0.65 * clamp(dk * 1.4);

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
        /* 🔴 가운데 정렬이다(2026-08-04 검수 3-F). 오른쪽 정렬로 R−8 에 두면 글자가
           점선 상자의 오른쪽 테두리(x 342, 선폭 3)에 바짝 붙어 겹쳐 보였다. 상자가
           폭 332 라 가운데면 양옆에 130px 씩 남는다 — 이 글자가 가리키는 것이
           오른쪽 끝이 아니라 **틈 전체**라, 뜻으로도 가운데가 맞다. */
        ctx.textAlign = 'center';
        const s = spec.gapLabel || '사각지대';
        const fs = fit(s, 900, 11.5, 140, 9);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
        ctx.fillText(s, (L + R) / 2, 126);
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

    /* 🔴 훅 카드는 여기서 그리지 않는다(2026-08-04 사용자 판정).
       .outrocard 가 .vis 와 좌표가 한 픽셀도 다르지 않고 배경이 불투명이라, 마지막
       OUTRO_MS(2.6초) 동안 이 캔버스는 통째로 덮인다 — 검수 실측으로 ep04s-3 은 훅이
       단 한 프레임도 보인 적이 없었고(카드 등장 35,765ms vs 예고 자막 35,776ms),
       가장 오래 보인 ep05s-1 도 100ms 였다. 훅은 engine/index.html 의 .oc-hook 이 맡아
       카드 안에서 2.6초 내내 서 있다. kind 는 그림에만 집중한다.
       🔑 "두 곳에 적으면 갈린다"가 원래 원칙이었는데 카드와 캔버스 사이에서 깨져 있었다. */
    ctx.textAlign = 'left';
  }
};
