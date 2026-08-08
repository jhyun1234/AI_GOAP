import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* nextpeek — 아웃트로 샷(4단 고정 구조 ④). 다음 편 기능의 0.5초 테스트 장면을 루프로 돌린다.

   무엇을 보여 주나 — **다음 편(11편)이 여는 사건**이다. 근거는 이 회차의 원문이 아니라
   **11편 원본 글**(`2026-07-22-unity-goap-debt-settlement-pathfinder-boundary.html`)이다:
   '목수 주민이 의뢰인의 집을 다 지으면, 다 됐다고 알리러 의뢰인에게 걸어갑니다.
   문제는 그 의뢰인이 가만히 서 있질 않는다는 겁니다. … 하지만 따라잡히지 않았습니다.
   그렇게 60초가 지나면 보고 심부름은 타임아웃으로 취소됐고, 그 순간 목수가 받아야 할
   보상이 그냥 사라졌습니다.'
   🔴 이 편 원문 맨 끝의 '다음 이야기 예고' 문단(공동 밭·재해)은 **블로그의 다음 글**이라
      쳐다보지 않았다. 그 습관이 ep02s 사고를 냈다.
   🔴 해결(빚으로 적어두기)은 안 그린다. 예고가 답까지 주면 다음 편을 볼 이유가 없다.

   그래서 0.5초 루프에서 도는 것은 이것 하나다 — **목수와 주민이 같은 속도로 걸어서
   간격이 한 번도 안 좁혀지고, 그러는 사이 품삯만 사라진다.** 둘 사이의 흰 캘리퍼는
   길이가 처음부터 끝까지 똑같다(값을 안 적는다 — 60초도 거리도 화면에 없다).

   🔤 도형 어휘는 이 회차 것을 그대로 쓴다 — **목수 = 이중 고리 · 주민 = 흰 점.**
   🔴 다만 **강조색이 여기서만 「품삯」을 가리킨다.** 본문 네 그림에서 강조색은 「부탁」
      하나였고 그 규칙은 안 건드렸다. 이 그림은 **다음 편의 축** 위에 있고, 그 편의 주인공이
      품삯이다. 그리고 그 강조색이 루프마다 사라지는 것이 이 미리보기의 전부다.

   ⏱ 등장(미리보기 창이 열리는 것)은 예고 자막의 cue(0)에 물린다. 0.5초 루프만 t 다.
   🔴 **소개 자막(cue 1)에는 아무것도 안 건다.** 마지막 3.0초는 아웃트로 카드가 이 캔버스를
      픽셀 동일 좌표로 덮으므로 그 구간에 그린 것은 한 프레임도 안 보인다(ep10s-2 실측:
      카드가 소개 줄보다 183ms 먼저 떴다). 보여 줄 것은 전부 예고 자막 구간에서 끝난다. */

