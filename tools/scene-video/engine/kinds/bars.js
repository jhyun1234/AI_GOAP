import {
  disp,
  ease, easeOut, clamp, lerp, spring, depthGrad, setShadow, clearShadow, GLOW, GLOW_INK,
  fitCanvas, mkCanvas, tone
} from '../lib.js';

/* bars — 세로 비교 막대. youtube-editor SKILL.md 패턴 2-B(후킹/임팩트용).

   ep01 정본의 compare 는 DOM 막대였고 화면의 절반쯤을 썼다. 여기서는 천장까지 쓴다.
   150 은 위에서 잘리고 9 는 바닥에 붙는다 — 이 높이 차이 자체가 "50배"의 증거라
   배수 표기는 확인 도장일 뿐 설명이 아니게 된다.

   guide:true 면 큰 막대 위에 작은 값의 높이로 기준선을 긋는다.
   "AI가 여기까지라고 봤다"가 큰 막대 안에서 눈에 보여야 두 값이 한 그림이 된다. */

const MIN_H = 10;   // 9 짜리 막대도 검정 배경에서 보이긴 해야 한다

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, cue, nLines }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const keys = ['b', 'a'];                       // 대본이 큰 값(실제 비용)을 먼저 말한다
    const max = Math.max(spec.a.value, spec.b.value) || 1;

    const labelH = 34, numH = 52;
    const baseY = h - labelH;                      // 막대 바닥
    const topY = numH;                             // 숫자가 앉을 자리
    const fullH = baseY - topY;
    const colW = (w - 26) / 2;

    keys.forEach((key, ci) => {
      const s = spec[key];
      const k = easeOut(cue(s.cue ?? ci, 0.15, 0.6));
      const x = ci * (colW + 26);
      const bw = colW * 0.62, bx = x + (colW - bw) / 2;
      const bh = Math.max(k > 0.01 ? MIN_H : 0, s.value / max * fullH * k);
      const y = baseY - bh;
      const c = tone(s.tone);

      // 빈 트랙 — 막대가 어디까지 갈 수 있는지가 보여야 높이가 뜻을 가진다
      ctx.fillStyle = 'rgba(255,255,255,.10)';
      ctx.fillRect(bx, topY, bw, fullH);

      // 깊이 — 같은 hue 안에서 위가 밝고 아래가 어둡다. 납작한 단색 막대보다 두께가 산다
      const accent = s.tone === 'accent';
      setShadow(ctx, accent ? GLOW : GLOW_INK, 20, 0);
      ctx.fillStyle = depthGrad(ctx, bx, y, bx, baseY, accent ? 'accent' : 'ink');
      ctx.fillRect(bx, y, bw, bh);
      clearShadow(ctx);

      // 숫자 — 막대 위에 앉되 자리는 고정. 막대를 따라 올라가면 레이아웃이 흔들린다
      ctx.textAlign = 'center';
      ctx.fillStyle = c;
      ctx.font = disp(900, 46);
      ctx.globalAlpha = k > 0.02 ? 1 : 0.15;
      ctx.fillText(Math.round(s.value * k).toLocaleString(), x + colW / 2, topY - 12);
      ctx.globalAlpha = 1;

      ctx.fillStyle = tone('sub');
      ctx.font = disp(700, 12);
      ctx.fillText(s.label, x + colW / 2, h - 10);
    });

    /* 기준선 — 큰 막대 안에 작은 값의 높이를 그어 준다 */
    if (spec.guide) {
      const small = spec.a.value <= spec.b.value ? spec.a : spec.b;
      const bigCi = spec.a.value <= spec.b.value ? keys.indexOf('b') : keys.indexOf('a');
      const gk = ease(cue(small.cue ?? nLines - 2, 0.15, 0.6));
      if (gk > 0.02) {
        const gy = baseY - small.value / max * fullH;
        const x = bigCi * (colW + 26);
        ctx.globalAlpha = gk;
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.moveTo(x, gy); ctx.lineTo(x + colW, gy); ctx.stroke();
        /* 🔴 이 글자는 흰 막대 위를 지난다. 그린 글자를 그냥 얹으면 안 읽힌다
           (실제 프레임에서 확인). 검정 테두리를 두르면 검정 배경 위에서도,
           흰 막대 위에서도 똑같이 읽힌다. */
        ctx.textAlign = 'left';
        ctx.font = disp(800, 11);
        const label = 'AI는 여기까지라고 봤다';
        ctx.lineWidth = 4; ctx.lineJoin = 'round'; ctx.strokeStyle = '#000000';
        ctx.strokeText(label, x + 4, gy - 8);
        ctx.fillStyle = tone('accent');
        ctx.fillText(label, x + 4, gy - 8);
        ctx.globalAlpha = 1;
      }
    }

    /* 배수 도장 — 두 막대가 다 선 뒤에만. 비교 대상이 없으면 배수는 뜻이 없다.
       자리는 작은 값 쪽 빈 트랙 위. 가운데에 놓으면 천장까지 찬 큰 막대를 덮는다. */
    if (spec.note) {
      const nk = ease(cue(spec.noteCue ?? nLines - 1, 0.1, 0.5));
      if (nk > 0.02) {
        const smallCi = spec.a.value <= spec.b.value ? keys.indexOf('a') : keys.indexOf('b');
        const nx = smallCi * (colW + 26) + colW / 2, ny = topY + fullH * 0.34;
        ctx.globalAlpha = nk;
        ctx.textAlign = 'center';
        // 도장은 스프링으로 박힌다 — 커졌다가 제자리에 앉는 게 '찍혔다'로 읽힌다
        ctx.font = disp(900, Math.round(40 * spring(nk)));
        setShadow(ctx, GLOW, 26, 4);
        ctx.fillStyle = depthGrad(ctx, nx, ny - 30, nx, ny + 8, 'accent');
        ctx.fillText(spec.note, nx, ny);
        clearShadow(ctx);
        ctx.globalAlpha = 1;
      }
    }
    ctx.textAlign = 'left';
  }
};
