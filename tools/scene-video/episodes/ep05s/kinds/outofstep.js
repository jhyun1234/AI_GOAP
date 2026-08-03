import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* outofstep — 다섯이 한 박자로 움직이다가, 하나가 박자에서 빠진다.

   예고를 카드나 막 띠로 만들지 않았다. 앞선 세 회차의 예고가 전부 '띠 위에 카드가
   얹히거나 밀려 들어오는' 그림이었기 때문이다. 여기서는 예고가 **박자**다 —
   주민 표식 다섯이 완전히 같은 위상으로 오르내리고(그래서 기준선을 다 같이 스친다),
   breakCue 에서 가운데 하나만 위상이 어긋나 기준선과 어긋난 자리에 있게 된다.
   다음 편 이름은 그 어긋난 표식 아래에서 자라 오른다.

   막은 이번 편과 같은 3막이므로 띠가 자라거나 끊기는 연출을 넣지 않았다 — 위쪽 칩
   하나로 '그대로'만 적는다.

   계속 도는 것 = 다섯의 오르내림(어긋난 뒤에도 멈추지 않는다). */

const BOB = 2.2;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, scene, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));

    const ak = ease(cue(spec.actCue ?? 0, 0.15, 0.6));
    const tk = ease(cue(spec.topicCue ?? 1, 0.15, 0.6));
    const sk = ease(cue(spec.sameCue ?? 2, 0.15, 0.6));
    const bk = ease(cue(spec.breakCue ?? 3, 0.15, 0.75));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    /* ── 막은 그대로 ──────────────────────────────── */
    {
      const k = clamp(ak * 2);
      const bx = 12, bw = w - 24, by = 10, bh = 26;
      ctx.globalAlpha = k;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('sub');
      roundRect(ctx, bx, by, bw, bh, 3); ctx.stroke();
      ctx.textAlign = 'left';
      const fs = fit(spec.act || '', 800, 12.5, bw - 96, 9);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.act || '', bx + 12, by + 18);
      if (spec.actNote) {
        ctx.textAlign = 'right';
        ctx.font = disp(800, 11); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.actNote, bx + bw - 12, by + 18);
      }
      ctx.globalAlpha = 1;
    }

    /* ── 다섯 ─────────────────────────────────────── */
    const N = 5, BREAK = 2;
    const XS = [52, 114, 176, 238, 300];
    const FOOT = 140, BODY = 30, LIFT = 24;
    const LINE_Y = FOOT - BODY - LIFT - 18;             // 다 같이 스치는 높이

    const off = 0.42 * ease(clamp((bk - 0.3) / 0.5));   // 어긋난 위상

    if (ak > 0.15) {
      const k = clamp((ak - 0.15) / 0.45);

      /* 기준선 */
      if (sk > 0.05) {
        const lk = clamp(sk * 1.8);
        ctx.globalAlpha = lk * 0.9;
        ctx.setLineDash([9, 7]);
        ctx.lineDashOffset = -(t * 12) % 16;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.moveTo(28, LINE_Y); ctx.lineTo(w - 28, LINE_Y); ctx.stroke();
        ctx.setLineDash([]); ctx.lineDashOffset = 0;
        ctx.globalAlpha = 1;

        if (spec.sameLabel) {
          ctx.globalAlpha = lk;
          ctx.textAlign = 'center';
          const fs = fit(spec.sameLabel, 800, 12, w - 60, 9);
          ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
          ctx.fillText(spec.sameLabel, w / 2, LINE_Y - 11);
          ctx.globalAlpha = 1;
        }
      }

      for (let i = 0; i < N; i++) {
        const phase = i === BREAK ? off : 0;
        const u = frac(t / BOB + phase);
        const lift = LIFT * (0.5 - 0.5 * Math.cos(u * Math.PI * 2));
        const y = FOOT - lift;
        const broken = i === BREAK && off > 0.04;
        ctx.globalAlpha = k;
        ctx.fillStyle = broken ? tone('accent') : tone('ink');
        if (broken) setShadow(ctx, GLOW, 12, 0);
        roundRect(ctx, XS[i] - 11, y - BODY, 22, BODY, 3); ctx.fill();
        ctx.beginPath(); ctx.arc(XS[i], y - BODY - 8, 7.5, 0, Math.PI * 2); ctx.fill();
        clearShadow(ctx);
        ctx.globalAlpha = 1;
      }
    }

    /* ── '다른 사람' ──────────────────────────────── */
    if (bk > 0.35 && spec.diffLabel) {
      const k = clamp((bk - 0.35) / 0.45);
      const s = Math.min(1, spring(k));
      const y = lerp(FOOT + 6, FOOT + 26, ease(k));
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(spec.diffLabel, 900, 16, w - 60, 11);
      ctx.font = disp(900, fs * s); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.diffLabel, XS[BREAK], y);
      ctx.globalAlpha = 1;
    }

    /* ── 다음 편 ──────────────────────────────────── */
    if (tk > 0.02) {
      const k = clamp(tk * 2);
      const s = Math.min(1, spring(clamp(tk * 1.2)));
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      ctx.font = disp(800, 10.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.nextLabel || '다음 편', w / 2, 198);

      const fs = fit(spec.topic || '', 900, 21, w - 34, 13);
      ctx.font = disp(900, fs * s); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.topic || '', w / 2, 226);
      ctx.globalAlpha = 1;

      if (tk > 0.5 && spec.oneLine) {
        const kk = clamp((tk - 0.5) / 0.5);
        ctx.globalAlpha = kk;
        const nfs = fit(spec.oneLine, 700, 11.5, w - 34, 8.5);
        ctx.font = disp(700, nfs); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.oneLine, w / 2, 248);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 남는 한 줄 ───────────────────────────────── */
    const hook = spec.hook || scene?.hook;
    if (hook && bk > 0.5) {
      const k = clamp((bk - 0.5) / 0.45);
      const s = Math.min(1, spring(k));
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(String(hook), 900, 15, w - 30, 10);
      ctx.font = disp(900, fs * s); ctx.fillStyle = tone('ink');
      ctx.fillText(String(hook), w / 2, 286);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
