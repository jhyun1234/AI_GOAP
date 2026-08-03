import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* actopen — 2막이 닫히고, 3막 표지가 위에서 내려와 박힌다.

   지난 편의 마지막 그림은 막의 가로 띠가 오른쪽으로 자라는 것이었다. 같은 장치를 돌려
   쓰지 않으려고 여기서는 시간 축을 아예 안 그린다 — 대신 표지가 '내려와 박히는' 세로
   운동이다. 지난 편이 예고한 자리에 이번 편이 실제로 도착하는 그림이라, 옆으로 자라는
   것보다 위에서 꽂히는 편이 맞는다.

   아래 두 칸은 M0 이 남긴 공백이다(할 일 없음 · 식량 고갈). 점선 테두리가 계속 흐르는
   동안은 아직 뚫린 자리이고, fillCue 에서 실선으로 닫힌다. 그리고 addCue 에서 공백을
   메우는 것이 아닌 칸 하나가 새로 얹힌다 — 명령과 거부. 3막의 '한 편에 기능 하나씩'을
   화면 구조 자체로 말한다.

   계속 도는 것 = 아직 안 메워진 칸의 점선이 흐르는 것, 메워진 뒤에는 새 칸 테두리를
   도는 표식. 두 구간이 이어져 화면에 정적이 남지 않는다. */

