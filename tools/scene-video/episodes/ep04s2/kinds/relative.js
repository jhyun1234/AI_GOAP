import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* relative — 같은 명령이 재고에 따라 무게가 달라진다.

   같은 자리에서 목표선만 옮긴다. 위는 절대값이라 목표선이 30 에 고정돼 있고, 현재 재고가
   28 이면 실제로 캐는 양은 그 사이의 손톱만 한 구간뿐이다(absCue). 아래는 상대값이라
   목표선이 현재 재고에서 +10 만큼 떨어진 자리에 선다(relCue). 두 구간의 **길이 차이**가
   이 결함의 전부이므로, 숫자를 크게 쓰는 대신 막대 옆의 빈 구간을 그대로 보여 준다.

   🔴 목표선의 절대 좌표를 화면에 적지 않았다. 원문이 말한 것은 '현재 재고 + 10' 이지
   합쳐진 값이 아니다. 28 + 10 을 계산해 38 이라고 찍으면 원문에 없는 수치를 화면에
   올리게 된다 — 라벨은 '지금 재고 + 10' 그대로 둔다.

   계속 도는 것 = 재고 막대 안을 오른쪽으로 흐르는 빗금(창고가 계속 돌아간다). */

const HATCH = 3.0;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, scene, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));

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

    /* ── 멈추고 물었다 ────────────────────────────── */
    if (ak > 0.02) {
      const k = clamp(ak * 1.8);
      const s = spec.stopLabel || '';
      ctx.globalAlpha = k;
      ctx.textAlign = 'left';
      const fs = fit(s, 800, 12.5, w - 60, 9);
      ctx.font = disp(800, fs);
      const tw = ctx.measureText(s).width;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      roundRect(ctx, 8, 14, tw + 20, 26, 3); ctx.stroke();
      ctx.fillStyle = tone('accent');
      ctx.fillText(s, 18, 32);
      ctx.globalAlpha = 1;
    }

    /* 재고 막대 하나를 그린다 */
    const bar = (y, targetV, on, accent, gapLabel, headLabel) => {
      const bh = 28;
      /* 🔴 트랙·재고·빗금은 ak(자막 0)부터 보인다 — 2026-08-04 검수 정정 ②.
         전에는 셋 다 on(=bk/rk)에 걸려 있어서 자막 0 구간 3.46초가 칩 하나 빼고 전부
         검정이었다. 14행 주석이 "계속 도는 것 = 재고 막대 안을 흐르는 빗금"이라고
         선언해 놓고 정작 샷의 첫 1/3 에 그 빗금이 존재하지 않았다.
         창고는 질문을 던지기 전에도 돌고 있다 — 그게 이 샷이 말하는 바다.
         목표선·라벨은 그대로 on 에 걸어 둔다(그건 각 안이 도착해야 뜨는 것이다). */
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
      ctx.globalAlpha = clamp(on * 1.6);

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
      ctx.fillText(spec.stockLabel || '현재 재고', sx, 242);
      ctx.font = mono(700, 15); ctx.fillStyle = tone('sub');
      ctx.fillText(String(stock), sx, 258);
      ctx.globalAlpha = 1;
    }

    if (rk > 0.5 && spec.intent) {
      const k = clamp((rk - 0.5) / 0.5);
      ctx.globalAlpha = k;
      ctx.textAlign = 'left';
      const fs = fit(spec.intent, 800, 12, w - 16, 8.5);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(spec.intent, 8, 278);
      ctx.globalAlpha = 1;
    }

    /* ── 남는 한 줄 ───────────────────────────────── */
    /* 🔴 훅을 그리는 코드는 원래 nextup(예고 샷)에만 있었다. 2026-08-03 분할에서 이 편이
       예고 샷 없이 relative 로 끝나게 되어, 1편 oneline 에 옮겨 둔 것과 같은 블록을 여기에도
       뒀다. 안 두면 ep01s erasure 가 만들고 ep02s·ep03s 가 이어 온 마지막 한 줄이 이 편에서만
       사라진다. 자리를 내려고 재고 라벨 250→242 · 재고 값 268→258 · intent 292→278 로 올렸다. */
    /* 🔴 훅을 relCue(자막 2)가 아니라 hookCue(예고 자막)에 물린다 — 2026-08-04 검수 정정 ①.
       rk>0.6 이면 자막2 +1.458초, 즉 상대값 나레이션이 도는 중에 이 편의 결론이 옆에 붙어
       한 문단처럼 읽혔다. 예고 자막에 물리면 훅이 혼자 서고, 그동안 화면도 산다
       (예고 자막엔 cue 가 없어 그냥 두면 그 구간이 통째로 멈춘다 — ep05s 가 그렇게
       정적 구간 6.6초를 만들었다). */
    const hook = spec.hook || scene?.hook;
    const hkk = ease(cue(spec.hookCue ?? spec.relCue ?? 2, 0.15, 0.5));
    if (hook && hkk > 0.02) {
      const k = clamp(hkk * 1.6);
      const s2 = Math.min(1, spring(k));
      ctx.globalAlpha = k;
      ctx.textAlign = 'center';
      const fs = fit(String(hook), 900, 15, w - 30, 10);
      ctx.font = disp(900, fs * s2); ctx.fillStyle = tone('ink');
      ctx.fillText(String(hook), w / 2, 299);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
