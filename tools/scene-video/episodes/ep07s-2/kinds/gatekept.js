import {
  disp, ease, clamp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* gatekept — 드러난 자리에 테스트가 걸린다.

   원문: "콘텐츠를 늘렸더니 그 콘텐츠가 갓 생긴 결함을 실전에서 밟아 준 셈이죠. 물론 이번에도
   그냥 고치고 넘어가지 않고, 대각선 경로가 다시 깨지는지 확인하는 검증 테스트를 함께
   걸어 뒀습니다."

   lockCue 에서 넓어진 커버 안의 결함 자리 하나에 빗장이 걸리고, 그 아래 원문 한 줄이 뜬다.
   nextCue 에서 판이 위로 미끄러지며 계절 축이 그어지고 다음 편 칩이 튀어 오른다.

   🔴 마지막 샷이라 정적 구간이 위험하다. 아웃트로 카드가 마지막 2.6초를 덮고 그 구간은
   판정에서 제외되므로 **문제는 카드가 뜨기 전**이다. 대책 둘을 함께 걸었다.
     ⓐ 커버 안을 훑는 스캔선이 2.8초 주기로 왕복한다(면적이 크다).
     ⓑ nextCue 에 면적 큰 변화 — 판 전체가 16px 미끄러지고 축이 그어지고 칩이 뜬다.
   창은 좁게(lead 0.20 · span 0.16) 잡아 변화가 카드 앞에서 끝나게 했다. */

const SCAN = 2.8;    // 스캔선 왕복 주기(초)
const LOCK = 3.3;    // 잠긴 자리 맥동 주기(초)

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

    const lk = ease(cue(spec.lockCue ?? 0, 0.15, 0.7));
    const nk = ease(cue(spec.nextCue ?? 1, 0.20, 0.16));
    const SHIFT = -16 * nk;

    const BX0 = 28, BY0 = 62 + SHIFT, BX1 = w - 28, BY1 = 196 + SHIFT;

    /* ── 커버 영역 ─────────────────────────────────── */
    ctx.textAlign = 'left';
    ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
    if (spec.coverLabel) ctx.fillText(spec.coverLabel, 8, 28);

    ctx.globalAlpha = clamp(lk * 2.4);
    ctx.setLineDash([10, 8]);
    ctx.lineDashOffset = -(t * 12) % 18;
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.rect(BX0, BY0, BX1 - BX0, BY1 - BY0); ctx.stroke();
    ctx.setLineDash([]); ctx.lineDashOffset = 0;
    ctx.globalAlpha = 1;

    /* ── 계속 도는 것 — 커버 안을 훑는 스캔선 ───────── */
    if (lk > 0.05) {
      const u = 0.5 - 0.5 * Math.cos(frac(t / SCAN) * Math.PI * 2);
      const sx = BX0 + 8 + (BX1 - BX0 - 16) * u;
      ctx.globalAlpha = clamp(lk * 2) * 0.75;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.beginPath(); ctx.moveTo(sx, BY0 + 6); ctx.lineTo(sx, BY1 - 6); ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 결함 자리 — 빗장이 걸린다 ─────────────────── */
    const FX = BX1 - 62, FY = BY1 - 44;
    const gate = ease(clamp(lk * 1.7 - 0.25));
    if (lk > 0.05) {
      const pulse = 1 + 0.12 * Math.sin(frac(t / LOCK) * Math.PI * 2);

      // 대각선 자리 자체
      ctx.globalAlpha = clamp(lk * 2.4);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(FX - 22, FY + 16); ctx.lineTo(FX + 20, FY - 18); ctx.stroke();
      ctx.globalAlpha = 1;

      if (gate > 0.02) {
        ctx.globalAlpha = gate;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
        roundRect(ctx, FX - 30 * pulse, FY - 26 * pulse, 60 * pulse, 52 * pulse, 8);
        ctx.stroke();

        setShadow(ctx, GLOW, 8, 0);
        ctx.fillStyle = tone('accent');
        ctx.beginPath(); ctx.arc(FX, FY, 5.5, 0, Math.PI * 2); ctx.fill();
        clearShadow(ctx);

        if (spec.flawLabel) {
          ctx.textAlign = 'center';
          ctx.font = disp(800, 11.5); ctx.fillStyle = tone('accent');
          /* 🔴 위로 올렸다. FY+46 은 커버 상자의 아래 테두리(BY1)와 겹쳐
             점선이 글자를 가로질렀다(스틸 실측). */
          ctx.fillText(spec.flawLabel, FX, FY - 40);
        }
        ctx.globalAlpha = 1;
      }
    }

    if (spec.testLabel) {
      const k = clamp(gate * 1.6 - 0.4);
      if (k > 0.02) {
        ctx.globalAlpha = k;
        ctx.textAlign = 'left';
        ctx.font = disp(900, 13); ctx.fillStyle = tone('accent');
        ctx.fillText(spec.testLabel, BX0 + 4, BY0 - 12);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 남는 한 줄 ────────────────────────────────── */
    if (spec.meaning) {
      const k = clamp((lk - 0.6) / 0.4);
      if (k > 0.02) {
        ctx.globalAlpha = k;
        ctx.textAlign = 'center';
        const fs = fit(spec.meaning, 900, 14, w - 30, 10);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('ink');
        ctx.fillText(spec.meaning, w / 2, 232 + SHIFT);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 예고 — 계절 축과 칩 ───────────────────────── */
    if (nk > 0.02) {
      const AXW = Math.min(300, w - 40);
      ctx.globalAlpha = nk;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.moveTo(w / 2 - AXW / 2, 262);
      ctx.lineTo(w / 2 - AXW / 2 + AXW * nk, 262);
      ctx.stroke();
      ctx.globalAlpha = 1;

      if (spec.nextLabel) {
        const rise = (1 - nk) * 10;
        ctx.globalAlpha = nk;
        ctx.textAlign = 'center';
        const fs = fit(spec.nextLabel, 900, 14, w - 34, 10);
        ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
        ctx.fillText(spec.nextLabel, w / 2, Math.min(h - 12, 284) + rise);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