const DASH = 16;   // 점선이 흐르는 속도(px/s)
const RUN = 3.4;   // 새 칸 테두리를 도는 주기(초)

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));

    const ak = ease(cue(spec.actCue ?? 0, 0.15, 0.55));
    const fk = ease(cue(spec.fillCue ?? 1, 0.15, 0.6));
    const nk = ease(cue(spec.addCue ?? 2, 0.15, 0.55));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 8) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    /* ── 2막이 닫힌다 ─────────────────────────────── */
    {
      const bx = 8, by = 12, bw = 126, bh = 28;
      ctx.globalAlpha = 0.55;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('sub');
      roundRect(ctx, bx, by, bw, bh, 3); ctx.stroke();
      ctx.textAlign = 'center';
      const s = spec.closing || '2막 · 리셋';
      const fs = fit(s, 800, 12, bw - 14);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(s, bx + bw / 2, by + 19);
      ctx.globalAlpha = 1;

      // 마감 캡
      const ck = clamp(ak * 2);
      if (ck > 0.02) {
        ctx.globalAlpha = ck * 0.8;
        ctx.strokeStyle = tone('sub'); ctx.lineWidth = 4;
        ctx.beginPath();
        ctx.moveTo(bx + bw + 5, by + bh / 2 - (bh / 2 + 4) * ck);
        ctx.lineTo(bx + bw + 5, by + bh / 2 + (bh / 2 + 4) * ck);
        ctx.stroke();
        ctx.globalAlpha = 1;
      }
    }

    /* ── 3막 표지가 내려와 박힌다 ─────────────────── */
    {
      const sx = 8, sw = w - 16, sh = 46;
      const land = 58;
      const drop = ease(clamp(ak / 0.72));
      const sy = lerp(land - 44, land, drop);
      const over = ak > 0.72 ? Math.sin((ak - 0.72) / 0.28 * Math.PI) * 2.5 : 0;

      ctx.globalAlpha = clamp(ak * 2.4);
      setShadow(ctx, GLOW, 18, 0);
      ctx.lineWidth = 4; ctx.strokeStyle = tone('accent');
      roundRect(ctx, sx, sy + over, sw, sh, 5); ctx.stroke();
      clearShadow(ctx);

      ctx.textAlign = 'center';
      const s = spec.opening || '3막';
      const fs = fit(s, 900, 24, sw - 30, 14);
      ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
      ctx.fillText(s, sx + sw / 2, sy + over + 32);
      ctx.globalAlpha = 1;

      if (ak > 0.8) {
        const k = clamp((ak - 0.8) / 0.2);
        ctx.globalAlpha = k * 0.9;
        ctx.textAlign = 'left';
        ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.stampLabel || '', sx + 2, sy + sh + 16);
        ctx.globalAlpha = 1;
      }
    }

    /* ── M0 이 남긴 공백 둘 ───────────────────────── */
    const holes = spec.holes || [];
    const hy = 128, hh = 60, gap = 10;
    const hw = (w - 16 - gap) / 2;
    holes.slice(0, 2).forEach((label, i) => {
      const x = 8 + i * (hw + gap);
      const show = clamp(ak * 2 - 0.6 + i * 0.05);
      if (show < 0.02) return;
      const filled = clamp(fk * 2.2 - i * 0.7);

      ctx.globalAlpha = clamp(show * 1.6);
      ctx.lineWidth = 3;
      if (filled < 0.5) {
        // 아직 뚫린 자리 — 점선이 계속 흐른다
        ctx.setLineDash([7, 6]);
        ctx.lineDashOffset = -frac(t / (26 / DASH)) * 13;
        ctx.strokeStyle = tone('sub');
        roundRect(ctx, x, hy, hw, hh, 4); ctx.stroke();
        ctx.setLineDash([]); ctx.lineDashOffset = 0;
      } else {
        const k = clamp((filled - 0.5) / 0.5);
        ctx.strokeStyle = tone('ink');
        roundRect(ctx, x, hy, hw, hh, 4); ctx.stroke();
        ctx.globalAlpha = clamp(show * 1.6) * k * 0.16;
        ctx.fillStyle = tone('ink');
        roundRect(ctx, x + 3, hy + 3, hw - 6, hh - 6, 3); ctx.fill();
        ctx.globalAlpha = clamp(show * 1.6);

        /* 메워진 칸도 멈추지 않는다 — 채워진 자리를 표식이 계속 지나간다.
           🔴 점선이 흐르는 것(뚫린 상태)과 새 칸 테두리를 도는 표식(마지막 상태) 사이에
           3초짜리 정적 구간이 생기던 자리다. 여기가 비면 화면이 한 번 죽는다. */
        const u = frac(t / 2.9 + i * 0.5);
        ctx.globalAlpha = clamp(show * 1.6) * k * 0.75 * Math.sin(Math.PI * u);
        ctx.fillStyle = tone('ink');
        ctx.fillRect(lerp(x + 5, x + hw - 13, u), hy + hh - 9, 8, 4);
        ctx.globalAlpha = clamp(show * 1.6);
      }

      ctx.textAlign = 'center';
      const fs = fit(label, 800, 13, hw - 16);
      ctx.font = disp(800, fs);
      ctx.fillStyle = filled > 0.5 ? tone('ink') : tone('sub');
      ctx.fillText(label, x + hw / 2, hy + 27);

      if (i === 1 && spec.holeNote) {
        const nfs = fit(spec.holeNote, 700, 10, hw - 12, 7.5);
        ctx.font = disp(700, nfs); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.holeNote, x + hw / 2, hy + 46);
      }
      if (i === 0 && filled > 0.6) {
        ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.holeFixNote || '', x + hw / 2, hy + 46);
      }
      ctx.globalAlpha = 1;
    });

    /* ── 공백이 아닌 것 하나가 더 얹힌다 ──────────── */
    if (nk > 0.02) {
      const bx = 8, bw = w - 16, bh = 62;
      const land = 210;
      const s = Math.min(1, spring(nk));
      const by = lerp(land - 26, land, ease(clamp(nk / 0.8)));

      ctx.globalAlpha = clamp(nk * 1.8);
      setShadow(ctx, GLOW, 16, 0);
      ctx.lineWidth = 4; ctx.strokeStyle = tone('accent');
      roundRect(ctx, bx + (bw - bw * s) / 2, by + (bh - bh * s) / 2, bw * s, bh * s, 5);
      ctx.stroke();
      clearShadow(ctx);

      ctx.textAlign = 'center';
      const s1 = spec.added || '';
      const fs = fit(s1, 900, 22, bw - 30, 13);
      ctx.font = disp(900, fs * s); ctx.fillStyle = tone('accent');
      ctx.fillText(s1, bx + bw / 2, by + 34);

      if (spec.addedNote) {
        ctx.font = disp(700, 10.5); ctx.fillStyle = tone('sub');
        ctx.fillText(spec.addedNote, bx + bw / 2, by + 52);
      }
      ctx.globalAlpha = 1;

      /* 계속 도는 것 — 새 칸 테두리를 한 바퀴 도는 표식 */
      if (nk > 0.5) {
        const per = 2 * (bw + bh);
        const u = frac(t / RUN) * per;
        let px, py;
        if (u < bw) { px = bx + u; py = by; }
        else if (u < bw + bh) { px = bx + bw; py = by + (u - bw); }
        else if (u < 2 * bw + bh) { px = bx + bw - (u - bw - bh); py = by + bh; }
        else { px = bx; py = by + bh - (u - 2 * bw - bh); }
        ctx.globalAlpha = clamp((nk - 0.5) * 2.4);
        setShadow(ctx, GLOW, 10, 0);
        ctx.fillStyle = tone('accent');
        ctx.beginPath(); ctx.arc(px, py, 4, 0, Math.PI * 2); ctx.fill();
        clearShadow(ctx);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
