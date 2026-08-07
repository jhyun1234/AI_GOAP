import {
  disp, ease, clamp, frac,
  fitCanvas, mkCanvas, roundRect, tone
} from '../../../engine/lib.js';

/* gnaw — 방금 그은 길이 파먹힌다.

   원문: "그런데 이 편리함이 부탁 시스템의 존재 이유를 통째로 갉아먹고 있었습니다."

   🔴 기본 도형이 여기서 **두 길**로 나온다. 위는 방금 만든 길 — 주민에서 목수를 거쳐
   집으로 가는 강조색 레일이고, 아래는 그 전부터 있던 편의 기능 — 주민에서 집으로 바로
   내려꽂히는 흰 호(弧)다. **아래 호는 목수를 안 거친다.** 그것이 문제의 전부다.

   🔴 자막이 한 줄이라 **그 한 줄의 앞뒤 구간**으로 가른다(split).
   앞 절(「방금 만든 부탁 시스템의」) → 레일이 왼쪽에서 오른쪽으로 그어진다.
   뒤 절(「존재 이유를 통째로 갉아먹은 거죠」) → 그 레일이 **오른쪽 끝에서부터** 조각조각
   떨어져 나가고, 떨어진 조각은 아래로 흘러내리며 사라진다. 남는 것은 흐린 자국뿐이다.

   🔴 **집은 끝까지 채워진 채로 있다.** 원문이 말하는 것은 '집이 안 지어졌다'가 아니라
   '집이 알아서 지어져서 부탁할 이유가 없었다'이므로, 여기서 굶는 것은 집이 아니라 길이다.
   같은 이유로 목수 고리도 함께 흐려진다 — 아무도 목수를 안 거치게 된다.

   🔴 파먹히는 순서를 **오른쪽(집 쪽)부터**로 잡았다. 아래 호가 집에 먼저 닿으므로,
   길이 쓸모를 잃는 것도 도착지 쪽부터다.

   계속 도는 것 = 호 위를 도는 표식 셋(경로가 길어 이 샷에서 가장 큰 이동) ·
   호와 자국의 점선 흐름. 둘 다 문턱이 없다. */

const RAIL_Y = 112;
const VX = 44, CXP = 176, HXP = 300;
const HW = 36, HH = 30;
const ARC_C = 330;            // 호 제어점 y — 실제 최저점은 약 226
const SEG = 14;
const ORBIT = 2.4;

/** 호 위의 한 점 (2차 베지에) */
function arcPt(u, w) {
  const x0 = VX, y0 = RAIL_Y + 10;
  const x1 = w * (HXP / 352), y1 = RAIL_Y + 14;
  const cx = w * (172 / 352), cy = ARC_C;
  const m = 1 - u;
  return {
    x: m * m * x0 + 2 * m * u * cx + u * u * x1,
    y: m * m * y0 + 2 * m * u * cy + u * u * y1
  };
}

