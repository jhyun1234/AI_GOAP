import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* nextpeek — 아웃트로 샷(4단 고정 구조 ④). 다음 편 기능의 0.5초 테스트 장면을 루프로 돌린다.

   무엇을 보여 주나 — **다음 편(10-3편)이 지우는 그 「편한 기능」**이다.
   근거는 같은 원문 글의 '완료 선언 후에 되돌아간 것들' 절 둘째 문단이다:
   '원래는 집이 부족하면 목수가 알아서 집을 짓고, 빈집이 생기면 무주택 주민에게
   자동으로 배정되게 해뒀습니다. 편리했죠. 그런데 이 편리함이 부탁 시스템의 존재
   이유를 통째로 갉아먹고 있었습니다.'
   🔴 원문 맨 끝의 '다음 이야기 예고' 문단(공동 밭·재해)은 **블로그의 다음 글**이지
      시리즈의 다음 편이 아니다. 그 문단은 쳐다보지 않았다.

   그래서 화면에서 도는 것은 이것 하나다 — **빈 택지 위에 집이 저 혼자 지어진다.
   아무도 부탁하지 않았는데 계속.** 옆에 선 주민의 말풍선 자리는 끝까지 비어 있다.
   자막은 「지운다」를 말하고 화면은 「무엇을」을 보인다 — 서로 다른 것을 진다.

   🔴 도형 문법은 이 회차 것 그대로다. 자율 건설은 **내가 붙인 편의**라서 각진 흰색이고,
      **강조색은 이 그림에 한 번도 안 나온다** — 자율 건설은 GOAP 이 한 일이 아니기 때문이다.
      이 회차에서 강조색이 0인 유일한 그림이고, 그것이 「그래서 다음 편에서 지워진다」의 근거다.
      (초안에는 맨 아래 이음 바에 강조색을 썼는데 그 요소를 지우면서 0이 됐다.)

   ⏱ 타이밍 — 이 회차에서 유일하게 t 로 도는 **주기 동작**이 여기 있다(LOOP 0.5초).
      단 그것은 「사건」이 아니라 **테스트 장면이 반복 재생되는 것 자체**이고,
      **등장(preview 창이 열리는 것)은 cue 0(예고 자막)에 물려 있다.** 즉 자막이
      부르기 전에는 아무것도 안 보이고, 부른 뒤부터 루프가 보인다.

   🔴 마지막 3.0초는 아웃트로 카드가 이 캔버스를 픽셀 동일 좌표로 덮는다.
      **소개 자막(cue 1) 구간은 카드가 통째로 덮는다** — 실측으로 카드가 소개 줄보다
      **183ms 먼저** 뜬다(카드 33,816ms · 소개 줄 시작 33,999ms). 즉 그 구간에 그린 것은
      **한 프레임도 안 보인다.**
      🔴 초안에는 소개 줄에 거는 이음 바(`linkCue`)가 있었는데 **실측 0프레임이라 지웠다.**
      span 을 0.14 로 조인 것도 소용이 없었다 — 창을 아무리 좁혀도 시작점이 카드 뒤였다.
      🔑 그래서 규칙은 「카드 전에 완성하라」가 아니라 **「소개 줄에는 아무것도 걸지 마라」**다.
      **보여 줄 것은 전부 예고 자막(cue 0) 구간에서 끝난다.** 그 구간에 0.5초 루프가
      계속 돌고 있으므로 정적도 나지 않는다. */

