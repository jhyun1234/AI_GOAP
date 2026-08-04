import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* relative — 같은 명령이 재고에 따라 무게가 달라진다.

   같은 자리에서 목표선만 옮긴다. 위는 절대값이라 목표선이 30 에 고정돼 있고, 현재 재고가
   28 이면 실제로 캐는 양은 그 사이의 손톱만 한 구간뿐이다(absCue). 아래는 상대값이라
   목표선이 현재 재고에서 +10 만큼 떨어진 자리까지 밀려 나간다(relCue). 두 구간의
   **길이 차이**가 이 결함의 전부이므로, 숫자를 크게 쓰는 대신 막대 옆의 빈 구간을 보여 준다.

   🔴 목표선의 절대 좌표를 화면에 적지 않았다. 원문이 말한 것은 '현재 재고 + 10' 이지
   합쳐진 값이 아니다. 28 + 10 을 계산해 38 이라고 찍으면 원문에 없는 수치를 화면에
   올리게 된다 — 라벨은 '지금 재고 + 10' 그대로 둔다.

   🔴 머리 칩이 「M1-C 리뷰」다(2026-08-04, ep04s-3). 이 편은 사건 둘을 담는데 둘을 잇는
   것이 인과가 아니라 **출처**뿐이라, S1 blindspot 과 같은 자리·같은 모양의 칩을 여기에도
   찍는다. 원문 근거 둘이 실재한다 — "M1-C 리뷰 중에 문제가 발견됐습니다"(6절) ·
   "M1-C 구현 직후 리뷰에서 결함이 나왔습니다"(4절 4단계).
   「구현을 멈추고 물었다」는 칩에서 밀려나 그 옆의 작은 글자로 갔고, relCue 에서 뜬다 —
   멈추고 물은 것이 실제로 값을 바꾼 자리가 거기이기 때문이다. 나레이션은 이 말을 한 번도
   하지 않는다(그 규칙 자체의 소유자는 ep03s S10 askfirst 다).

   🔴 훅 블록을 통째로 뺐다. ep04s2 에서는 이 그림이 편의 마지막 샷이라 여기에 훅을 그렸는데,
   ep04s-3 에서는 뒤에 tworules 가 한 샷 더 있다. 남겨 두면 재고 막대 이야기를 하는 화면에
   여가·규칙 이야기의 결론이 나란히 붙는다 — ep04s2 검수 4-A 가 정확히 그것을 지적했다.
   훅이 빠져 자리가 났으므로 재고 라벨 242→250 · 재고 값 258→268 · intent 278→292 로
   되돌렸다(ep04s 1차본과 같은 좌표다).

   계속 도는 것 = 재고 막대 안을 오른쪽으로 흐르는 빗금(창고가 계속 돌아간다).
   🔴 이 빗금은 ak(자막 0)부터 돈다. ep04s2 검수 정정 ②로 들어온 수정이고 되돌리지 않았다 —
   되돌리면 자막 0 구간(약 3.3초)이 칩 하나 빼고 전부 검정이 된다. */

