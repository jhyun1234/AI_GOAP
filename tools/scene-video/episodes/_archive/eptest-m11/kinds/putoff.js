import {
  ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, disp,
  setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* putoff — 오늘 칸과 앞날 칸 사이의 거리가 성격이다.

   기본 도형의 마지막 변주. 게으름은 능력이 아니라 **일이 놓이는 자리**다.
   오늘 할 일은 남들과 같은 자리에 있고, 앞날을 위한 일 셋만 오른쪽으로 밀려난다.
   -20 인 둘은 같은 만큼, -15 인 하나는 조금 덜 — 세 값의 차이가 곧 밀린 거리다.

   🔴 밀린 거리를 숫자와 같은 축에 두는 것이 이 그림의 전부다. 막대 길이로 -20 을
   그리면 '게으름의 크기' 가 되는데, 원문이 말하는 것은 크기가 아니라 순서다.
   그래서 값은 토큰에 적히고, 토큰의 x 좌표가 그 값이 하는 일을 보여 준다.

   🔴 마지막 샷이라 효과음을 두지 않았다. 조용히 닫는다.

   계속 도는 것 = 오늘에서 앞날로 쉬지 않고 흘러가는 할 일 표식 셋. 밀려난 토큰
   뒤로 지나간다 — 오늘 할 일은 성격과 무관하게 계속 흐른다는 뜻이다. */

const DRIFT = 2.8;                       // 할 일 하나가 오늘에서 앞날로 가는 초
const ROW_TOP = 64, ROW_H = 40;
const LANE_L = 22, LANE_R = 330;
const MAX_SHIFT = 170;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const tk = ease(cue(spec.traitCue ?? 0, 0.15, 0.5));
    const bk = ease(cue(spec.boostCue ?? 1, 0.15, 0.55));
    const nk = ease(cue(spec.thenCue ?? 2, 0.15, 0.5));
    const boosts = spec.boosts || [];
    const axis = spec.axis || [];

    ctx.textBaseline = 'alphabetic';

    /* ── 이름과 정의 ────────────────────────────── */
    ctx.globalAlpha = clamp(tk * 2);
    ctx.font = disp(800, 12);
    {
      const tw = ctx.measureText(spec.trait || '').width;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      setShadow(ctx, GLOW, 9, 0);
      roundRect(ctx, 14, 8, tw + 22, 25, 4); ctx.stroke();
      clearShadow(ctx);
      ctx.textAlign = 'left';
      ctx.fillStyle = tone('accent');
      ctx.fillText(spec.trait || '', 25, 25);
    }
    ctx.textAlign = 'right';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10.5);
    right(ctx, spec.core || '', w - 14, 25, w);
    ctx.globalAlpha = 1;

    /* ── 축 ─────────────────────────────────────── */
    ctx.globalAlpha = clamp(tk * 2);
    ctx.textAlign = 'left';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 9.5);
    ctx.fillText(axis[0] || '', LANE_L, 54);
    ctx.textAlign = 'right';
    right(ctx, axis[1] || '', LANE_R, 54, w);
    ctx.globalAlpha = 1;

    /* ── 세 줄 ──────────────────────────────────── */
    boosts.forEach((b, i) => {
      const top = ROW_TOP + i * ROW_H;
      const mid = top + 20;
      const a = clamp(tk * 2);

      // 레일
      ctx.globalAlpha = a;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(LANE_L, mid); ctx.lineTo(LANE_R, mid); ctx.stroke();
      ctx.globalAlpha = 1;

      // 계속 도는 것 — 오늘 할 일이 흘러간다(토큰 뒤로 지나간다)
      {
        const k = frac(t / DRIFT + i * 0.31);
        const x = lerp(LANE_L, LANE_R, k);
        ctx.globalAlpha = a * Math.sin(Math.PI * clamp(k * 1.05)) * 0.9;
        ctx.fillStyle = tone('sub');
        roundRect(ctx, x - 11, mid - 9, 22, 18, 3); ctx.fill();
        ctx.globalAlpha = 1;
      }

      // 토큰 — 값이 클수록 뒤로 밀린다
      ctx.font = disp(800, 11.5);
      const nw = ctx.measureText(b.name || '').width;
      ctx.font = disp(900, 13);
      const vTxt = String(b.v ?? '');
      const vw = ctx.measureText(vTxt).width;
      const bw = nw + vw + 34;
      const shift = MAX_SHIFT * (Math.abs(b.v || 0) / 20) * ease(clamp(bk * 1.1));
      const bx = clamp(26 + shift, 14, w - 14 - bw);

      ctx.globalAlpha = a;
      ctx.fillStyle = '#000';
      roundRect(ctx, bx, top + 5, bw, 30, 4); ctx.fill();
      ctx.strokeStyle = shift > 6 ? tone('accent') : tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, bx, top + 5, bw, 30, 4); ctx.stroke();
      ctx.textAlign = 'left';
      ctx.fillStyle = tone('ink'); ctx.font = disp(800, 11.5);
      ctx.fillText(b.name || '', bx + 13, top + 25);
      ctx.fillStyle = tone('accent'); ctx.font = disp(900, 13);
      ctx.fillText(vTxt, bx + 13 + nw + 10, top + 25);
      ctx.globalAlpha = 1;
    });

    /* ── 오늘 할 일은 그대로 ────────────────────── */
    if (bk > 0.4) {
      ctx.globalAlpha = clamp((bk - 0.4) * 3);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, LANE_L, 190, LANE_R - LANE_L, 28, 4); ctx.stroke();
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('ink'); ctx.font = disp(800, 12);
      put(ctx, spec.today || '', w / 2, 209, w);
      ctx.globalAlpha = 1;
    }

    /* ── 되받기 : 예전과 지금 ───────────────────── */
    if (nk > 0.02) {
      // 예전 — 그어진다
      ctx.globalAlpha = clamp(nk * 2.2);
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 12);
      put(ctx, spec.before || '', w / 2, 243, w);
      const sw = Math.min(ctx.measureText(spec.before || '').width, w - 28);
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(w / 2 - sw / 2, 239);
      ctx.lineTo(w / 2 - sw / 2 + sw * clamp(nk * 1.6), 239);
      ctx.stroke();
      ctx.globalAlpha = 1;

      // 지금 — 이 편의 결론
      const ak = clamp((nk - 0.3) * 2.4);
      if (ak > 0.02) {
        ctx.globalAlpha = ak;
        ctx.font = disp(900, 15);
        const tw = ctx.measureText(spec.after || '').width;
        const bw = Math.min(w - 24, tw + 28);
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        setShadow(ctx, GLOW, 11, 0);
        roundRect(ctx, (w - bw) / 2, 256, bw, 30, 4); ctx.stroke();
        clearShadow(ctx);
        ctx.textAlign = 'center';
        ctx.fillStyle = tone('accent');
        put(ctx, spec.after || '', w / 2, 277, w);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};

function put(ctx, s, cx, y, cw, pad = 14) {
  if (!s) return;
  const tw = ctx.measureText(s).width;
  const x = tw < cw - pad * 2 ? clamp(cx, pad + tw / 2, cw - pad - tw / 2) : cw / 2;
  ctx.fillText(s, x, y);
}
function right(ctx, s, x, y, cw, pad = 12) {
  if (!s) return;
  const tw = ctx.measureText(s).width;
  ctx.fillText(s, tw < cw - pad * 2 ? clamp(x, pad + tw, cw - pad) : cw - pad, y);
}
