import {
  disp, mono, ease, easeOut, clamp, lerp,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* fork — 하나로 뭉쳐 있던 반환값이 이름 셋으로 갈리는 샷.

   원문 91행: "예전에는 이 셋이 사실상 뭉뚱그려져 있어서, 호출하는 쪽 코드가
   '실패든 성공이든 일단 좌표나 맞춰두자'는 식으로 대충 처리할 여지가 있었습니다."

   그래서 이 샷에는 반드시 **'전' 상태**가 있어야 한다. 뭉뚱그린 칸 하나가 먼저 서고,
   거기서 나온 화살표가 아무 판단 없이 '도착'으로 꽂히는 것까지 보여야
   이름 붙이기가 무슨 변화인지 읽힌다. 그 화살표는 셋으로 갈리는 순간 X 로 끊긴다.

   영어 원어(Unreachable/AlreadyThere/PathFound)는 화면에만 둔다 — 자막은 세 이름을
   읽지 않는다. 화면이 자막을 옮겨 적으면 화면이 더하는 정보가 0 이 된다. */

function fitFont(ctx, text, maxW, weight, size) {
  let s = size;
  ctx.font = disp(weight, s);
  while (s > 8 && ctx.measureText(text).width > maxW) { s -= 1; ctx.font = disp(weight, s); }
  return s;
}

/** 훑는 빛 — 화면이 지금 가리키는 칸 위를 계속 지나간다. 정적 프레임 방지도 겸한다. */
function scan(ctx, x, y, bw, bh, t, alpha) {
  ctx.save();
  ctx.beginPath(); ctx.rect(x + 3, y + 3, bw - 6, bh - 6); ctx.clip();
  const sx = x + ((t * 95) % (bw + 90)) - 45;
  ctx.globalAlpha = alpha;
  ctx.fillStyle = tone('accent');
  ctx.fillRect(sx, y, 45, bh);
  ctx.restore();
  ctx.globalAlpha = 1;
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const cx = w / 2;

    const BX = 8, BW = 206, RH = 52, R0 = 62, RGAP = 64;
    const SY = R0 + RGAP;                       // 뭉뚱그린 칸 = 가운데 줄 자리
    const SKX = 254, SKW = w - SKX - 8;

    const beforeK = ease(cue(spec.beforeCue ?? 0, 0.2, 0.5));
    const sinkK = ease(cue(spec.sinkCue ?? 1, 0.2, 0.5));
    const splitK = ease(cue(spec.splitCue ?? 2, 0.2, 0.6));

    ctx.textBaseline = 'alphabetic';

    // 무엇에 대한 화면인지 — 자막이 말하지 않는 한 줄
    if (beforeK > 0.02) {
      ctx.globalAlpha = beforeK * 0.9;
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
      ctx.fillText(spec.caption || '길찾기 함수가 돌려주는 값', cx, 34);
      ctx.globalAlpha = 1;
    }

    /* ── 옛 처리 : 아무 데나 '도착' ───────────────
       화살표가 뭉뚱그린 칸에서 곧장 '도착' 으로 간다. 판단이 없다는 게 요점이다. */
    if (sinkK > 0.02) {
      const dim = 1 - splitK * 0.72;
      ctx.globalAlpha = clamp(sinkK * 1.5) * dim;

      const ax0 = BX + BW, ax1 = SKX, ay = SY + RH / 2;
      const ak = easeOut(sinkK);
      ctx.lineWidth = 3; ctx.lineCap = 'round';
      ctx.strokeStyle = tone('ink');
      ctx.beginPath(); ctx.moveTo(ax0, ay); ctx.lineTo(lerp(ax0, ax1, ak), ay); ctx.stroke();
      if (ak > 0.9) {
        ctx.beginPath();
        ctx.moveTo(ax1 - 9, ay - 6); ctx.lineTo(ax1, ay); ctx.lineTo(ax1 - 9, ay + 6);
        ctx.stroke();
      }

      ctx.fillStyle = '#000';
      roundRect(ctx, SKX, SY, SKW, RH, 5); ctx.fill();
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      roundRect(ctx, SKX, SY, SKW, RH, 5); ctx.stroke();
      ctx.textAlign = 'center';
      fitFont(ctx, spec.sink?.label || '도착', SKW - 16, 900, 20);
      ctx.fillStyle = tone('ink');
      ctx.fillText(spec.sink?.label || '도착', SKX + SKW / 2, SY + RH / 2 + 7);

      if (spec.sink?.edge) {
        ctx.font = disp(700, 10);
        ctx.fillStyle = tone('sub');
        ctx.fillText(spec.sink.edge, SKX + SKW / 2, SY - 10);
      }
      ctx.globalAlpha = 1;

      // 갈라지는 순간 이 경로는 끊긴다
      if (splitK > 0.05) {
        const mx = (ax0 + ax1) / 2, s = 10 * easeOut(splitK);
        ctx.globalAlpha = clamp(splitK * 1.6);
        ctx.lineWidth = 4; ctx.strokeStyle = tone('ink');
        ctx.beginPath();
        ctx.moveTo(mx - s, ay - s); ctx.lineTo(mx + s, ay + s);
        ctx.moveTo(mx + s, ay - s); ctx.lineTo(mx - s, ay + s);
        ctx.stroke();
        ctx.globalAlpha = 1;
      }
    }

    /* ── 전 : 뭉뚱그린 칸 하나 ─────────────────── */
    if (beforeK > 0.02 && splitK < 0.99) {
      ctx.globalAlpha = clamp(beforeK * 1.5) * (1 - splitK);
      ctx.fillStyle = '#000';
      roundRect(ctx, BX, SY, BW, RH, 5); ctx.fill();
      ctx.setLineDash([7, 6]);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      roundRect(ctx, BX, SY, BW, RH, 5); ctx.stroke();
      ctx.setLineDash([]);

      ctx.textAlign = 'left';
      fitFont(ctx, spec.before?.label || '길찾기 결과', BW - 28, 900, 15);
      ctx.fillStyle = tone('ink');
      ctx.fillText(spec.before?.label || '길찾기 결과', BX + 14, SY + 24);
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10.5);
      ctx.fillText(spec.before?.note || '하나로 뭉뚱그림', BX + 14, SY + 41);
      ctx.globalAlpha = 1;

      if (splitK < 0.05) scan(ctx, BX, SY, BW, RH, t, clamp(beforeK) * 0.15);
    }

    /* ── 후 : 이름 셋 ───────────────────────────
       가운데 자리에서 위아래로 벌어진다 — 하나가 셋이 됐다는 게 움직임으로 보여야 한다. */
    const rows = spec.after || [];
    rows.forEach((r, i) => {
      const k = clamp(splitK * 1.25 - i * 0.06);
      if (k <= 0.02) return;
      const ry = lerp(SY, R0 + i * RGAP, easeOut(k));
      ctx.globalAlpha = clamp(k * 1.5);
      ctx.fillStyle = '#000';
      roundRect(ctx, BX, ry, BW, RH, 5); ctx.fill();
      ctx.lineWidth = 3;
      if (r.lit) {
        setShadow(ctx, GLOW, 16, 0);
        ctx.strokeStyle = tone('accent');
      } else ctx.strokeStyle = tone('track');
      roundRect(ctx, BX, ry, BW, RH, 5); ctx.stroke();
      clearShadow(ctx);

      ctx.textAlign = 'left'; ctx.textBaseline = 'middle';
      fitFont(ctx, r.label, BW - 110, 900, 15);
      ctx.fillStyle = r.lit ? tone('accent') : tone('ink');
      ctx.fillText(r.label, BX + 14, ry + RH / 2);

      if (r.code) {
        ctx.textAlign = 'right';
        ctx.font = mono(700, 10);
        ctx.fillStyle = tone('sub');
        ctx.fillText(r.code, BX + BW - 14, ry + RH / 2);
      }
      ctx.textBaseline = 'alphabetic';
      ctx.globalAlpha = 1;

      if (r.lit && k > 0.6) scan(ctx, BX, ry, BW, RH, t, 0.15);
    });

    ctx.textAlign = 'left'; ctx.textBaseline = 'alphabetic';
  }
};