const HATCH = 3.0;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));

    const ak = ease(cue(spec.askCue ?? 0, 0.15, 0.6));
    const bk = ease(cue(spec.absCue ?? 1, 0.15, 0.62));
    const rk = ease(cue(spec.relCue ?? 2, 0.15, 0.62));

    ctx.textBaseline = 'alphabetic';
    const fit = (txt, weight, start, max, min = 7.5) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const SCALE = 44;
    const bx0 = 30, bx1 = w - 22;
    const xOf = v => bx0 + (v / SCALE) * (bx1 - bx0);
    const stock = spec.stock ?? 28;
    const absT = spec.absTarget ?? 30;
    const plus = spec.plus ?? 10;

    /* ── M1-C 리뷰 칩 + 멈추고 물었다 ─────────────── */
    let chipRight = 8;
    if (spec.review && ak > 0.02) {
      const k = clamp(ak * 1.8);
      ctx.globalAlpha = k;
      const fs = fit(spec.review, 800, 12, w - 60, 9);
      ctx.font = disp(800, fs);
      const tw = ctx.measureText(spec.review).width;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, 8, 14, tw + 20, 26, 3); ctx.stroke();
      ctx.textAlign = 'left';
      ctx.fillStyle = tone('accent');
      ctx.fillText(spec.review, 18, 32);
      chipRight = 8 + tw + 20;
      ctx.globalAlpha = 1;
    }
    if (spec.stopLabel && rk > 0.15) {
      const k = clamp((rk - 0.15) / 0.4);
      const lx = chipRight + 12;
      ctx.globalAlpha = k;
      ctx.textAlign = 'left';
      const fs = fit(spec.stopLabel, 700, 11, w - lx - 10, 8.5);
      ctx.font = disp(700, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.stopLabel, lx, 32);
      ctx.globalAlpha = 1;
    }

    /* 재고 막대 하나를 그린다 */
    const bar = (y, targetV, on, accent, gapLabel, headLabel) => {
      const bh = 28;
      /* 트랙·재고·빗금은 ak(자막 0)부터 보인다. 목표선·라벨만 on(=bk/rk)에 걸린다 —
         그건 각 안이 도착해야 뜨는 것이다. 창고는 질문을 던지기 전에도 돌고 있다. */
      const vis = Math.max(on, ak * 0.55);
      ctx.globalAlpha = clamp(vis * 1.6);

      // 트랙
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, bx0, y, bx1 - bx0, bh, 3); ctx.stroke();

      // 현재 재고
      const sx = xOf(stock);
      ctx.fillStyle = tone('ink');
      ctx.globalAlpha = clamp(vis * 1.6) * 0.3;
      ctx.fillRect(bx0 + 2, y + 2, sx - bx0 - 2, bh - 4);

      /* 계속 도는 것 — 재고 안을 흐르는 빗금 */
      ctx.save();
      ctx.beginPath();
      ctx.rect(bx0 + 2, y + 2, sx - bx0 - 2, bh - 4);
      ctx.clip();
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.globalAlpha = clamp(vis * 1.6) * 0.4;
      const off = frac(t / HATCH) * 22;
      for (let x = bx0 - 30 + off; x < sx + 30; x += 22) {
        ctx.beginPath();
        ctx.moveTo(x, y + bh); ctx.lineTo(x + 14, y);
        ctx.stroke();
      }
      ctx.restore();
      ctx.globalAlpha = clamp(on * 1.6);

      // 목표선
      const tx = xOf(targetV);
      const grow = ease(clamp(on / 0.8));
      const tgt = lerp(sx, tx, grow);
      ctx.strokeStyle = accent ? tone('accent') : tone('ink');
      ctx.lineWidth = 4;
      if (accent) setShadow(ctx, GLOW, 10, 0);
      ctx.beginPath(); ctx.moveTo(tgt, y - 8); ctx.lineTo(tgt, y + bh + 8); ctx.stroke();
      clearShadow(ctx);

      // 실제로 더 캐는 구간
      ctx.fillStyle = accent ? tone('accent') : tone('ink');
      ctx.globalAlpha = clamp(on * 1.6) * (accent ? 0.75 : 0.5);
      ctx.fillRect(sx, y + 4, Math.max(0, tgt - sx), bh - 8);
      ctx.globalAlpha = clamp(on * 1.6);

      // 구간 라벨
      if (on > 0.55 && gapLabel) {
        const k = clamp((on - 0.55) / 0.45);
        ctx.globalAlpha = k;
        ctx.textAlign = 'left';
        const fs = fit(gapLabel, 900, 15, w - tgt - 14, 10);
        ctx.font = disp(900, fs);
        ctx.fillStyle = accent ? tone('accent') : tone('ink');
        const lx = Math.min(tgt + 10, w - 10 - ctx.measureText(gapLabel).width);
        ctx.fillText(gapLabel, lx, y + bh + 26);
        ctx.globalAlpha = 1;
      }

      // 머리 라벨
      if (headLabel) {
        ctx.globalAlpha = clamp(on * 1.6);
        ctx.textAlign = 'left';
        const fs = fit(headLabel, 800, 12, w - 16, 8.5);
        ctx.font = disp(800, fs);
        ctx.fillStyle = accent ? tone('accent') : tone('sub');
        ctx.fillText(headLabel, bx0 - 22, y - 14);
        ctx.globalAlpha = 1;
      }
      ctx.globalAlpha = 1;
    };

    bar(78, absT, bk, false, spec.absGain || '', spec.absLabel || '');
    bar(178, stock + plus, rk, true, spec.relGain || '', spec.relLabel || '');

    /* ── 현재 재고 표시 ───────────────────────────── */
    if (bk > 0.25) {
      const k = clamp((bk - 0.25) / 0.5);
      const sx = xOf(stock);
      ctx.globalAlpha = k * 0.8;
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.setLineDash([5, 6]);
      ctx.beginPath(); ctx.moveTo(sx, 70); ctx.lineTo(sx, 234); ctx.stroke();
      ctx.setLineDash([]);
      ctx.textAlign = 'center';
      ctx.font = disp(700, 10.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.stockLabel || '현재 재고', sx, 250);
      ctx.font = mono(700, 15); ctx.fillStyle = tone('sub');
      ctx.fillText(String(stock), sx, 268);
      ctx.globalAlpha = 1;
    }

    if (rk > 0.5 && spec.intent) {
      const k = clamp((rk - 0.5) / 0.5);
      ctx.globalAlpha = k;
      ctx.textAlign = 'left';
      const fs = fit(spec.intent, 800, 12, w - 16, 8.5);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(spec.intent, 8, 292);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
