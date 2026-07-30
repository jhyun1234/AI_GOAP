import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* hidefault — 두 어긋남이 같은 화면에서 되받는다.

   결말 자리. 왼쪽에는 S1 의 빈 말풍선 셋이 다시 서고(없는 신호), 오른쪽에는 S4 의 거꾸로
   차오르는 게이지가 다시 놓인다(거꾸로 신호). 가운데에는 '에러 · 0건' 도장이 찍힌다 —
   둘 다 조용했다는 표시. 회차 안에서 되받는 자리다.

   회차의 기본 도형 안에서 이 그림의 몫은 '왼쪽엔 없는 신호, 오른쪽엔 거꾸로 신호'.
   계속 도는 것 = 중앙 도장의 잉크가 안팎으로 번지는 호흡. */

const BREATH = 2.8;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const L = spec.left || {}, R = spec.right || {};
    const rv = ease(cue(spec.reviveCue ?? 1, 0.15, 0.55));
    const sk = ease(cue(spec.stampCue ?? 2, 0.15, 0.5));

    ctx.textBaseline = 'alphabetic';

    const midX = w / 2;
    const paneW = (w - 24 - 60) / 2;   // 중앙 60px 는 도장 자리
    const leftX = 12;
    const rightX = w - 12 - paneW;
    const paneTop = 44;
    const paneBot = h - 88;

    /* ── 왼쪽 : 빈 말풍선 셋 (없는 신호) ─────────── */
    {
      ctx.textAlign = 'left';
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
      ctx.fillText(L.label || '', leftX, paneTop - 8);

      const rowN = 3;
      const rowH = (paneBot - paneTop) / rowN;
      for (let i = 0; i < rowN; i++) {
        const y = paneTop + i * rowH + 8;
        // 사람 아이콘
        const cxIcon = leftX + 14;
        const cyIcon = y + 20;
        ctx.globalAlpha = clamp(rv * 1.4);
        ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
        ctx.beginPath(); ctx.arc(cxIcon, cyIcon, 10, 0, Math.PI * 2); ctx.stroke();
        // 빈 말풍선
        const bx = cxIcon + 16, by = y + 4;
        const bw = paneW - (bx - leftX) - 4;
        const bh = rowH - 12;
        ctx.setLineDash([5, 5]);
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        roundRect(ctx, bx, by, bw, bh, 3); ctx.stroke();
        ctx.setLineDash([]);
        // 커서 깜빡임
        const bl = frac(t / 0.86 + i * 0.31);
        if (bl < 0.5) {
          ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
          ctx.beginPath();
          ctx.moveTo(bx + 8, by + 6); ctx.lineTo(bx + 8, by + bh - 6);
          ctx.stroke();
        }
        ctx.globalAlpha = 1;
      }
      // 부제
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 9);
      ctx.fillText(L.note || '', leftX, paneBot + 12);
    }

    /* ── 오른쪽 : 거꾸로 게이지 (거꾸로 신호) ─────── */
    {
      ctx.textAlign = 'left';
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
      ctx.fillText(R.label || '', rightX, paneTop - 8);

      const bx = rightX + paneW / 2 - 18;
      const bwGauge = 36;
      // 트랙
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, bx, paneTop + 8, bwGauge, paneBot - paneTop - 16, 4); ctx.stroke();
      // 채우기 (rv 로 차오름) — 채워질수록 굶주림
      const fillPct = 0.9 * rv;
      const bh = paneBot - paneTop - 16;
      const fh = bh * fillPct;
      ctx.fillStyle = tone('ink');
      roundRect(ctx, bx + 3, paneTop + 8 + bh - fh + 1.5, bwGauge - 6, Math.max(0, fh - 3), 3); ctx.fill();
      // 아래 방향 화살표 — 이 게이지는 GOAP 가 낮추라고 시키던 것이다
      ctx.fillStyle = tone('ink'); ctx.font = disp(900, 18);
      ctx.textAlign = 'center';
      ctx.fillText('↓', bx + bwGauge / 2, paneBot + 6);
      // 부제
      ctx.textAlign = 'left';
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 9);
      let ns = 9;
      const nt = R.note || '';
      while (ns > 7 && ctx.measureText(nt).width > paneW) { ns -= 0.5; ctx.font = disp(700, ns); }
      ctx.fillText(nt, rightX, paneBot + 12);
    }

    /* ── 중앙 : '에러 · 0건' 도장 (호흡) ───────── */
    {
      const cx = midX;
      const cy = (paneTop + paneBot) / 2;
      const breath = (Math.sin(t * Math.PI * 2 / BREATH) + 1) / 2;   // 0~1
      const baseR = 34;
      const r = baseR + breath * 3;
      const alpha = sk > 0.02 ? clamp(sk * 1.4) : 0.35;

      ctx.globalAlpha = alpha;
      // 외곽 링
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      setShadow(ctx, GLOW, 12 + breath * 4, 0);
      ctx.beginPath(); ctx.arc(cx, cy, r, 0, Math.PI * 2); ctx.stroke();
      clearShadow(ctx);
      // 안쪽 채움 (호흡)
      ctx.globalAlpha = alpha * (0.15 + breath * 0.1);
      ctx.fillStyle = tone('accent');
      ctx.beginPath(); ctx.arc(cx, cy, r - 4, 0, Math.PI * 2); ctx.fill();
      // 라벨
      ctx.globalAlpha = alpha;
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('accent'); ctx.font = disp(900, 13);
      const words = (spec.stamp || 'error 0').split('·');
      const w1 = (words[0] || '').trim();
      const w2 = (words[1] || '').trim();
      ctx.fillText(w1, cx, cy - 4);
      ctx.font = disp(900, 15);
      ctx.fillText(w2, cx, cy + 14);
      ctx.globalAlpha = 1;
    }

    /* ── 하단 : reprise 한 줄 ─────────────────── */
    if (sk > 0.4) {
      ctx.globalAlpha = clamp((sk - 0.4) * 1.6);
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('sub'); ctx.font = disp(800, 12);
      let rs = 12;
      const rt = spec.reprise || '';
      while (rs > 9 && ctx.measureText(rt).width > w - 24) { rs -= 0.5; ctx.font = disp(800, rs); }
      ctx.fillText(rt, w / 2, h - 30);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
