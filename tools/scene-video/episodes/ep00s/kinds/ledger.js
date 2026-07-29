import {
  disp, mono, ease, easeOut, clamp,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* ledger — 실행명세서 여섯 줄. 이 편의 열린 고리를 닫는 자리.

   S1 마지막 자막에서 "커밋 여섯 개"라고 던지고 화면 아래에 빈 칸 여섯을 심어 뒀다.
   여기서 그 여섯 칸이 채워진다.

   🔴 막대 두 개(2 대 4)로 그리지 않았다. 그건 ep01s 의 어휘이기도 하고,
   무엇보다 원문 89행이 말하는 건 비율이 아니라 **순서**다 —
   "M1부터 M6까지 6개 커밋으로 쪼갠 실행명세서". 커밋은 사슬이지 막대가 아니다.
   세로 사슬로 그리면 두 가지가 공짜로 따라온다:
     ① 여섯 칸 각각이 무엇이었는지 이름을 붙일 수 있다(막대는 못 한다)
     ② 앞 둘과 뒤 넷이 위아래로 갈라져 2:4 가 도장 없이 그냥 보인다
   1차 씬이 도장("두 배")을 작은 막대 위에 얹어 잠깐 오독될 여지를 남겼던 문제도
   여기서는 아예 생기지 않는다.

   아직 안 온 칸은 이름 없는 작은 동그라미로만 둔다. 라벨을 미리 띄워 두면
   화면이 "이 칸은 이거다"라고 먼저 주장해 버린다. */

function fitFont(ctx, text, maxW, weight, size) {
  let s = size;
  ctx.font = disp(weight, s);
  while (s > 8 && ctx.measureText(text).width > maxW) { s -= 1; ctx.font = disp(weight, s); }
  return s;
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const steps = spec.steps || [];
    const RH = 44, GAP = 3, TOP = 6;
    const SPINE = 24, BOXX = 40, BOXW = w - 64 - BOXX;
    const span = steps.length * RH + (steps.length - 1) * GAP;

    const fixK = ease(cue(spec.fixCue ?? 0, 0.2, 0.55));
    const watchK = ease(cue(spec.watchCue ?? 1, 0.2, 0.6));
    const rowY = i => TOP + i * (RH + GAP);

    ctx.textBaseline = 'alphabetic';

    /* ── 사슬 ───────────────────────────────────
       처음부터 끝까지 그어져 있다. 커밋 여섯 개는 이미 정해져 있었고(명세서를 먼저 썼다),
       채워지는 순서만 남았다는 뜻이다. 점 하나가 계속 내려간다. */
    ctx.lineWidth = 3; ctx.lineCap = 'round';
    ctx.strokeStyle = tone('track');
    ctx.beginPath(); ctx.moveTo(SPINE, TOP + 8); ctx.lineTo(SPINE, TOP + span - 8); ctx.stroke();
    ctx.fillStyle = 'rgba(255,255,255,0.55)';
    ctx.beginPath();
    ctx.arc(SPINE, TOP + 8 + ((t * 74) % (span - 16)), 6, 0, Math.PI * 2);
    ctx.fill();

    steps.forEach((s, i) => {
      const watch = s.group === 'watch';
      const grp = watch ? steps.filter(x => x.group === 'watch') : steps.filter(x => x.group === 'fix');
      const idxInGrp = grp.indexOf(s);
      const gk = watch ? watchK : fixK;
      const k = clamp(gk * grp.length - idxInGrp * 0.55);
      const y = rowY(i), cyc = y + RH / 2;

      // 아직 안 온 칸 — 이름 없이 자리만
      if (k <= 0.02) {
        ctx.lineWidth = 3; ctx.strokeStyle = tone('track');
        ctx.beginPath(); ctx.arc(SPINE, cyc, 6, 0, Math.PI * 2); ctx.stroke();
        return;
      }

      const col = watch ? tone('accent') : tone('ink');
      ctx.globalAlpha = clamp(k * 1.5);

      // 사슬의 매듭
      ctx.fillStyle = col;
      ctx.beginPath(); ctx.arc(SPINE, cyc, 7, 0, Math.PI * 2); ctx.fill();

      const slide = (1 - easeOut(k)) * 16;
      ctx.fillStyle = '#000';
      roundRect(ctx, BOXX + slide, y + 2, BOXW, RH - 4, 5); ctx.fill();
      ctx.lineWidth = 3;
      if (watch) setShadow(ctx, GLOW, 12, 0);
      ctx.strokeStyle = watch ? tone('accent') : tone('ink');
      roundRect(ctx, BOXX + slide, y + 2, BOXW, RH - 4, 5); ctx.stroke();
      clearShadow(ctx);

      ctx.textAlign = 'left'; ctx.textBaseline = 'middle';
      ctx.font = mono(700, 11);
      ctx.fillStyle = watch ? tone('accent') : tone('sub');
      ctx.fillText(s.tag, BOXX + slide + 12, cyc);

      fitFont(ctx, s.label, BOXW - 56, 800, 13);
      ctx.fillStyle = tone('ink');
      ctx.fillText(s.label, BOXX + slide + 44, cyc);
      ctx.textBaseline = 'alphabetic';
      ctx.globalAlpha = 1;
    });

    /* ── 묶음 ───────────────────────────────────
       앞 둘은 버그를 지운 커밋, 뒤 넷은 감시 장치. 2:4 는 자리로만 말한다. */
    const drawBracket = (from, to, label, k, col) => {
      if (k <= 0.05) return;
      const y0 = rowY(from) + 4, y1 = rowY(to) + RH - 4;
      const bx = w - 56, ext = easeOut(k);
      const my = (y0 + y1) / 2, half = (y1 - y0) / 2 * ext;
      ctx.globalAlpha = clamp(k * 1.5);
      ctx.lineWidth = 3; ctx.lineCap = 'butt';
      ctx.strokeStyle = col;
      ctx.beginPath();
      ctx.moveTo(bx, my - half); ctx.lineTo(bx, my + half);
      ctx.moveTo(bx, my - half); ctx.lineTo(bx - 8, my - half);
      ctx.moveTo(bx, my + half); ctx.lineTo(bx - 8, my + half);
      ctx.stroke();

      ctx.save();
      ctx.translate(bx + 18, my); ctx.rotate(Math.PI / 2);
      ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
      ctx.font = disp(900, 12);
      ctx.fillStyle = col;
      ctx.fillText(label, 0, 0);
      ctx.restore();
      ctx.textBaseline = 'alphabetic';
      ctx.globalAlpha = 1;
    };

    const fixIdx = steps.map((s, i) => s.group === 'fix' ? i : -1).filter(i => i >= 0);
    const watchIdx = steps.map((s, i) => s.group === 'watch' ? i : -1).filter(i => i >= 0);
    if (fixIdx.length) drawBracket(fixIdx[0], fixIdx.at(-1), spec.fixBracket || '', fixK, tone('ink'));
    if (watchIdx.length) drawBracket(watchIdx[0], watchIdx.at(-1), spec.watchBracket || '', watchK, tone('accent'));

    ctx.textAlign = 'left'; ctx.textBaseline = 'alphabetic';
  }
};
