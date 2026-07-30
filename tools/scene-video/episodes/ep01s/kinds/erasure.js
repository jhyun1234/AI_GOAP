import {
  disp, ease, clamp, lerp, frac, rnd,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* erasure — 지워지는 것과 남는 것.

   구성안의 지시는 "이 두 편이 다루는 시스템은 곧 전부 삭제된다"를 시청자에게 미리 알리라는
   것이고, 그 취지는 **애써 이해한 구조가 3편에서 사라져 헛수고가 되지 않게** 하는 데 있다.
   그래서 지워지는 대상을 '마을' 그림으로 지어내지 않고, 시청자가 방금 40여 초 동안 실제로
   받아 든 것들 — 이 회차가 세운 이름표 넷 — 을 지운다. 없는 장면을 만들지 않으면서
   "네가 이해한 게 사라진다"를 문자 그대로 보여주는 유일한 방법이다.

   🔑 `scene.hook` 을 되살린 자리이기도 하다. 그동안 이 필드는 어느 화면에도 안 뜨는 죽은
      필드였다(옛 title 이 outro 분기에서 안 그렸다). 지우는 획이 지나간 자리에 그 한 줄만
      남는다 — 마을은 지워져도 이 문장은 남는다는 뜻이고, 마지막 자막(예고)과 부딪히지 않는다.
      문구는 kind 에 하드코딩하지 않고 scene 에서 읽는다.

   회차의 기본 도형 안에서 이 그림의 몫은 '화면을 지우고 지나가는 획'이다.
   계속 도는 것 = 지워진 자리에서 흘러내리는 잔해. */

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, scene, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const items = spec.items || [];
    const ek = ease(cue(spec.eraseCue ?? 0, 0.1, 0.5));
    const hk = ease(cue(spec.eraseCue ?? 0, -0.85, 0.95));

    const edge = lerp(-30, w + 30, ek);   // 지우는 획의 앞머리

    ctx.textBaseline = 'middle';

    /* ── 이 회차가 세운 것들 ─────────────────── */
    const cols = 2;
    const cw = (w - 24 - 12) / cols, ch = 48;
    const y0 = 34;
    items.forEach((label, i) => {
      const cxi = 12 + (i % cols) * (cw + 12);
      const cyi = y0 + Math.floor(i / cols) * (ch + 12);
      const gone = clamp((edge - (cxi + cw * 0.35)) / 46);
      const a = 1 - gone;
      if (a <= 0.02) return;

      ctx.globalAlpha = a;
      ctx.fillStyle = '#000';
      roundRect(ctx, cxi, cyi, cw, ch * (0.35 + 0.65 * a), 5); ctx.fill();
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      roundRect(ctx, cxi, cyi, cw, ch * (0.35 + 0.65 * a), 5); ctx.stroke();
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('ink');
      let s = 16;
      ctx.font = disp(900, s);
      while (s > 9 && ctx.measureText(label).width > cw - 18) { s -= 1; ctx.font = disp(900, s); }
      ctx.fillText(label, cxi + cw / 2, cyi + ch * (0.35 + 0.65 * a) / 2 + 1);
      ctx.globalAlpha = 1;
    });

    /* ── 계속 도는 것 ─────────────────────────────
       지워진 자리에서 잔해가 흘러내린다. 지우는 획이 지나간 뒤에도 화면이 멎지 않는다. */
    const bottom = y0 + Math.ceil(items.length / cols) * (ch + 12);
    ctx.fillStyle = tone('ink');
    for (let i = 0; i < 34; i++) {
      const px = 16 + rnd(i * 2 + 5) * (w - 32);
      if (px > edge) continue;
      const ph = rnd(i * 2 + 6);
      const u = frac(t / 2.4 + ph);
      const py = lerp(bottom - 34, bottom + 22, u);
      ctx.globalAlpha = 0.5 * (1 - u) * clamp((edge - px) / 40);
      ctx.fillRect(px, py, 3, 3);
    }
    ctx.globalAlpha = 1;

    /* ── 지우는 획 ────────────────────────────── */
    if (ek > 0.001 && ek < 0.999) {
      ctx.globalAlpha = 0.9;
      setShadow(ctx, GLOW, 18, 0);
      ctx.lineWidth = 5; ctx.strokeStyle = tone('accent');
      ctx.beginPath(); ctx.moveTo(edge, 18); ctx.lineTo(edge, bottom + 6); ctx.stroke();
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    /* ── 남는 한 줄 ───────────────────────────── */
    const hook = spec.hook || scene?.hook;
    if (hook && hk > 0.02) {
      const lines = String(hook).split('\n').slice(0, 2);
      const s = spring(hk);
      let base = 23;
      ctx.font = disp(900, base * 1.05);
      while (base > 12 && lines.some(l => ctx.measureText(l).width > w - 36)) {
        base -= 1; ctx.font = disp(900, base * 1.05);
      }
      ctx.globalAlpha = clamp(hk * 1.5);
      ctx.textAlign = 'center';
      ctx.fillStyle = tone('ink');
      ctx.font = disp(900, base * s);
      const ty = bottom + 46;
      lines.forEach((l, i) => ctx.fillText(l, w / 2, ty + i * (base * 1.34)));
      // 남았다는 표시 — 밑줄이 왼쪽에서 오른쪽으로 그어진다
      ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
      const uy = ty + (lines.length - 1) * (base * 1.34) + base * 0.9;
      ctx.beginPath();
      ctx.moveTo(w / 2 - 54, uy);
      ctx.lineTo(w / 2 - 54 + 108 * ease(hk), uy);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left'; ctx.textBaseline = 'alphabetic';
  }
};
