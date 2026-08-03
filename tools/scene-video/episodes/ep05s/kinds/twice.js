import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* twice — 같은 자국이 두 번 찍힌다.

   이 편의 결론은 '코드가 0줄이었다'가 아니라 '같은 일이 두 번 일어났다'이다. 그래서
   화면이 세는 것은 줄 수가 아니라 자국의 겹이다. firstCue 에서 자국 하나가 눌려 찍히고
   (M2 요리), secondCue 에서 두 번째 자국이 위에서 내려와 첫 자국을 감싸며 정확히 겹친다
   (M3 휴식 앵커). 겹치는 순간 테두리가 두 겹이 되고 안쪽에 패턴 이름이 남는다.
   meanCue 에서 자국 아래로 차단선이 그어진다 — 원문 6절의 "구조적으로 차단됐다".

   🔴 앞선 회차가 '코드 0줄'을 편집기 줄 번호와 0 이라는 카운터로 그렸다. 여기에는
   편집기도 카운터도 없다. '에셋 1개, 코드 0줄'은 움직이는 숫자가 아니라 **자국 안에
   찍혀 있는 이름**으로만 나온다 — 그 이름을 두 번 받아 낸 것이 이 샷의 사건이다.

   계속 도는 것 = 자국 테두리를 도는 표식(겹치기 전에는 안쪽 자국, 겹친 뒤에는 바깥 자국). */

