import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* askfirst — 일이 한 줄로 내려가다, 판단이 필요한 칸에서만 옆으로 건너간다.

   왼쪽 열은 Claude Code 가 한 일(진단 · 선택지 3개 · 실행 명세서 · 단계별 구현),
   오른쪽 열은 사람이 한 일(결정) 하나뿐이다. 진행선은 왼쪽을 따라 내려오다 세 번째 칸에서
   오른쪽으로 건너갔다가 다시 왼쪽으로 돌아온다 — 그 한 칸이 이 프로젝트의 규칙이고,
   규칙 문장은 아래에 원문 67행 그대로 붙는다.

   🔴 Claude 마크를 쓰지 않는다. 마크를 쓰면 브랜드 색 때문에 팔레트 예외를 선언해야 하고,
   이 장면에서 중요한 것은 로고가 아니라 '어느 칸이 누구 것인가'라는 배치다. 이름 글자만 둔다.

   회차의 기본 도형 안에서 이 그림의 몫은 '한 칸만 사람에게 넘어간다'.
   계속 도는 것 = 진행선 위를 계속 도는 표식. 그 표식은 매 바퀴 오른쪽으로 건너갔다 온다. */

const LOOP = 5.2;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const rows = spec.rows || [];
    const n = rows.length || 5;

    const sk = ease(cue(spec.splitCue ?? 0, 0.15, 0.72));
    const rk = ease(cue(spec.ruleCue ?? 1, 0.15, 0.6));

    ctx.textBaseline = 'alphabetic';

    const lX = 14, lW = 162, rX = 190, rW = w - rX - 14;
    const top = 52, pitch = 34, chipH = 26;
    const cx = i => (rows[i] && rows[i].side === 'me') ? rX + rW / 2 : lX + lW / 2;
    const cy = i => top + i * pitch + chipH / 2;

    // 열 이름
    ctx.globalAlpha = clamp(sk * 3);
    ctx.textAlign = 'center';
    ctx.fillStyle = tone('sub'); ctx.font = disp(800, 11);
    ctx.fillText(spec.aiLabel || 'Claude Code', lX + lW / 2, top - 14);
    ctx.fillText(spec.meLabel || '나', rX + rW / 2, top - 14);
    ctx.globalAlpha = 1;

    /* ── 진행선 ────────────────────────────────────── */
    const pts = [];
    for (let i = 0; i < n; i++) pts.push([cx(i), cy(i)]);
    const crossed = i => rows[i] && rows[i].side === 'me';

    for (let i = 0; i < n - 1; i++) {
      const k = clamp(sk * (n + 1) - i - 0.6);
      if (k < 0.02) continue;
      const hot = rk > 0.15 && (crossed(i) || crossed(i + 1));
      ctx.globalAlpha = clamp(k * 2);
      ctx.strokeStyle = hot ? tone('accent') : tone('track');
      ctx.lineWidth = hot ? 4 : 3;
      ctx.beginPath();
      ctx.moveTo(pts[i][0], pts[i][1]);
      ctx.lineTo(lerp(pts[i][0], pts[i + 1][0], k), lerp(pts[i][1], pts[i + 1][1], k));
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    /* 계속 도는 것 — 진행선을 도는 표식 */
    {
      const segs = [];
      let tot = 0;
      for (let i = 0; i < n - 1; i++) {
        const d = Math.hypot(pts[i + 1][0] - pts[i][0], pts[i + 1][1] - pts[i][1]);
        segs.push(d); tot += d;
      }
      let u = frac(t / LOOP) * tot, si = 0;
      while (si < segs.length - 1 && u > segs[si]) { u -= segs[si]; si++; }
      const f = segs[si] ? u / segs[si] : 0;
      const mx = lerp(pts[si][0], pts[si + 1][0], f);
      const my = lerp(pts[si][1], pts[si + 1][1], f);
      ctx.globalAlpha = clamp(sk * 3) * 0.9;
      ctx.fillStyle = (crossed(si) || crossed(si + 1)) && rk > 0.15 ? tone('accent') : tone('sub');
      ctx.beginPath(); ctx.arc(mx, my, 4.5, 0, Math.PI * 2); ctx.fill();
      ctx.globalAlpha = 1;
    }

    /* ── 칸 ───────────────────────────────────────── */
    for (let i = 0; i < n; i++) {
      const r = rows[i] || {};
      const me = r.side === 'me';
      const k = clamp(sk * (n + 1) - i);
      if (k < 0.02) continue;
      const bw = me ? rW : lW, bx = me ? rX : lX;
      const s = Math.min(1, spring(k));
      const sw = bw * s, sh = chipH * s;
      const sx = bx + (bw - sw) / 2, sy = top + i * pitch + (chipH - sh) / 2;
      const hot = me && rk > 0.15;

      ctx.globalAlpha = clamp(k * 2);
      if (hot) setShadow(ctx, GLOW, 12, 0);
      ctx.lineWidth = hot ? 4 : 3;
      ctx.strokeStyle = hot ? tone('accent') : tone('ink');
      roundRect(ctx, sx, sy, sw, sh, 3); ctx.stroke();
      clearShadow(ctx);

      ctx.textAlign = 'center';
      const txt = r.text || '';
      let fs = 11; ctx.font = disp(800, fs);
      while (fs > 8 && ctx.measureText(txt).width > sw - 16) { fs -= 0.5; ctx.font = disp(800, fs); }
      ctx.fillStyle = hot ? tone('accent') : tone('ink');
      ctx.font = disp(800, fs * s);
      ctx.fillText(txt, sx + sw / 2, sy + sh / 2 + 4);
      ctx.globalAlpha = 1;
    }

    /* ── 규칙 ─────────────────────────────────────── */
    if (rk > 0.02) {
      const lines = spec.rule || [];
      const cyy = 228, chh = 62;
      ctx.globalAlpha = clamp(rk * 2);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('sub');
      roundRect(ctx, 12, cyy, w - 24, chh, 4); ctx.stroke();
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.moveTo(12, cyy + 8);
      ctx.lineTo(12, cyy + 8 + (chh - 16) * clamp(rk * 1.6));
      ctx.stroke();
      ctx.textAlign = 'left';
      lines.slice(0, 2).forEach((l, i) => {
        let fs = 11; ctx.font = disp(700, fs);
        while (fs > 8 && ctx.measureText(l).width > w - 52) { fs -= 0.5; ctx.font = disp(700, fs); }
        ctx.fillStyle = tone('ink');
        ctx.fillText(l, 26, cyy + 26 + i * 22);
      });
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
