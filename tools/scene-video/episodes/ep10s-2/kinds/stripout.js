import {
  disp, ease, clamp, lerp,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* stripout — 매달아 둔 것을 뜯어내고, 빈 칸에 신호 하나만 남긴다.

   이 편의 뒤집힘이다. 앞 샷들에서 위→아래로 붙던 각진 흰 상자가 여기서 **가로로**
   빠져나간다 — 위아래 축을 비워 주는 움직임이라, 다음 샷에서 사슬이 그 자리를
   아래에서부터 채우는 것과 한 몸으로 읽힌다.

   🔑 뜯어낸 자리에는 **흐린 테두리 자국을 남긴다.** 원문이 '걷어내고'라고 쓰지
   '없던 일로 했다'고 쓰지 않기 때문이다 — 고민했던 설계가 있었다는 사실은 남는다.

   🔑 ACCEPT 는 새 시스템으로 옆에 서지 않고 **목표 목록 칸 안에** 앉는다.
   원문이 '"수락"이라는 신호만 목수의 목표 목록에 얹어줬습니다' 라고 쓴 그대로다.

   🔴 빠져나가는 상자는 **가장자리에 닿기 전에 사라진다.** 화면 밖으로 끌고 나가면
      테두리와 글자가 canvas 경계에서 잘린 채로 남는다 — 밀려 나가는 뜻은 이동이
      지지, 잘림이 지는 것이 아니다.

   자막이 한 줄이라 앞뒤로 가른다 — spec.split 앞이 뜯어냄, 뒤가 남김이다. */

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

    const split = spec.split ?? 0.5;
    const c0 = cue(spec.stripCue ?? 0, 0.15, 0.85);
    const sk = ease(clamp(c0 / split));                    // 상자 둘이 빠져나간다
    const ck = ease(clamp((c0 - split) / 0.42));           // 칩 하나가 내려앉는다

    const CX = w / 2;
    const GX = 24, GW = w - 48, GY = 44, GH = 48;          // 목표 목록 칸
    const BW = 150, BX = (w - BW) / 2, BH = 32;
    const B1 = 118, B2 = 164;
    const CHW = 132, CHH = 30;                             // ACCEPT 칩
    const PULL = w / 2 - 20;                               // 끌려 나가는 거리

    /* ── 상단 라벨 ────────────────────────────────── */
    if (spec.label) {
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.label, 10, GY - 12);
    }

    /* ── 아래쪽 레일 — 비워진 자리를 계속 흐른다 ──── */
    ctx.save();
    ctx.setLineDash([9, 8]);
    ctx.lineDashOffset = -(t * 16) % 17;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(20, h - 40); ctx.lineTo(w - 20, h - 40); ctx.stroke();
    ctx.restore();

    /* ── 뜯겨 나가는 각진 상자 둘 ─────────────────── */
    const drop = spec.drop ?? [];
    for (let i = 0; i < drop.length; i++) {
      const y = i === 0 ? B1 : B2;
      const dir = i === 0 ? -1 : 1;
      const k = ease(clamp(sk * 1.3 - i * 0.16));

      /* 있던 자리에 남는 흐린 자국 — 계속 흐른다 */
      ctx.save();
      ctx.setLineDash([7, 7]);
      ctx.lineDashOffset = (t * 11 * dir) % 14;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.rect(BX, y, BW, BH); ctx.stroke();
      ctx.restore();

      const a = clamp(1 - k * 1.12);
      if (a <= 0.02) continue;
      const x = BX + dir * PULL * k;

      ctx.globalAlpha = a;
      // 매달려 있던 줄 — 상자와 함께 끌려 나간다
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(CX, i === 0 ? GY + GH : B1 + BH);
      ctx.lineTo(x + BW / 2, y);
      ctx.stroke();

      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.rect(x, y, BW, BH); ctx.stroke();

      ctx.textAlign = 'center';
      const fs = fit(drop[i], 800, 15, BW - 20, 9);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(drop[i], x + BW / 2, y + BH / 2 + fs * 0.36);
      ctx.globalAlpha = 1;
    }

    /* ── 목표 목록 칸 ─────────────────────────────── */
    ctx.save();
    ctx.setLineDash([10, 8]);
    ctx.lineDashOffset = -(t * 14) % 18;
    ctx.strokeStyle = ck > 0.4 ? tone('accent') : tone('sub');
    ctx.lineWidth = ck > 0.4 ? 4 : 3;
    roundRect(ctx, GX, GY, GW, GH, 13); ctx.stroke();
    ctx.restore();

    /* 빈 자리 — 칩이 앉기 전까지 안쪽에 빈 테두리가 있다 */
    if (ck < 0.98) {
      ctx.save();
      ctx.globalAlpha = 1 - ck;
      ctx.setLineDash([6, 6]);
      ctx.lineDashOffset = (t * 10) % 12;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, CX - CHW / 2, GY + (GH - CHH) / 2, CHW, CHH, CHH / 2); ctx.stroke();
      ctx.restore();
    }

    /* ── 남는 신호 하나 — 위에서 칸 안으로 내려앉는다 ── */
    if (ck > 0.02 && spec.keep) {
      const y = lerp(8, GY + (GH - CHH) / 2, ease(ck));
      ctx.globalAlpha = clamp(ck * 2.4);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      setShadow(ctx, GLOW, 10, 0);
      roundRect(ctx, CX - CHW / 2, y, CHW, CHH, CHH / 2); ctx.stroke();
      clearShadow(ctx);

      ctx.textAlign = 'center';
      const fs = fit(spec.keep, 900, 18, CHW - 24, 11);
      ctx.font = disp(900, fs); ctx.fillStyle = tone('accent');
      ctx.fillText(spec.keep, CX, y + CHH / 2 + fs * 0.36);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
