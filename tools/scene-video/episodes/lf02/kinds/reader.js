import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* reader — 이 문서의 수신인이 사람이 아니라는 것을, 편지 하나가 사람 칸을 지나쳐
   AI 세션 칸으로 계속 건너가는 것으로 말한다.
   위에는 두 층이 있다 — 규칙(CLAUDE.md)이 사고법(방법론) 위에 얹혀 있고, 사고법이
   위로 밀고 올라와도 규칙 층이 천장으로 막는다.
   연속 모션 = 편지가 세션 칸을 차례로 도는 것. 수신인이 「모든 세션」이라 끝이 없다.
   문자열 출처: 번외 글 21·12행 · Docs/개발_방법론_명세서.md 7행. */

const NS = 4;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    const kTo = ease(cue(0));      // 대상 독자는 모든 AI 세션
    const kMsg = ease(cue(1));     // 같은 깊이로 이어가
    const kRule = ease(cue(2));    // 충돌하면 규칙이 이긴다
    const kCap = ease(cue(3));     // 폭주하지 않도록

    // ── 위 : 두 층 (규칙이 천장) ────────────────────
    const ly = 30, lh = 24;
    const push = kCap > 0.05 ? 4 * (0.5 - 0.5 * Math.cos(t * 2.2)) : 0;
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
    roundRect(ctx, M + 40, ly + lh + 10 - push, w - M * 2 - 80, lh, 3); ctx.stroke();
    ctx.font = mono(700, 10); ctx.fillStyle = tone('accent');
    ctx.fillText('METHODOLOGY  (how to judge)', M + 52, ly + lh + 27 - push);

    ctx.strokeStyle = kRule > 0.1 ? tone('ink') : tone('track');
    ctx.lineWidth = kCap > 0.3 ? 5 : 3;
    roundRect(ctx, M + 20, ly, w - M * 2 - 40, lh, 3); ctx.stroke();
    ctx.font = mono(700, 10); ctx.fillStyle = kRule > 0.1 ? tone('ink') : tone('sub');
    ctx.fillText('CLAUDE.md  (what to obey)  WINS', M + 34, ly + 17);

    // ── 아래 : 사람 칸 / AI 세션 칸 ─────────────────
    const by = 132, hh = 56;
    const hw = 104, hx = M;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    ctx.setLineDash([7, 6]);
    roundRect(ctx, hx, by, hw, hh, 3); ctx.stroke();
    ctx.setLineDash([]);
    ctx.font = disp(800, 14); ctx.fillStyle = tone('sub');
    {
      const s = '사람';
      const sw = ctx.measureText(s).width;
      ctx.fillText(s, hx + (hw - sw) / 2, by + 26);
      ctx.font = mono(700, 9);
      const s2 = 'NOT THE READER';
      const w2 = ctx.measureText(s2).width;
      ctx.fillText(s2, hx + (hw - w2) / 2, by + 44);
    }

    const sx0 = hx + hw + 30;
    const cwd = (w - M - sx0 - (NS - 1) * 10) / NS;

    // 편지가 도는 위상
    const PR = 2.2;
    const idx = Math.floor(t / PR) % NS;
    const ph = (t % PR) / PR;

    for (let i = 0; i < NS; i++) {
      const cx = sx0 + i * (cwd + 10);
      const kIn = clamp((kTo - i * 0.09) / 0.45);
      const got = i === idx && ph > 0.55;
      ctx.save(); ctx.globalAlpha = clamp(0.3 + 0.7 * kIn);
      ctx.strokeStyle = got ? tone('accent') : tone('track');
      ctx.lineWidth = got ? 4 : 3;
      roundRect(ctx, cx, by, cwd, hh, 3); ctx.stroke();
      ctx.restore();
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText(`SESSION #${i + 1}`, cx + 8, by + 18);
      if (kMsg > 0.05) {
        const depth = got ? 1 : 0.45;
        const kk = clamp(kMsg) * depth;
        ctx.fillStyle = got ? tone('accent') : tone('sub');
        ctx.fillRect(cx + 8, by + hh - 16, (cwd - 16) * kk, 6);
      }
    }

    // ── 연속 모션 : 편지 봉투 (층과 칸 사이의 빈 띠를 지난다) ──
    {
      const target = sx0 + idx * (cwd + 10) + cwd / 2;
      const start = hx + hw / 2;
      const ex = lerp(start, target, ease(Math.min(1, ph / 0.55)));
      const ey = 108;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, ex - 15, ey - 10, 30, 20, 2); ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(ex - 15, ey - 10); ctx.lineTo(ex, ey + 2); ctx.lineTo(ex + 15, ey - 10);
      ctx.stroke();
    }

    // 메시지
    if (kMsg > 0.05) {
      ctx.save(); ctx.globalAlpha = clamp(kMsg);
      ctx.font = disp(900, 15); ctx.fillStyle = tone('ink');
      const s = '같은 깊이로 이어가';
      const sw = ctx.measureText(s).width;
      ctx.fillText(s, Math.min(sx0, w - M - sw), h - 12);
      ctx.restore();
    }
  },
};
