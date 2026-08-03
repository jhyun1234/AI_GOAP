import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* oneline — 같은 명령이 두 게임에서 다르게 끝난다.

   위는 RTS 다. 명령이 유닛에 닿고 그대로 통과해 복종으로 끝난다 — 그 표식이 계속 반복해서
   도착한다(rtsCue). 아래는 이 게임이다. 같은 표식이 같은 자리까지 왔다가 주민 앞에서 막을
   만나 되돌아간다(mineCue). 두 줄의 차이는 도형이 아니라 **표식의 종착지**다.

   아래 카드는 프로젝트 문서에 적힌 성공 기준 한 마디다(quoteCue). 기술 지표가 아니라 이
   문장이 목적이라는 것이 이 절의 요지라, 테스트 통과 수치는 카드 아래 작은 줄로 내렸다 —
   크기의 순서가 곧 원문이 말한 우선순위다.

   계속 도는 것 = RTS 줄에 반복해서 도착하는 명령 표식. 이 편에서 유일하게 '고쳐지지 않는'
   반복이라, 아래 줄의 한 번뿐인 되튕김과 대비된다. */

const ORDER = 2.2;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));

    const rk = ease(cue(spec.rtsCue ?? 0, 0.15, 0.6));
    const mk = ease(cue(spec.mineCue ?? 1, 0.15, 0.6));
    const lk = ease(cue(spec.leadCue ?? 2, 0.15, 0.6));
    const qk = ease(cue(spec.quoteCue ?? 3, 0.15, 0.5));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const row = (yTop, who, act, note, on, accent, bounced) => {
      const uy = yTop + 34, ux = 150;
      ctx.globalAlpha = clamp(on * 1.8);

      ctx.textAlign = 'left';
      const wfs = fit(who, 700, 10.5, 130, 8);
      ctx.font = disp(700, wfs); ctx.fillStyle = tone('sub');
      ctx.fillText(who, 8, yTop + 14);

      // 경로
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(10, uy); ctx.lineTo(ux - 18, uy); ctx.stroke();

      // 유닛
      ctx.lineWidth = 3; ctx.strokeStyle = accent ? tone('accent') : tone('ink');
      ctx.beginPath(); ctx.arc(ux, uy, 14, 0, Math.PI * 2); ctx.stroke();

      // 판정
      ctx.textAlign = 'left';
      const afs = fit(act, 900, 21, w - ux - 34, 13);
      ctx.font = disp(900, afs);
      ctx.fillStyle = accent ? tone('accent') : tone('ink');
      ctx.fillText(act, ux + 26, uy + 6);

      if (note) {
        const nfs = fit(note, 700, 9.5, w - ux - 30, 7);
        ctx.font = disp(700, nfs); ctx.fillStyle = tone('sub');
        ctx.fillText(note, ux + 26, uy + 22);
      }
      ctx.globalAlpha = 1;

      /* 표식 — 위는 계속 도착하고, 아래는 막에 막힌다.
         🔴 문턱을 0.1 로 낮췄다. 0.35 였을 때 샷 첫 0.9초가 통째로 정적이었다 —
         진입 모션이 끝나자마자 무언가 움직이고 있어야 한다. */
      if (on > 0.1) {
        if (!bounced) {
          const u = frac(t / ORDER);
          const mx = lerp(12, ux - 16, u);
          ctx.globalAlpha = clamp(on * 1.8) * (0.35 + 0.65 * Math.sin(Math.PI * u));
          ctx.fillStyle = tone('ink');
          ctx.fillRect(mx - 5, uy - 3, 11, 6);
          ctx.globalAlpha = 1;
        } else {
          const k = clamp((on - 0.35) / 0.5);
          const mx = lerp(12, ux - 30, ease(k)) - 26 * ease(clamp((on - 0.7) / 0.3));
          ctx.globalAlpha = clamp(on * 1.8);
          ctx.fillStyle = tone('ink');
          ctx.fillRect(mx - 5, uy - 3, 11, 6);
          // 막
          setShadow(ctx, GLOW, 12, 0);
          ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
          ctx.beginPath(); ctx.arc(ux, uy, 24, Math.PI - 0.85 * k, Math.PI + 0.85 * k); ctx.stroke();
          clearShadow(ctx);
          ctx.globalAlpha = 1;
        }
      }
    };

    row(16, spec.rts?.who || '', spec.rts?.act || '', spec.rts?.note || '', rk, false, false);
    row(94, spec.mine?.who || '', spec.mine?.act || '', spec.mine?.note || '', mk, true, true);

    /* ── 성공 기준 ────────────────────────────────── */
    if (lk > 0.05 && spec.quoteLabel) {
      ctx.globalAlpha = clamp(lk * 1.8);
      ctx.textAlign = 'left';
      ctx.font = disp(800, 11); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.quoteLabel, 8, 178);
      ctx.globalAlpha = 1;
    }

    if (qk > 0.02 && spec.quote) {
      const k = clamp(qk * 1.6);
      const s = Math.min(1, spring(k));
      const cx = 8, cy = 188, cw = w - 16, chh = 58;
      ctx.globalAlpha = k;
      setShadow(ctx, GLOW, 18, 0);
      ctx.lineWidth = 4; ctx.strokeStyle = tone('accent');
      roundRect(ctx, cx + (cw - cw * s) / 2, cy + (chh - chh * s) / 2, cw * s, chh * s, 5);
      ctx.stroke();
      clearShadow(ctx);
      ctx.textAlign = 'center';
      const fs = fit(spec.quote, 900, 19, cw - 26, 12);
      ctx.font = disp(900, fs * s); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.quote, cx + cw / 2, cy + 37);
      ctx.globalAlpha = 1;
    }

    /* ── 지표는 작게 ──────────────────────────────── */
    const stats = spec.stats || [];
    if (qk > 0.45 && stats.length) {
      const k = clamp((qk - 0.45) / 0.55);
      ctx.globalAlpha = k * 0.95;
      ctx.textAlign = 'left';
      stats.slice(0, 2).forEach((s, i) => {
        const fs = fit(s, 700, 10.5, w - 24, 7.5);
        ctx.font = disp(700, fs); ctx.fillStyle = tone('sub');
        ctx.fillText('· ' + s, 8, 268 + i * 18);
      });
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
