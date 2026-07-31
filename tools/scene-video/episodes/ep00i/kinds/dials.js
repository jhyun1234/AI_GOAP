import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* dials — 성격은 이름이 아니라 눈금 여섯이다.

   TraitId.cs 15~22행: 성격은 성향 6축(근면·대비·사교·모험·자존·겁)의 벡터다. 이름 여섯 개를
   나열하고 마는 그림은 "성격이 있다"까지밖에 못 간다. 그래서 위에는 이름을 칩으로 눕혀 두고,
   아래에는 **고른 하나의 실제 값**을 편다 — Personality_Lazy.asset 의 Traits 그대로다.

   🔴 이름 여섯과 눈금 여섯이 나란히 서면 "1번 이름 = 1번 눈금"으로 읽힌다. 그래서 이름은
      가로 3×2 칩으로, 눈금은 세로 6줄로 축을 아예 다르게 뒀다.

   회차의 기본 도형 안에서 이 그림의 몫은 '가운데 0선에서 스스로 좌우로 벌어진다'.
   계속 도는 것 = 고른 이름 칩 위를 오르내리는 표식(위치 이동). */

const BOB = 2.4;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const lk = ease(cue(spec.listCue ?? 0, 0.15, 0.55));
    const dk = ease(cue(spec.dialCue ?? 1, 0.12, 0.65));

    const names = spec.names || [];
    const dials = spec.dials || [];
    const picked = spec.picked ?? 0;

    ctx.textBaseline = 'alphabetic';

    /* ── 위 : 이름 칩 3×2 ──────────────────────── */
    const chipW = 104, chipH = 27, gapX = 8, gapY = 8;
    const x0 = (w - (chipW * 3 + gapX * 2)) / 2;
    const y0 = 22;
    let pickCx = w / 2, pickCy = y0;

    ctx.textAlign = 'center';
    names.forEach((n, i) => {
      const col = i % 3, row = (i / 3) | 0;
      const bx = x0 + col * (chipW + gapX);
      const by = y0 + row * (chipH + gapY);
      const k = clamp((lk - i * 0.06) / 0.5);
      if (k <= 0.01) return;
      const isPick = i === picked && dk > 0.02;
      if (isPick) { pickCx = bx + chipW / 2; pickCy = by; }

      const s = spring(k);
      ctx.save(); ctx.translate(bx + chipW / 2, by + chipH / 2); ctx.scale(s, s);
      if (isPick) setShadow(ctx, GLOW, 12, 0);
      ctx.lineWidth = 3;
      ctx.strokeStyle = isPick ? tone('accent') : tone('track');
      ctx.fillStyle = '#000';
      roundRect(ctx, -chipW / 2, -chipH / 2, chipW, chipH, 6);
      ctx.fill(); ctx.stroke();
      clearShadow(ctx);
      ctx.restore();

      ctx.globalAlpha = k;
      ctx.fillStyle = isPick ? tone('accent') : tone('sub');
      ctx.font = disp(isPick ? 900 : 700, 14);
      ctx.fillText(n, bx + chipW / 2, by + chipH / 2 + 5);
      ctx.globalAlpha = 1;
    });

    // 고른 칩 위를 오르내리는 표식 — 계속 돈다
    if (dk > 0.02) {
      const u = frac(t / BOB);
      const off = Math.sin(u * Math.PI * 2) * 4;
      ctx.fillStyle = tone('accent');
      const my = pickCy - 8 + off;
      ctx.beginPath();
      ctx.moveTo(pickCx, my + 7); ctx.lineTo(pickCx - 6, my - 2); ctx.lineTo(pickCx + 6, my - 2);
      ctx.closePath(); ctx.fill();
    }

    /* ── 아래 : 고른 성격의 눈금 여섯 ──────────── */
    const rowTop = 108, rowH = 25;
    const axCx = 210, axHalf = 118;    // 0선 위치와 ±100 반폭

    // 0선
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(axCx, rowTop - 8); ctx.lineTo(axCx, rowTop + rowH * dials.length - 4);
    ctx.stroke();

    ctx.textAlign = 'left';
    dials.forEach((d, i) => {
      const cy = rowTop + i * rowH + rowH / 2;
      const k = clamp((dk - i * 0.07) / 0.5);

      // 축 이름
      ctx.globalAlpha = lerp(0.35, 1, k);
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 13);
      ctx.fillText(d.label || '', 14, cy + 5);
      ctx.globalAlpha = 1;

      // 눈금 바탕
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(axCx - axHalf, cy); ctx.lineTo(axCx + axHalf, cy);
      ctx.stroke();

      if (k <= 0.01) return;
      const v = (d.value ?? 0) / 100;
      const end = axCx + axHalf * v * k;
      const deep = Math.abs(v) >= 0.5;
      ctx.strokeStyle = deep ? tone('accent') : tone('ink');
      ctx.lineWidth = 5;
      if (deep) setShadow(ctx, GLOW, 10, 0);
      ctx.beginPath(); ctx.moveTo(axCx, cy); ctx.lineTo(end, cy); ctx.stroke();
      clearShadow(ctx);

      // 값이 0이면 0선 위 점으로만
      ctx.fillStyle = deep ? tone('accent') : tone('ink');
      ctx.beginPath(); ctx.arc(end, cy, Math.abs(v) < 0.02 ? 3.5 : 5, 0, Math.PI * 2); ctx.fill();
    });

    // 좌·우 끝 뜻 표시
    ctx.globalAlpha = lerp(0, 0.75, dk);
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
    ctx.textAlign = 'left';
    ctx.fillText('←', axCx - axHalf, rowTop + rowH * dials.length + 14);
    ctx.textAlign = 'right';
    ctx.fillText('→', axCx + axHalf, rowTop + rowH * dials.length + 14);
    ctx.textAlign = 'center';
    ctx.fillText('0', axCx, rowTop + rowH * dials.length + 14);
    ctx.globalAlpha = 1;

    ctx.textAlign = 'left';
  }
};
