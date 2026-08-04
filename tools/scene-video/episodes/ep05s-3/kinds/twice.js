import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* twice — 떨어져 있던 자국 둘이 가운데로 모여 정확히 포개진다.

   이 편의 결론은 '코드가 0줄이었다'가 아니라 '같은 일이 두 번 일어났다'이다. 그래서
   화면이 세는 것은 줄 수가 아니라 자국의 겹이다. firstCue 에서 왼쪽 자리에 자국 하나가
   눌려 찍히고(요리), secondCue 에서 오른쪽 자리에 두 번째 자국이 찍힌 뒤 **둘이 가운데로
   미끄러져 포개진다**(집에서 쉬기). 겹치는 순간 테두리가 두 겹이 되고 안쪽에 패턴 이름이
   남는다. meanCue 에서 자국 아래로 차단선이 그어진다 — 원문 6절의 "구조적으로 차단됐다".

   🔴 좌우로 벌렸다가 모으는 구성은 앞 샷 pledge 를 받은 것이다. 저기서 종이 아래 도장
   자리가 좌우 두 칸이었고, 그 두 칸이 여기서 하나로 합쳐진다 — '두 번이 사실 같은 하나였다'가
   이 회차의 논지이자 이 도형의 마지막 상태다. 두 번째 자국이 위에서 내려와 감싸는 초고 방식은
   그 좌우 배치와 이어지지 않아 버렸다.

   🔴 편집기도 카운터도 없다. '에셋 1개, 코드 0줄'은 움직이는 숫자가 아니라 **자국 안에
   찍혀 있는 이름**으로만 나온다 — 그 이름을 두 번 받아 낸 것이 이 샷의 사건이다.

   🔴 훅은 이 kind 가 그리지 않는다(2026-08-04 사용자 판정). 원래는 meanCue 에 물려
   있었다 — 아웃트로 카드(OUTRO_MS 2600)가 마지막 자막 시작 +0.6초부터 .vis 를 통째로
   덮어서, 예고 자막에 걸면 훅을 0.6초만 보기 때문이었다. 검수가 여섯 편을 실측하니
   ep04s-3 은 −0.01초로 **뜨기 전에 덮였고** 다섯 편의 「남는 한 줄」이 사실상 화면에
   없었다. 그래서 훅을 engine/index.html 의 .oc-hook 으로 올렸다 — 카드 안이라 2.6초
   내내 서 있고, 이 kind 는 그림에만 집중한다.
   🔑 예고 자막의 좁은 창(span 0.18) 다음 편 칩은 그대로 둔다. 카드가 덮기 전에 뜨고,
   예고 구간에도 cue 로 움직이는 것이 하나 남아야 하기 때문이다.

   계속 도는 것 = 겹친 자국의 맥동(주기 3.4초·±1.6px, 둘레 484px 가 움직인다),
   테두리를 도는 표식(r 5.5), 겹치기 전에는 아직 빈 자리의 점선 흐름.
   🔴 표식만으로는 부족하다는 것이 이미 관측됐다(r 4.5 는 판정 문턱 0.0008 아래였다).
   맥동은 프레임당 최대 0.59px × 둘레 484px ≈ 570px² 로 문턱의 약 6배다. 둘 다 지울 것. */