const LOOP = 0.5;       // 테스트 장면 한 바퀴(초) — 사용자 지시의 「0.5초 미리보기」

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

    /* 🔴 cue 는 이것 하나뿐이다. 소개 자막(cue 1)에 거는 것이 없다 —
       그 구간은 아웃트로 카드가 통째로 덮는다(카드가 183ms 먼저 뜬다, 위 주석 참조). */
    const ak = ease(cue(spec.peekCue ?? 0, 0.15, 0.42));   // 미리보기 창이 열린다

    const PX = 20, PW = w - 40;
    const PY = 46, PH = h - PY - 62;
    const gy = PY + PH - 30;                                // 미리보기 속 바닥
    const lx = PX + PW * 0.34;                              // 택지 한가운데
    const vx = PX + PW * 0.76;                              // 옆에 선 주민

    /* ── 상단 라벨 ────────────────────────────────── */
    if (spec.label) {
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.label, 12, PY - 14);
    }

    if (ak <= 0.02) { ctx.textAlign = 'left'; return; }

    /* ── 미리보기 창 — 중립 테두리(track). 열리면서 세로로 펴진다 ── */
    const open = lerp(0.62, 1, ak);
    const oh = PH * open, oy = PY + (PH - oh) / 2;
    ctx.save();
    ctx.globalAlpha = clamp(ak * 1.6);
    ctx.setLineDash([12, 9]);
    ctx.lineDashOffset = -(t * 11) % 21;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    roundRect(ctx, PX, oy, PW, oh, 10); ctx.stroke();
    ctx.restore();

    if (ak < 0.55) { ctx.textAlign = 'left'; return; }      // 다 열린 뒤에 장면이 든다
    const inner = clamp((ak - 0.55) / 0.45);

    ctx.globalAlpha = inner;

    /* 바닥 — 계속 흐른다 */
    ctx.save();
    ctx.setLineDash([9, 8]);
    ctx.lineDashOffset = -(t * 16) % 17;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(PX + 14, gy); ctx.lineTo(PX + PW - 14, gy); ctx.stroke();
    ctx.restore();

    /* 빈 택지 — 각진 흰 점선. 집이 사라져도 이 자리는 남는다 */
    ctx.save();
    ctx.setLineDash([7, 6]);
    ctx.lineDashOffset = (t * 10) % 13;
    ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.rect(lx - 34, gy - 7, 68, 14); ctx.stroke();
    ctx.restore();

    /* ── 0.5초 루프: 집이 저 혼자 올라간다 ────────── */
    const ph = frac(t / LOOP);
    const bp = clamp(ph / 0.62);
    const wk = ease(clamp(bp / 0.55));                       // 벽이 올라온다
    const rk = ease(clamp((bp - 0.55) / 0.45));              // 지붕이 얹힌다
    const gone = ease(clamp((ph - 0.86) / 0.14));            // 사라지고 다시 처음부터

    if (wk > 0.02 && gone < 0.98) {
      ctx.save();
      ctx.globalAlpha = inner * (1 - gone);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;

      const wh = 34 * wk;
      ctx.beginPath(); ctx.rect(lx - 26, gy - 8 - wh, 52, wh); ctx.stroke();

      if (rk > 0.02) {
        const apex = lerp(gy - 42, gy - 64, rk);
        ctx.globalAlpha = inner * (1 - gone) * clamp(rk * 1.8);
        ctx.beginPath();
        ctx.moveTo(lx - 33, gy - 42);
        ctx.lineTo(lx, apex);
        ctx.lineTo(lx + 33, gy - 42);
        ctx.closePath(); ctx.stroke();
      }
      ctx.restore();
    }

    /* 기능 이름 — 택지 아래 */
    if (spec.feature) {
      ctx.globalAlpha = inner;
      ctx.textAlign = 'center';
      const fs = fit(spec.feature, 800, 13, PW * 0.44, 9);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.feature, lx, gy + 24);
    }

    /* ── 옆에 선 주민 — 말풍선 자리가 끝까지 비어 있다 ── */
    ctx.globalAlpha = inner;
    ctx.fillStyle = tone('ink');
    ctx.beginPath(); ctx.arc(vx, gy - 9, 8, 0, Math.PI * 2); ctx.fill();

    ctx.save();
    ctx.globalAlpha = inner;
    ctx.setLineDash([6, 6]);
    ctx.lineDashOffset = (t * 9) % 12;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    roundRect(ctx, vx - 19, gy - 52, 38, 22, 11); ctx.stroke();
    ctx.restore();
    ctx.globalAlpha = 1;

    ctx.textAlign = 'left';
  }
};