const LOOP = 0.5;        // 테스트 장면 한 바퀴(초) — 사용자 지시의 「0.5초 미리보기」
const GAP = 84;          // 절대 안 좁혀지는 간격(px)
const RUN = 62;          // 한 바퀴에 둘이 함께 나아가는 거리(px)

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

    /* 🔴 cue 는 이것 하나뿐이다(위 주석 참조). */
    const ak = ease(cue(spec.peekCue ?? 0, 0.15, 0.42));

    const PX = 20, PW = w - 40;
    const PY = 46, PH = h - PY - 60;
    const gy = PY + PH - 34;                 // 미리보기 속 바닥

    /* ── 상단 라벨 ────────────────────────────────── */
    if (spec.label) {
      ctx.textAlign = 'left';
      ctx.font = disp(800, fit(spec.label, 800, 12.5, PW * 0.5, 9));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.label, 20, PY - 14);
    }

    if (ak <= 0.02) { ctx.textAlign = 'left'; return; }

    /* ── 미리보기 창 — 중립 테두리. 열리면서 세로로 펴진다 ── */
    const open = lerp(0.60, 1, ak);
    const oh = PH * open, oy = PY + (PH - oh) / 2;
    ctx.save();
    ctx.globalAlpha = clamp(ak * 1.6);
    ctx.setLineDash([12, 9]);
    ctx.lineDashOffset = -(t * 11) % 21;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    roundRect(ctx, PX, oy, PW, oh, 10); ctx.stroke();
    ctx.restore();

    if (ak < 0.55) { ctx.textAlign = 'left'; return; }   // 다 열린 뒤에 장면이 든다
    const inner = clamp((ak - 0.55) / 0.45);

    /* 바닥 — 계속 흐른다 */
    ctx.save();
    ctx.globalAlpha = inner;
    ctx.setLineDash([9, 8]);
    ctx.lineDashOffset = -(t * 18) % 17;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(PX + 14, gy); ctx.lineTo(PX + PW - 14, gy); ctx.stroke();
    ctx.restore();

    /* ── 0.5초 루프 ────────────────────────────────
       목수(cx)와 주민(vx)이 **같은 값**만큼 나아간다 → 간격은 상수 GAP 이다. */
    const ph = frac(t / LOOP);
    const go = ph * RUN;
    const fade = 1 - ease(clamp((ph - 0.88) / 0.12));      // 한 바퀴 끝에서 장면이 리셋된다
    const cx = Math.min(PX + 46 + go, PX + PW - GAP - 34);
    const vx = cx + GAP;
    const my = gy - 15;

    ctx.globalAlpha = inner * fade;

    /* 목수 = 이중 고리(이 회차의 어휘) */
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.arc(cx, my, 11, 0, Math.PI * 2); ctx.stroke();
    ctx.save();
    ctx.setLineDash([7, 6]);
    ctx.lineDashOffset = -(t * 16) % 13;
    ctx.beginPath(); ctx.arc(cx, my, 17, 0, Math.PI * 2); ctx.stroke();
    ctx.restore();

    /* 주민 = 흰 점 */
    ctx.fillStyle = tone('ink');
    ctx.beginPath(); ctx.arc(vx, my, 7, 0, Math.PI * 2); ctx.fill();

    /* 안 좁혀지는 간격 — 흰 캘리퍼. 길이가 처음부터 끝까지 같다 */
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(cx + 21, gy + 13); ctx.lineTo(vx - 11, gy + 13);
    ctx.moveTo(cx + 21, gy + 8); ctx.lineTo(cx + 21, gy + 18);
    ctx.moveTo(vx - 11, gy + 8); ctx.lineTo(vx - 11, gy + 18);
    ctx.stroke();

    /* ── 품삯 — 목수가 들고 가다 루프 후반에 사라진다 ──
       🔴 강조색과 흰색을 같은 픽셀에 겹치지 않는다: 캡슐은 목수 고리보다 위(−44)에 있고
          고리의 바깥 반지름 17 과 캡슐 아래변 사이가 16px 떨어진다. */
    const goneK = ease(clamp((ph - 0.52) / 0.30));
    const pa = inner * fade * (1 - goneK);
    if (pa > 0.02 && spec.pay) {
      const pw = 66, phh = 22, pxx = Math.max(PX + 12, Math.min(cx - pw / 2, PX + PW - 12 - pw));
      const py = my - 44;
      ctx.globalAlpha = pa;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, pxx, py - phh / 2, pw, phh, phh / 2); ctx.stroke();
      ctx.textAlign = 'center';
      ctx.font = disp(900, fit(spec.pay, 900, 11, pw - 16, 8));
      ctx.fillStyle = tone('accent');
      ctx.fillText(spec.pay, pxx + pw / 2, py + 4);
    }
    ctx.globalAlpha = 1;

    /* 무슨 기능인지 — 바닥 아래 */
    if (spec.feature) {
      ctx.globalAlpha = inner;
      ctx.textAlign = 'center';
      ctx.font = disp(800, fit(spec.feature, 800, 13, PW * 0.62, 9));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.feature, PX + PW / 2, gy + 52);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
