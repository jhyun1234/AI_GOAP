import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect } from '../../../engine/lib.js';

/* motion5 — 위는 조각이 이어지는 것(오토타일), 아래는 몸짓 다섯 종.
   도구는 자막과 무관하게 계속 휘둘러진다 — 이 마디가 말하는 것이 '정지 그림의 끝'이라
   화면이 멈추면 그 자체로 틀린 그림이 된다.
   셋째 칸은 명세와 시트가 어긋나 정정된 자리다(괭이 -> 물뿌리개).
   출처: Docs/M23_비주얼_실행명세서.md 0절 3항 · devlog/sessions/2026-08-09.md [01:25]. */

const CELLS = ['도끼질', '곡괭이질', '괭이질', '망치질', '맨손'];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 18, FW = w - M * 2;

    const kEnd = ease(cue(0));
    /* 🔴 조각이 이어지는 것은 **첫 자막부터** 시작한다. 1차본은 cue(1) 에만 걸었는데
       첫 자막(「색 원과 정지 그림의 시대를 … 끝냈어요」) 구간이 통째로 멈췄다
       (검수 실측 4.6초). 조각이 붙는 것이 바로 그 문장이 말하는 일이다. */
    const kTile = (ease(cue(0)) + ease(cue(1))) / 2;
    // 칸도 세 자막에 걸쳐 앉는다 — 한 cue 에만 걸면 그 앞이 정지 구간이 된다.
    const kMove = (ease(cue(1)) + ease(cue(2)) + ease(cue(3))) / 3;
    const kFix = ease(cue(4));

    // ── 위 : 조각이 이어진다 ────────────────────────
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('AUTOTILE', M, 28);
    const ty = 38, seg = 4, sw = 88;
    const total = seg * sw;
    const gap = lerp(26, 0, ease(kTile));
    const startX = M + 130;
    for (let i = 0; i < seg; i++) {
      const x = startX + i * (sw + gap);
      ctx.strokeStyle = kTile > 0.6 ? tone('accent') : tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(x, ty + 22); ctx.lineTo(x + sw, ty + 22);
      ctx.moveTo(x + 14, ty + 22); ctx.lineTo(x + 14, ty + 4);
      ctx.moveTo(x + sw - 14, ty + 22); ctx.lineTo(x + sw - 14, ty + 4);
      ctx.stroke();
    }
    // 이어지기 전의 상태 표시
    ctx.globalAlpha = clamp(1 - kEnd);
    ctx.font = disp(800, 13); ctx.fillStyle = tone('sub');
    ctx.fillText('색 원 · 정지 그림', M, ty + 26);
    ctx.globalAlpha = 1;
    if (kEnd > 0.5) {
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.beginPath(); ctx.moveTo(M, ty + 20); ctx.lineTo(M + 108, ty + 20); ctx.stroke();
    }

    // ── 아래 : 몸짓 다섯 종 ─────────────────────────
    const cy = 106, ch = h - 106 - 24;
    const cw = (FW - 4 * 8) / 5;
    for (let i = 0; i < CELLS.length; i++) {
      const k = clamp((kMove - i * 0.13) / 0.4);
      if (k <= 0.02) continue;
      const x = M + i * (cw + 8);
      const fixed = i === 2 && kFix > 0.5;
      ctx.globalAlpha = k;
      ctx.strokeStyle = fixed ? tone('accent') : tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, x, cy, cw, ch, 4); ctx.stroke();

      // 도구가 계속 휘둘러진다 (칸마다 위상차)
      const P = 1.1, kk = ((t + i * 0.22) % P) / P;
      const swing = Math.sin(kk * Math.PI * 2) * 0.62;
      const px = x + cw / 2, py = cy + ch - 34;
      ctx.save();
      ctx.translate(px, py);
      ctx.rotate(swing);
      ctx.strokeStyle = fixed ? tone('accent') : tone('ink'); ctx.lineWidth = 6;
      ctx.beginPath(); ctx.moveTo(0, 0); ctx.lineTo(0, -38); ctx.stroke();
      ctx.lineWidth = 6;
      ctx.beginPath(); ctx.moveTo(-11, -38); ctx.lineTo(11, -38); ctx.stroke();
      ctx.restore();

      const label = fixed ? '물뿌리개' : CELLS[i];
      ctx.font = disp(800, 13);
      ctx.fillStyle = fixed ? tone('accent') : tone('ink');
      const lw = ctx.measureText(label).width;
      ctx.fillText(label, x + cw / 2 - lw / 2, cy + ch - 10);
      if (i === 2 && kFix > 0.05 && kFix <= 0.5) {
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(x + cw / 2 - lw / 2, cy + ch - 15);
        ctx.lineTo(x + cw / 2 + lw / 2, cy + ch - 15);
        ctx.stroke();
      }
      ctx.globalAlpha = 1;
    }
  },
};
