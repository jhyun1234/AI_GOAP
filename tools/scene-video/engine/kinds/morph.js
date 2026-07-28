import { span, ease, easeOut, lerp, rnd, fitCanvas, mkCanvas, tone, roundRect } from '../lib.js';

/* morph — 하나의 덩어리가 변형되어 다음 상태가 된다. 컷 전환이 아니라 연속 변형.
   mode:"shatter_reveal" = 덩어리가 부서지고, 뒤에 숨어 있던 것이 딱 하나 드러난다. */
export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, cue, nLines }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const cx = w / 2, cy = h / 2;
    const N = spec.from?.shards ?? 16;

    /* "깨지죠"라고 말할 때 깨지고, "드러나거든요"라고 말할 때 드러난다.
       🔴 드러남의 lead 를 길게 잡아 부서짐과 겹친다. 붙여서 순서대로 두면
          파편이 다 사라진 뒤 드러남이 시작될 때까지 화면이 비는 구간이 생긴다
          (실측 1.37초 — 가이드가 금지하는 0.5초 이상 정적 구간). */
    const brk = ease(cue(spec.breakCue ?? 1, 0.15, 0.72));
    const rev = ease(cue(spec.revealCue ?? nLines - 1, spec.revealLead ?? 1.3, 0.6));

    // 덩어리 → 파편
    const BW = w * 0.72, BH = h * 0.34;
    const cols = Math.ceil(Math.sqrt(N * BW / BH)), rows = Math.ceil(N / cols);
    const sw = BW / cols, sh = BH / rows;

    for (let i = 0; i < N; i++) {
      const c = i % cols, r = Math.floor(i / cols);
      const x0 = cx - BW / 2 + c * sw, y0 = cy - BH / 2 + r * sh;
      const ang = rnd(i * 29 + 7) * Math.PI * 2;
      const dist = (60 + rnd(i * 53 + 11) * 190) * easeOut(brk);
      const x = x0 + Math.cos(ang) * dist;
      const y = y0 + Math.sin(ang) * dist * 0.8;
      const a = 1 - brk;
      if (a <= 0.02) continue;
      ctx.save();
      ctx.globalAlpha = a;
      ctx.translate(x + sw / 2, y + sh / 2);
      ctx.rotate(rnd(i * 71 + 3) * brk * 1.6 - brk * 0.8);
      // 3색 — 파편은 흰 아웃라인, fill 은 검정. 어두운 회색 fill 은 검정에서 안 보인다
      ctx.fillStyle = '#000000';
      ctx.strokeStyle = i % 4 === 0 ? tone('ink') : tone('track');
      ctx.lineWidth = 3;
      roundRect(ctx, -sw / 2 + 2, -sh / 2 + 2, sw - 4, sh - 4, 2);
      ctx.fill(); ctx.stroke();
      ctx.restore();
    }

    if (brk < 0.55) {
      ctx.globalAlpha = 1 - brk / 0.55;
      ctx.fillStyle = tone('sub'); ctx.textAlign = 'center';
      ctx.font = '700 12px Consolas, monospace';
      ctx.fillText(spec.from?.label || '', cx, cy - BH / 2 - 16);
      ctx.globalAlpha = 1;
    }

    // 뒤에 있던 것 — 하나만 남는다
    if (rev > 0) {
      const s = lerp(0.85, 1, rev);   // 가이드: scale 0.8~1.15 범위 (레이아웃 시프트 방지)
      ctx.save();
      ctx.globalAlpha = rev;
      ctx.translate(cx, cy); ctx.scale(s, s);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.fillStyle = 'rgba(0,255,136,.12)';
      roundRect(ctx, -86, -23, 172, 46, 4); ctx.fill(); ctx.stroke();
      ctx.fillStyle = tone('accent'); ctx.textAlign = 'center';
      ctx.font = '900 15px Pretendard, "Malgun Gothic", sans-serif';
      ctx.fillText(spec.to?.label || '', 0, 6);
      ctx.restore();

      // 사방을 훑는 시선 — 여기 말고는 없다
      ctx.globalAlpha = rev * 0.55;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.setLineDash([4, 7]);
      ctx.beginPath(); ctx.arc(cx, cy, 112 - rev * 14, 0, 7); ctx.stroke();
      ctx.setLineDash([]);

      // 나머지 자리는 비었다는 표시 — "한 곳뿐"이 눈으로 확인된다.
      // 이게 없으면 시청자는 화면 밖에 더 있는지 알 수 없다.
      const xs = [[-1, -1], [1, -1], [-1, 1], [1, 1]];
      ctx.globalAlpha = rev * 0.42; ctx.lineWidth = 3; ctx.lineCap = 'round';
      ctx.strokeStyle = tone('ink');
      for (const [sx, sy] of xs) {
        const x = cx + sx * w * 0.30, y = cy + sy * h * 0.28, r = 9;
        ctx.beginPath();
        ctx.moveTo(x - r, y - r); ctx.lineTo(x + r, y + r);
        ctx.moveTo(x + r, y - r); ctx.lineTo(x - r, y + r);
        ctx.stroke();
      }
      ctx.globalAlpha = 1;
    }
  }
};