const ORBIT = 5.2;
const PULSE = 3.4;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, scene, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));

    const fk = ease(cue(spec.firstCue ?? 0, 0.15, 0.6));
    const mk = ease(cue(spec.secondCue ?? 1, 0.15, 0.72));
    const nk = ease(cue(spec.meanCue ?? 2, 0.15, 0.6));
    const xk = ease(cue(spec.nextCue ?? 3, 0.15, 0.18));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const IW = 132, IH = 78, OW = 150, OH = 92;
    const MIDY = 135;
    const LX = 100, RX = 252;                      // 떨어져 있을 때의 두 자리
    const merge = ease(clamp((mk - 0.45) / 0.45)); // 0.9 에서 포개짐 완성 (sfx 근거)

    const icx = lerp(LX, w / 2, merge), iy = MIDY - IH / 2;
    const ocx = lerp(RX, w / 2, merge), oy = MIDY - OH / 2;

    /* 계속 도는 것 ① — 포개진 뒤의 맥동.
       ramp 를 곱해 0 에서 부드럽게 붙는다(문턱에서 툭 튀면 그 자체가 결함으로 보인다). */
    const pulse = 1.6 * Math.sin(frac(t / PULSE) * Math.PI * 2) * clamp((merge - 0.82) / 0.18);

    /* 계속 도는 것 ② — 테두리를 도는 표식 */
    const orbit = (x, y, ww, hh, col, alpha) => {
      const per = 2 * (ww + hh);
      let d = frac(t / ORBIT) * per, mx = x, my = y;
      if (d < ww) { mx = x + d; my = y; }
      else if (d < ww + hh) { mx = x + ww; my = y + (d - ww); }
      else if (d < 2 * ww + hh) { mx = x + ww - (d - ww - hh); my = y + hh; }
      else { mx = x; my = y + hh - (d - 2 * ww - hh); }
      ctx.globalAlpha = alpha;
      setShadow(ctx, GLOW, 10, 0);
      ctx.fillStyle = col;
      ctx.beginPath(); ctx.arc(mx, my, 5.5, 0, Math.PI * 2); ctx.fill();
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    };

    /* 계속 도는 것 ③ — 아직 안 찍힌 자리의 점선 */
    const slot = (cx0, ww, hh, alpha) => {
      if (alpha <= 0.02) return;
      ctx.globalAlpha = alpha;
      ctx.setLineDash([9, 7]);
      ctx.lineDashOffset = -(t * 13) % 16;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, cx0 - ww / 2, MIDY - hh / 2, ww, hh, 4); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;
    };
    slot(LX, IW, IH, (1 - clamp(fk * 1.6)) * 0.9);
    slot(RX, OW, OH, (1 - clamp(mk * 1.6)) * 0.9);

    /* ── 첫 자국 ──────────────────────────────────── */
    if (fk > 0.02) {
      const press = ease(clamp(fk / 0.7));
      const dy = lerp(-14, 0, press);
      const s = Math.min(1, spring(clamp(fk * 1.2)));
      ctx.globalAlpha = clamp(fk * 2);
      ctx.lineWidth = 4; ctx.strokeStyle = tone('ink');
      roundRect(ctx, icx - IW * s / 2, iy + dy + (IH - IH * s) / 2, IW * s, IH * s, 4);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* ── 두 번째 자국 ─────────────────────────────── */
    if (mk > 0.02) {
      const press = ease(clamp(mk / 0.45));
      const dy = lerp(-14, 0, press);
      const s = Math.min(1, spring(clamp(mk * 1.6)));
      ctx.globalAlpha = clamp(mk * 2);
      ctx.lineWidth = 4; ctx.strokeStyle = tone('accent');
      if (merge > 0.85) setShadow(ctx, GLOW, 16, 0);
      roundRect(ctx,
        ocx - OW * s / 2 - pulse,
        oy + dy + (OH - OH * s) / 2 - pulse * 0.6,
        OW * s + pulse * 2,
        OH * s + pulse * 1.2, 5);
      ctx.stroke();
      clearShadow(ctx);
      ctx.globalAlpha = 1;
      if (merge > 0.35) {
        orbit(ocx - OW / 2 - pulse, oy - pulse * 0.6, OW + pulse * 2, OH + pulse * 1.2,
          tone('accent'), clamp(mk * 2) * 0.95);
      }
    }

    /* ── 겹친 자리에 남는 이름 ────────────────────── */
    if (merge > 0.5 && spec.pattern) {
      const k = clamp((merge - 0.5) / 0.4);
      const s = Math.min(1, spring(k));
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.pattern, 900, 18, IW - 16, 11);
      ctx.font = disp(900, fs * s); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.pattern, w / 2, MIDY + 6);
      ctx.globalAlpha = 1;
    }

    /* ── 양쪽 이름표 — 포개지면서 물러난다 ────────── */
    const fade = 1 - clamp((merge - 0.5) / 0.35);
    if (fk > 0.55 && spec.first && fade > 0.02) {
      const k = clamp((fk - 0.55) / 0.35) * fade;
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.first, 800, 12.5, IW - 10, 9);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(spec.first, icx, MIDY - IH / 2 - 12);
      ctx.globalAlpha = 1;
    }
    if (mk > 0.3 && spec.second && fade > 0.02) {
      const k = clamp((mk - 0.3) / 0.3) * fade;
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.second, 800, 12.5, OW - 10, 9);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.second, ocx, MIDY - OH / 2 - 12);
      ctx.globalAlpha = 1;
    }

    /* ── 두 번 연속 재현 ──────────────────────────── */
    if (merge > 0.6 && spec.twiceLabel) {
      const k = clamp((merge - 0.6) / 0.4);
      const s = Math.min(1, spring(k));
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.twiceLabel, 900, 17, w - 40, 11);
      ctx.font = disp(900, fs * s); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.twiceLabel, w / 2, 66);
      ctx.globalAlpha = 1;
    }

    /* ── 차단선 ───────────────────────────────────── */
    if (nk > 0.02) {
      const k = clamp(nk * 1.8);
      const half = lerp(0, 90, ease(clamp(nk / 0.6)));
      ctx.globalAlpha = k;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.moveTo(w / 2 - half, 208); ctx.lineTo(w / 2 + half, 208);
      ctx.stroke();
      if (half > 62) {
        ctx.beginPath();
        ctx.moveTo(w / 2 - half, 200); ctx.lineTo(w / 2 - half, 216);
        ctx.moveTo(w / 2 + half, 200); ctx.lineTo(w / 2 + half, 216);
        ctx.stroke();
      }
      ctx.globalAlpha = 1;
    }

    if (nk > 0.3 && spec.meaning) {
      const k = clamp((nk - 0.3) / 0.45);
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.meaning, 900, 15, w - 28, 10);
      ctx.font = disp(900, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(spec.meaning, w / 2, 242);
      ctx.globalAlpha = 1;
    }

    /* ── 남는 한 줄 ───────────────────────────────── */
    /* 🔴 훅 카드는 여기서 그리지 않는다(2026-08-04 사용자 판정).
       .outrocard 가 .vis 와 좌표가 한 픽셀도 다르지 않고 배경이 불투명이라, 마지막
       OUTRO_MS(2.6초) 동안 이 캔버스는 통째로 덮인다 — 검수 실측으로 ep04s-3 은 훅이
       단 한 프레임도 보인 적이 없었고(카드 등장 35,765ms vs 예고 자막 35,776ms),
       가장 오래 보인 ep05s-1 도 100ms 였다. 훅은 engine/index.html 의 .oc-hook 이 맡아
       카드 안에서 2.6초 내내 서 있다. kind 는 그림에만 집중한다.
       🔑 "두 곳에 적으면 갈린다"가 원래 원칙이었는데 카드와 캔버스 사이에서 깨져 있었다. */
/* ── 다음 편 ──────────────────────────────────── */
    if (xk > 0.02 && spec.nextLabel) {
      const k = clamp(xk * 1.6);
      const s = Math.min(1, spring(k));
      const bw = Math.min(w - 60, 210), bx = (w - bw) / 2, by = 12, bh = 26;
      ctx.globalAlpha = k;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
      setShadow(ctx, GLOW, 10, 0);
      roundRect(ctx, bx + (bw - bw * s) / 2, by + (bh - bh * s) / 2, bw * s, bh * s, 4);
      ctx.stroke();
      clearShadow(ctx);
      ctx.textAlign = 'center';
      const fs = fit(spec.nextLabel, 800, 12, bw - 24, 9);
      ctx.font = disp(800, fs * s); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.nextLabel, w / 2, by + 18);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