function hatch(ctx, x, y, bw, bh, k, t, color) {
  if (k <= 0.004) return;
  const fh = bh * k, fy = y + bh - fh;
  ctx.save();
  ctx.beginPath(); ctx.rect(x, fy, bw, fh); ctx.clip();
  ctx.strokeStyle = color; ctx.lineWidth = 3;
  const step = 13, off = (t * 11) % step;
  for (let d = -bh - step; d < bw + step; d += step) {
    ctx.beginPath();
    ctx.moveTo(x + d + off, y + bh);
    ctx.lineTo(x + d + off + bh, y);
    ctx.stroke();
  }
  ctx.restore();
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const fit = (txt, weight, start, max, min = 8) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const cx = w * (CXP / 352);
    const hx = Math.min(w * (HXP / 352), w - 22 - HW / 2);
    const railL = VX + 9, railR = hx - HW / 2 - 2;

    const c0 = cue(spec.gnawCue ?? 0, 0.15, 0.85);
    const split = spec.split ?? 0.55;
    const rk = ease(clamp(c0 / split));                 // 길을 놓는다
    const gk = ease(clamp((c0 - split) / (1 - split))); // 길이 파먹힌다

    /* ── 아래 호 — 전부터 있던 편의 기능 ───────────── */
    const ak = clamp(c0 * 7);
    if (ak > 0.02) {
      ctx.globalAlpha = ak * 0.95;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.setLineDash([13, 9]);
      ctx.lineDashOffset = -(t * 22) % 22;
      ctx.beginPath();
      for (let i = 0; i <= 40; i++) {
        const p = arcPt(i / 40, w);
        if (i === 0) ctx.moveTo(p.x, p.y); else ctx.lineTo(p.x, p.y);
      }
      ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;

      if (spec.autoLabel) {
        ctx.globalAlpha = ak * 0.9;
        ctx.textAlign = 'center';
        ctx.font = disp(800, fit(spec.autoLabel, 800, 13, 150, 9));
        ctx.fillStyle = tone('sub');
        ctx.fillText(spec.autoLabel, cx, 258);
        ctx.globalAlpha = 1;
      }

      /* 호 위를 도는 표식 셋 — 편의 기능은 쉬지 않는다 */
      for (let i = 0; i < 3; i++) {
        const u = frac(t / ORBIT + i / 3);
        const p = arcPt(u, w);
        ctx.globalAlpha = ak * (0.35 + 0.65 * Math.sin(Math.PI * u));
        ctx.fillStyle = tone('ink');
        ctx.beginPath(); ctx.arc(p.x, p.y, 6, 0, Math.PI * 2); ctx.fill();
        ctx.globalAlpha = 1;
      }
    }

    /* ── 위 레일 — 방금 만든 길, 그리고 파먹힘 ─────── */
    const segW = (railR - railL) / SEG;
    for (let i = 0; i < SEG; i++) {
      const sx = railL + i * segW;
      const drawn = clamp((rk * SEG - i) / 0.9);          // 왼쪽부터 그어진다
      if (drawn <= 0.02) continue;
      const eaten = clamp((gk * (SEG + 1.5) - (SEG - 1 - i)) / 1.2);  // 오른쪽부터 먹힌다

      /* 먹힌 자리에 남는 자국 */
      if (eaten > 0.02) {
        ctx.globalAlpha = eaten * 0.9;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.setLineDash([6, 7]);
        ctx.lineDashOffset = (t * 13) % 13;
        ctx.beginPath();
        ctx.moveTo(sx, RAIL_Y); ctx.lineTo(sx + segW - 2, RAIL_Y);
        ctx.stroke();
        ctx.setLineDash([]); ctx.lineDashOffset = 0;
        ctx.globalAlpha = 1;
      }

      const live = drawn * (1 - eaten);
      if (live > 0.02) {
        ctx.globalAlpha = live;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 5;
        const dy = 14 * eaten;                            // 떨어져 나간다
        ctx.beginPath();
        ctx.moveTo(sx, RAIL_Y + dy);
        ctx.lineTo(sx + segW * drawn - 2, RAIL_Y + dy);
        ctx.stroke();
        ctx.globalAlpha = 1;
      }
    }

    if (spec.railLabel) {
      const a = clamp(rk * 2.4) * (1 - 0.72 * gk);
      if (a > 0.02) {
        ctx.globalAlpha = a;
        ctx.textAlign = 'center';
        ctx.font = disp(900, fit(spec.railLabel, 900, 13, 170, 9));
        ctx.fillStyle = tone('accent');
        ctx.fillText(spec.railLabel, cx, 84);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 목수 고리 — 아무도 안 거치게 되면서 흐려진다 ── */
    const ck = clamp(rk * 2.2) * (1 - 0.78 * gk);
    if (ck > 0.02) {
      ctx.globalAlpha = ck;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.arc(cx, RAIL_Y, 12, 0, Math.PI * 2); ctx.stroke();
      ctx.setLineDash([7, 6]);
      ctx.lineDashOffset = -(t * 16) % 13;
      ctx.beginPath(); ctx.arc(cx, RAIL_Y, 19, 0, Math.PI * 2); ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;
      ctx.globalAlpha = 1;
    }

    /* ── 주민 · 집 — 흰 것을 마지막에 얹는다 ───────── */
    ctx.globalAlpha = clamp(c0 * 7);
    ctx.fillStyle = tone('ink');
    ctx.beginPath(); ctx.arc(VX, RAIL_Y, 7, 0, Math.PI * 2); ctx.fill();
    ctx.globalAlpha = 1;

    const hk = clamp(c0 * 5);
    if (hk > 0.02) {
      const hxL = hx - HW / 2, hyT = RAIL_Y - HH / 2;
      hatch(ctx, hxL, hyT, HW, HH, ease(hk), t, tone('ink'));
      ctx.globalAlpha = hk;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, hxL, hyT, HW, HH, 5); ctx.stroke();
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
