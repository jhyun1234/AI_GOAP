import {
  disp, ease, clamp, lerp, frac, rnd,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* sprawl — 퍼진 것과 닿은 것.

   원문의 대비는 "4096번이나 후보를 뒤지고도 4단계짜리 답을 못 찾았다"이다.
   덩어리 둘의 크기를 재는 그림(막대·블록)으로 그리면 '4096이 크다'까지만 남는다.
   이 글이 말하는 건 크기가 아니라 **방향**이다 — 넓게 퍼졌는데 정답 쪽으로는 한 걸음도
   안 갔다. 그래서 위는 왼쪽 출발점에서 오른쪽으로 넓게 번지다 만 점 4,096개
   (하나가 곧 열어본 후보 하나), 아래는 곧장 '철'에 닿는 네 걸음짜리 길 하나로 그렸다.
   각도는 ±1.25rad(약 ±71°)라 전방위가 아니라 **한쪽으로 열린 부채꼴**이다 — 탐색이
   출발점 주변만 훑었다는 뜻이고, `reads` 문구도 이 모양에 맞춰 적었다.

   🔴 점은 정확히 spec.count 개를 그린다. 화면이 주장하는 수와 실제로 그리는 수를 어긋나게
      두지 않기 위해서다. 위치는 전부 rnd(정수) 씨앗이라 프레임마다 같다.
   계속 도는 것 = 점들이 저마다의 위상으로 퍼졌다 줄었다 한다(탐색이 계속 돌고 있다). */

const RMAX = 214;      // 퍼짐의 최대 반경
const FLAT = 0.36;     // 세로로 눌러 담는 비율 — 캔버스를 넘지 않게
const BREATH = 3.1;    // 퍼졌다 줄어드는 주기(초)

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const ox = 30, oy = 92;
    const n = spec.count || 0;

    const ak = ease(cue(spec.answerCue ?? 1, 0.2, 0.6));
    const gk = ease(cue(spec.gapCue ?? 2, 0.2, 0.5));

    ctx.textBaseline = 'alphabetic';

    /* ── 열어본 후보 ──────────────────────────────
       점 하나 = 후보 하나. 전부 짧게 뻗다 만 자리다. */
    ctx.globalAlpha = 0.5;
    ctx.fillStyle = tone('ink');
    for (let i = 0; i < n; i++) {
      const ang = (rnd(i * 3 + 11) - 0.5) * 2.5;
      const rr = Math.sqrt(rnd(i * 3 + 12));
      const ph = rnd(i * 3 + 13);
      const breath = 0.62 + 0.38 * (0.5 - 0.5 * Math.cos(Math.PI * 2 * frac(t / BREATH + ph)));
      const r = rr * RMAX * breath;
      ctx.fillRect(ox + Math.cos(ang) * r - 1, oy + Math.sin(ang) * r * FLAT - 1, 2.4, 2.4);
    }
    ctx.globalAlpha = 1;

    // 어디서 출발했나
    ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
    setShadow(ctx, GLOW, 12, 0);
    ctx.beginPath(); ctx.arc(ox, oy, 7, 0, Math.PI * 2); ctx.stroke();
    clearShadow(ctx);

    // 몇 개였나
    ctx.textAlign = 'right';
    ctx.fillStyle = tone('ink'); ctx.font = disp(900, 26);
    ctx.fillText(String(n).replace(/\B(?=(\d{3})+(?!\d))/g, ','), w - 10, 34);
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
    ctx.fillText(spec.countLabel || '', w - 10, 50);

    /* ── 대비 ─────────────────────────────────────
       숫자 둘을 나란히 놓는 한 줄. 자막이 말하는 순간에 뜬다. */
    if (gk > 0.02 && spec.gapNote) {
      ctx.globalAlpha = clamp(gk * 1.6);
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('accent'); ctx.font = disp(900, 20);
      ctx.fillText(spec.gapNote, w / 2, 198);
      ctx.globalAlpha = 1;
    }

    /* ── 정답 ─────────────────────────────────────
       네 걸음이면 닿는다. 왼쪽에서 오른쪽으로 그어지고 마지막에 '철'이 켜진다. */
    const steps = spec.answer || [];
    const target = spec.target || '';
    const BY = 236, BH = 36, GAPX = 12;

    ctx.font = disp(800, 11);
    const bws = steps.map(s => ctx.measureText(s).width + 20);
    const tw = target ? ctx.measureText(target).width + 20 : 0;
    let totalW = bws.reduce((a, b) => a + b, 0) + GAPX * bws.length + tw;
    let x = Math.max(14, (w - totalW) / 2);

    ctx.textAlign = 'left';
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
    ctx.fillText(spec.answerLabel || '정답', x, BY - 10);

    ctx.textBaseline = 'middle';
    steps.forEach((s, i) => {
      const k = clamp(ak * (steps.length + 1) - i);
      const bw = bws[i];
      if (k > 0.02) {
        ctx.globalAlpha = k;
        ctx.fillStyle = '#000';
        roundRect(ctx, x, BY, bw, BH, 4); ctx.fill();
        ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
        roundRect(ctx, x, BY, bw, BH, 4); ctx.stroke();
        ctx.fillStyle = tone('ink'); ctx.font = disp(800, 11);
        ctx.textAlign = 'center';
        ctx.fillText(s, x + bw / 2, BY + BH / 2 + 1);
        ctx.globalAlpha = 1;
      }
      // 이음선
      const k2 = clamp(ak * (steps.length + 1) - i - 0.5);
      if (k2 > 0.02) {
        ctx.globalAlpha = k2;
        ctx.lineWidth = 3; ctx.strokeStyle = 'rgba(255,255,255,0.55)';
        ctx.beginPath();
        ctx.moveTo(x + bw, BY + BH / 2);
        ctx.lineTo(x + bw + GAPX * k2, BY + BH / 2);
        ctx.stroke();
        ctx.globalAlpha = 1;
      }
      x += bw + GAPX;
    });

    if (target) {
      const k = clamp(ak * (steps.length + 1) - steps.length);
      if (k > 0.02) {
        ctx.globalAlpha = k;
        setShadow(ctx, GLOW, 14, 0);
        ctx.fillStyle = tone('accent');
        roundRect(ctx, x, BY, tw, BH, 4); ctx.fill();
        clearShadow(ctx);
        ctx.fillStyle = '#000'; ctx.font = disp(900, 13);
        ctx.textAlign = 'center';
        ctx.fillText(target, x + tw / 2, BY + BH / 2 + 1);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left'; ctx.textBaseline = 'alphabetic';
  }
};
