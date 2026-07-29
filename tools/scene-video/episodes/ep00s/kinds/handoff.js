import {
  disp, ease, easeOut, clamp, lerp, frac, depthGrad,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* handoff — 마지막 샷. 두 가지를 한 번에 해야 하는 자리다.
   0편은 이 사건 하나로 완결돼야 하고(결함 C 는 지워졌다),
   동시에 1편으로 넘겨야 한다(계획 칸의 3 은 아직 안 풀렸다).

   그래서 화면에 남는 물건이 둘이다. 위의 이름표는 줄이 그어져 꺼지고,
   아래의 칸 하나만 살아서 오른쪽으로 넘어간다.

   이름표의 꺾쇠는 S3(dossier)에서 이 이름이 붙던 그 모양 그대로다 —
   붙는 걸 본 물건에 줄이 그어져야 지워졌다는 게 읽힌다. */

function fitFont(ctx, text, maxW, weight, size) {
  let s = size;
  ctx.font = disp(weight, s);
  while (s > 8 && ctx.measureText(text).width > maxW) { s -= 1; ctx.font = disp(weight, s); }
  return s;
}

function brackets(ctx, x, y, bw, bh, off, alpha, col) {
  const L = 22;
  ctx.globalAlpha = alpha;
  ctx.lineWidth = 4; ctx.lineCap = 'butt';
  ctx.strokeStyle = col;
  const corners = [
    [x - off, y - off, 1, 1], [x + bw + off, y - off, -1, 1],
    [x - off, y + bh + off, 1, -1], [x + bw + off, y + bh + off, -1, -1]
  ];
  for (const [px, py, sx, sy] of corners) {
    ctx.beginPath();
    ctx.moveTo(px + sx * L, py); ctx.lineTo(px, py); ctx.lineTo(px, py + sy * L);
    ctx.stroke();
  }
  ctx.globalAlpha = 1;
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const cx = w / 2;

    const PX = 66, PY = 44, PW = w - 132, PH = 74;
    const CW = 116, CH = 96, CY = 156, CX0 = cx - CW / 2;

    const strikeK = ease(cue(spec.strikeCue ?? 0, 0.2, 0.6));
    const passK = ease(cue(spec.passCue ?? 1, 0.2, 0.55));

    ctx.textBaseline = 'alphabetic';

    /* ── 지워지는 이름 ─────────────────────────── */
    const dead = clamp(strikeK * 1.4 - 0.4);
    const alive = 1 - dead * 0.68;
    // 꺾쇠는 자리에서 조금씩 벌어졌다 좁혀진다 — 이 샷에서 계속 도는 움직임
    const off = 2.5 + Math.sin(t * 1.9) * 2.2;
    brackets(ctx, PX, PY, PW, PH, off, alive, dead > 0.5 ? tone('sub') : tone('accent'));

    ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
    fitFont(ctx, spec.name || '', PW - 40, 900, 40);
    if (dead < 0.5) {
      setShadow(ctx, GLOW, 22, 4);
      ctx.fillStyle = depthGrad(ctx, cx, PY + 16, cx, PY + PH - 10, 'accent');
    } else ctx.fillStyle = tone('sub');
    ctx.globalAlpha = alive;
    ctx.fillText(spec.name || '', cx, PY + PH / 2 + 1);
    clearShadow(ctx);
    ctx.globalAlpha = 1;

    if (strikeK > 0.01) {
      ctx.lineWidth = 5; ctx.lineCap = 'round';
      ctx.strokeStyle = tone('ink');
      ctx.beginPath();
      ctx.moveTo(PX - 6, PY + PH / 2);
      ctx.lineTo(PX - 6 + (PW + 12) * easeOut(strikeK), PY + PH / 2);
      ctx.stroke();
    }

    /* ── 남는 것 하나 ───────────────────────────
       S9 에서 값이 떨어졌던 그 칸이다. 여기서만 살아서 다음 편으로 넘어간다. */
    const slide = easeOut(passK) * 92;
    const bx = CX0 + slide;
    const breathe = 16 + Math.sin(t * 2.2) * 7;

    ctx.fillStyle = '#000';
    roundRect(ctx, bx, CY, CW, CH, 6); ctx.fill();
    setShadow(ctx, GLOW, breathe, 4);
    ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
    roundRect(ctx, bx, CY, CW, CH, 6); ctx.stroke();
    clearShadow(ctx);

    ctx.textAlign = 'center'; ctx.textBaseline = 'alphabetic';
    ctx.font = disp(800, 12);
    ctx.fillStyle = tone('accent');
    ctx.fillText(spec.card?.label || '', bx + CW / 2, CY + 26);
    ctx.font = disp(900, 50);
    ctx.fillStyle = depthGrad(ctx, bx, CY + 34, bx, CY + CH - 10, 'accent');
    ctx.fillText(String(spec.card?.value ?? ''), bx + CW / 2, CY + 78);

    /* ── 다음 편으로 ───────────────────────────── */
    if (passK > 0.05) {
      const ax0 = CX0 - 12, ax1 = CX0 - 12 - 70;
      // 왼쪽에서 밀어내는 손짓 : 칸이 오른쪽으로 가고, 지나온 자리에 자국이 남는다
      ctx.globalAlpha = clamp(passK * 1.4 - 0.2);
      ctx.lineWidth = 3; ctx.lineCap = 'round';
      ctx.strokeStyle = 'rgba(255,255,255,0.55)';
      ctx.beginPath(); ctx.moveTo(ax1, CY + CH / 2); ctx.lineTo(ax0, CY + CH / 2); ctx.stroke();
      ctx.fillStyle = tone('ink');
      ctx.beginPath();
      ctx.arc(lerp(ax1, ax0, frac(t / 1.2)), CY + CH / 2, 5, 0, Math.PI * 2);
      ctx.fill();

      ctx.textAlign = 'center';
      ctx.font = disp(900, 14);
      ctx.fillStyle = tone('ink');
      ctx.fillText(spec.next || '다음 편', bx + CW / 2, CY - 16);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left'; ctx.textBaseline = 'alphabetic';
  }
};
