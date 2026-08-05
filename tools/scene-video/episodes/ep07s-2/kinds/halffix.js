import {
  disp, ease, clamp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* halffix — 경우는 둘인데 패치는 하나다.

   원문: "그날 앞서 한 수정은 똑바로 가는 경우만 고쳤고, 대각선으로 가는 경우에는 그 검사가
   통째로 빠져 있었습니다. 반쪽만 고쳐진 채로 열두 시간쯤 지나온 겁니다."

   이 편의 제목이 그대로 도형이 된다. 경우 둘(STRAIGHT · DIAGONAL)에 검사 슬롯이 하나씩
   있고, 아침 패치는 위쪽에만 꽂혀 있다. gapCue 에서 아래 슬롯의 테두리가 **열리면서**
   비어 있음이 드러나고(그 순간에 drop 소리가 붙는다), hoursCue 에서 그 아래로 열두 시간
   짜리 띠가 자란다.

   🔴 빈 슬롯은 **잘못 꽂힌 게 아니라 비어 있다.** 원문이 "통째로 빠져 있었다"이므로
   X 표시나 깨진 조각을 그리면 사실이 달라진다.

   🔴 시간 띠에 눈금을 안 붙였다. 띠 길이는 시간의 **양**을 뜻하지 몇 시부터 몇 시까지를
   뜻하지 않는다 — 눈금을 그리면 원문에 없는 시각을 주장하게 된다.

   🔴 앞편(ep07s-1)의 howwhat 도 '빈 자리'를 그렸다. 다른 점: 저기의 빈자리는 처음부터
   그럴 의도였던 것이고 여기는 **있어야 하는데 없는 것**이라, 저기는 조용히 두었고
   여기는 테두리가 열리면서 드러난다. */

const FLOW = [1.7, 2.1];   // 두 경우의 표식 주기(초)

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const fit = (txt, weight, start, max, min = 8) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const cases = spec.cases ?? ['STRAIGHT', 'DIAGONAL'];
    const patched = spec.patched ?? 0;
    const ck = ease(cue(spec.caseCue ?? 0, 0.15, 0.7));
    const gk = ease(cue(spec.gapCue ?? 1, 0.15, 0.75));
    const hk = ease(cue(spec.hoursCue ?? 2, 0.15, 0.7));

    const X0 = 34, X1 = w - 34;
    const rowY = i => 92 + i * 68;
    const SLOT = 34, slotX = (X0 + X1) / 2;

    for (let i = 0; i < cases.length; i++) {
      const y = rowY(i);
      const isP = i === patched;
      const a = clamp(ck * 2.6 - i * 0.5);
      if (a <= 0.02) continue;

      /* 경우 이름 */
      ctx.globalAlpha = a;
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5);
      ctx.fillStyle = isP ? tone('accent') : tone('sub');
      ctx.fillText(cases[i], X0, y - 26);

      /* 경로 */
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(X0, y); ctx.lineTo(X1, y); ctx.stroke();
      ctx.globalAlpha = 1;

      /* 검사 슬롯 */
      const openK = isP ? 1 : ease(clamp(gk / 0.6));      // 안 꽂힌 쪽은 gapCue 에서 열린다
      ctx.globalAlpha = a;
      ctx.lineWidth = isP ? 4 : 3;
      ctx.strokeStyle = isP ? tone('accent') : tone('ink');
      if (!isP) { ctx.setLineDash([7, 6]); ctx.lineDashOffset = -(t * 10) % 13; }
      roundRect(ctx, slotX - SLOT / 2, y - SLOT / 2, SLOT, SLOT, 6);
      ctx.stroke();
      ctx.setLineDash([]); ctx.lineDashOffset = 0;

      if (isP) {
        /* 꽂힌 패치 */
        setShadow(ctx, GLOW, 8, 0);
        ctx.fillStyle = tone('accent');
        ctx.beginPath(); ctx.arc(slotX, y, 7.5, 0, Math.PI * 2); ctx.fill();
        clearShadow(ctx);
        if (spec.patchLabel) {
          ctx.textAlign = 'center';
          ctx.font = disp(800, 11.5); ctx.fillStyle = tone('accent');
          ctx.fillText(spec.patchLabel, slotX, y - 26);
        }
      } else if (openK > 0.02) {
        /* 빈 슬롯 — 안이 비어 있다는 것만 보인다 */
        ctx.globalAlpha = a * openK;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(slotX - 9, y); ctx.lineTo(slotX + 9, y);
        ctx.stroke();
      }
      ctx.globalAlpha = 1;

      /* 계속 도는 것 — 표식이 흐른다. 안 꽂힌 쪽은 슬롯 앞에서 사라진다 */
      for (let s = 0; s < 3; s++) {
        const u = frac(t / FLOW[i % FLOW.length] + s / 3);
        const x = X0 + (X1 - X0) * u;
        const blocked = !isP && gk > 0.25 && x > slotX - SLOT / 2 - 6;
        if (blocked) continue;
        ctx.globalAlpha = a * (1 - Math.abs(u - 0.5) * 0.55);
        ctx.fillStyle = tone('accent');
        ctx.beginPath(); ctx.arc(x, y, 4.5, 0, Math.PI * 2); ctx.fill();
        ctx.globalAlpha = 1;
      }
    }

    /* ── 반쪽인 채로 지나온 시간 ───────────────────── */
    if (hk > 0.02) {
      const BY = 246, BH = 22;
      const bw = (X1 - X0) * ease(hk);
      ctx.globalAlpha = hk;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, X0, BY, X1 - X0, BH, 5); ctx.stroke();

      ctx.save();
      roundRect(ctx, X0, BY, bw, BH, 5); ctx.clip();
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      const off = (t * 12) % 14;
      for (let g = -BH; g < X1 - X0 + BH; g += 14) {
        ctx.beginPath();
        ctx.moveTo(X0 + g + off, BY + BH); ctx.lineTo(X0 + g + off + BH, BY);
        ctx.stroke();
      }
      ctx.restore();

      ctx.textAlign = 'center';
      const label = `${spec.hoursLabel ?? 'HALF-FIXED'} · ${spec.hours ?? 12}h`;
      const fs = fit(label, 900, 14, w - 40, 10);
      ctx.font = disp(900, fs); ctx.fillStyle = tone('ink');
      ctx.fillText(label, w / 2, BY + BH + 26);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