const ORBIT = 5.2;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, scene, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));

    const fk = ease(cue(spec.firstCue ?? 0, 0.15, 0.6));
    const mk = ease(cue(spec.secondCue ?? 1, 0.15, 0.72));
    const nk = ease(cue(spec.meanCue ?? 2, 0.15, 0.6));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const IX = 104, IY = 92, IW = 144, IH = 86;         // 첫 자국
    const OX = 96, OY = 82, OW = 160, OH = 106;         // 두 번째 자국
    const land = ease(clamp((mk - 0.4) / 0.45));

    /* 테두리를 도는 표식 */
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
      ctx.beginPath(); ctx.arc(mx, my, 4.5, 0, Math.PI * 2); ctx.fill();
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    };

    /* ── 첫 자국 ──────────────────────────────────── */
    if (fk > 0.02) {
      const press = ease(clamp(fk / 0.7));
      const dy = lerp(-16, 0, press);
      const s = Math.min(1, spring(clamp(fk * 1.2)));
      ctx.globalAlpha = clamp(fk * 2);
      ctx.lineWidth = 4; ctx.strokeStyle = tone('ink');
      roundRect(ctx, IX + (IW - IW * s) / 2, IY + dy + (IH - IH * s) / 2, IW * s, IH * s, 4);
      ctx.stroke();
      ctx.globalAlpha = 1;
      if (land < 0.5) orbit(IX, IY + dy, IW, IH, tone('ink'), clamp(fk * 2) * 0.9);
    }

    /* ── 두 번째 자국 — 위에서 내려와 정확히 겹친다 ─ */
    if (mk > 0.02) {
      const dy = lerp(-38, 0, land);
      ctx.globalAlpha = clamp(mk * 2);
      ctx.lineWidth = 4; ctx.strokeStyle = tone('accent');
      if (land > 0.85) setShadow(ctx, GLOW, 16, 0);
      roundRect(ctx, OX, OY + dy, OW, OH, 5); ctx.stroke();
      clearShadow(ctx);
      ctx.globalAlpha = 1;
      if (land >= 0.5) orbit(OX, OY + dy, OW, OH, tone('accent'), clamp(mk * 2) * 0.95);
    }

    /* ── 자국 안의 이름 ───────────────────────────── */
    if (fk > 0.55 && spec.pattern) {
      const k = clamp((fk - 0.55) / 0.45);
      const s = Math.min(1, spring(k));
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.pattern, 900, 17, IW - 18, 11);
      ctx.font = disp(900, fs * s);
      ctx.fillStyle = land > 0.85 ? tone('accent') : tone('ink');
      ctx.fillText(spec.pattern, w / 2, IY + IH / 2 + 6);
      ctx.globalAlpha = 1;
    }

    /* ── 양쪽 이름표 ──────────────────────────────── */
    if (fk > 0.7 && spec.first) {
      const k = clamp((fk - 0.7) / 0.3);
      ctx.globalAlpha = k;
      ctx.textAlign = 'right';
      const fs = fit(spec.first, 800, 11, 84, 8);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(spec.first, 90, IY + IH / 2 + 4);
      ctx.globalAlpha = 1;
    }
    if (land > 0.5 && spec.second) {
      const k = clamp((land - 0.5) / 0.4);
      ctx.globalAlpha = k;
      ctx.textAlign = 'left';
      const fs = fit(spec.second, 800, 11, w - 268, 8);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.second, 262, IY + IH / 2 + 4);
      ctx.globalAlpha = 1;
    }

    /* ── 두 번 연속 ───────────────────────────────── */
    if (land > 0.6 && spec.twiceLabel) {
      const k = clamp((land - 0.6) / 0.4);
      const s = Math.min(1, spring(k));
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.twiceLabel, 900, 16, w - 40, 11);
      ctx.font = disp(900, fs * s); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.twiceLabel, w / 2, 66);
      ctx.globalAlpha = 1;
    }

    /* ── 차단선 ───────────────────────────────────── */
    if (nk > 0.02) {
      const k = clamp(nk * 1.8);
      const half = lerp(0, 86, ease(clamp(nk / 0.6)));
      ctx.globalAlpha = k;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.moveTo(w / 2 - half, 214); ctx.lineTo(w / 2 + half, 214);
      ctx.stroke();
      if (half > 60) {
        ctx.beginPath();
        ctx.moveTo(w / 2 - half, 206); ctx.lineTo(w / 2 - half, 222);
        ctx.moveTo(w / 2 + half, 206); ctx.lineTo(w / 2 + half, 222);
        ctx.stroke();
      }
      ctx.globalAlpha = 1;
    }

    if (nk > 0.35 && spec.meaning) {
      const k = clamp((nk - 0.35) / 0.5);
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.meaning, 900, 15, w - 28, 10);
      ctx.font = disp(900, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(spec.meaning, w / 2, 250);
      ctx.globalAlpha = 1;
    }
    if (nk > 0.6 && spec.durability) {
      const k = clamp((nk - 0.6) / 0.4);
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.durability, 700, 11, w - 40, 8);
      ctx.font = disp(700, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.durability, w / 2, 278);
      ctx.globalAlpha = 1;
    }

    /* ── 남는 한 줄 ───────────────────────────────── */
    /* 🔴 훅을 그리는 코드는 1차본에서 outofstep(예고 샷)에만 있었다. 2026-08-03 분할로 이 편이
       예고 샷 없이 twice 로 끝나게 되어 같은 블록을 여기로 옮겼다. 안 옮기면 ep01s 부터 이어 온
       마지막 한 줄이 이 편에서만 사라진다. durability 줄(278)과 겹치지 않게 300 에 뒀다. */
    /* 🔴 훅을 meanCue(자막 2)가 아니라 hookCue(예고 자막)에 물린다. 예고 자막에는 cue 가 없어서
       그동안 화면이 통째로 멈추고, 정적 구간이 3.6 → 6.6초로 뛰었다(2026-08-03). 궤도 표식은
       반지름 4.5px 라 정적 판정 문턱(평균 차이 0.0008) 아래여서 움직임으로 안 잡힌다.
       훅 카드가 그 자리에서 스프링으로 올라오면 예고를 읽는 동안 화면이 산다. */
    const hook = spec.hook || scene?.hook;
    const hk = ease(cue(spec.hookCue ?? spec.meanCue ?? 2, 0.15, 0.5));
    if (hook && hk > 0.02) {
      const k2 = clamp(hk * 1.6);
      const s2 = Math.min(1, spring(k2));
      ctx.globalAlpha = k2;
      ctx.textAlign = 'center';
      const fsH = fit(String(hook), 900, 15, w - 30, 10);
      ctx.font = disp(900, fsH * s2); ctx.fillStyle = tone('ink');
      ctx.fillText(String(hook), w / 2, 300);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};