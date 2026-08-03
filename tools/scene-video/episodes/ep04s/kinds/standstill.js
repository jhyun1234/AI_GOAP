import {
  disp, mono, ease, clamp, lerp, frac, span,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* standstill — 검사는 계속 도는데 켜지는 칸이 하나도 없다.

   왼쪽은 우선순위 사다리다. 다만 칸에 이름이 없다 — 원문이 이 시점의 goal 이름을 대지
   않으므로 이름을 지어 넣지 않고 빈 칸(···)으로 둔다. 검사 표식은 위에서 아래로 계속
   내려가는데(t 함수) 어느 칸도 켜지지 않고 그대로 바닥을 벗어난다. 이 '계속 도는데
   아무 일도 안 일어남'이 이 편의 첫 사건이다 — 주민은 고장난 게 아니라 정상 동작 중이다.

   오른쪽 주민 셋은 처음엔 각자 다른 위상으로 걸어 다니다가(liveCue) 창고가 차고
   (fullCue) 검사가 빈손으로 끝나면(freezeCue) 그 자리에 못 박힌 듯 멈춘다. 마지막에
   플레이어에서 주민으로 가는 경로가 중간에 끊겨 있고 '관람' 도장이 찍힌다(watchCue).

   🔴 정지를 그리는 그림이라 정적 구간이 생기기 쉽다. 그래서 멈추는 것은 주민뿐이고,
   사다리 검사 표식은 끝까지 돈다. 멈춘 주민 옆에서 검사만 계속 도는 그림이
   "굳었다"보다 정확하다.

   계속 도는 것 = 사다리를 내려가는 검사 표식(항상) + 굳기 전 주민의 걸음. */

const SCAN = 2.6;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));

    const lk = ease(cue(spec.liveCue ?? 0, 0.15, 0.6));
    const fk = ease(cue(spec.fullCue ?? 1, 0.15, 0.6));
    const zk = ease(cue(spec.freezeCue ?? 2, 0.15, 0.5));
    const wk = ease(cue(spec.watchCue ?? 3, 0.15, 0.55));

    ctx.textBaseline = 'alphabetic';

    const fit = (txt, weight, start, max, min = 8) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    /* ── 창고 ─────────────────────────────────────── */
    {
      const bx = 60, by = 12, bw = w - 60 - 8, bh = 18;
      ctx.font = disp(800, 11); ctx.textAlign = 'left';
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.warehouse || '창고', 8, by + 13);

      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, bx, by, bw, bh, 3); ctx.stroke();

      const full = lerp(0.62, 1, fk);
      ctx.fillStyle = tone('ink');
      ctx.globalAlpha = 0.85;
      roundRect(ctx, bx + 3, by + 3, (bw - 6) * full, bh - 6, 2); ctx.fill();
      ctx.globalAlpha = 1;

      if (fk > 0.55) {
        const k = clamp((fk - 0.55) / 0.45);
        ctx.globalAlpha = k;
        ctx.textAlign = 'right';
        ctx.font = disp(900, 11); ctx.fillStyle = tone('accent');
        ctx.fillText(spec.fullLabel || '가득', bx + bw - 6, by + 13);
        ctx.globalAlpha = 1;
      }
    }

    /* ── 왼쪽: 우선순위 사다리 ─────────────────────── */
    const rows = spec.rows || 5;
    const lx = 8, lw = 138, rowH = 20, pitch = 25, top = 50;
    const bot = top + (rows - 1) * pitch + rowH;

    for (let i = 0; i < rows; i++) {
      const y = top + i * pitch;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, lx, y, lw, rowH, 3); ctx.stroke();
      ctx.textAlign = 'left';
      ctx.font = mono(700, 11); ctx.fillStyle = tone('sub');
      ctx.globalAlpha = 0.65;
      ctx.fillText(spec.rowMark || '···', lx + 9, y + 14);
      ctx.globalAlpha = 1;

      // 어느 칸도 켜지지 않는다 — 검사가 지나간 자리에 ✕ 만 남는다
      const passed = clamp(fk * (rows + 1) - i);
      if (passed > 0.05) {
        const k = clamp(passed * 1.5);
        const mx = lx + lw - 16, my = y + rowH / 2, r = 4.5 * k;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
        ctx.globalAlpha = 0.9 * k;
        ctx.beginPath();
        ctx.moveTo(mx - r, my - r); ctx.lineTo(mx + r, my + r);
        ctx.moveTo(mx + r, my - r); ctx.lineTo(mx - r, my + r);
        ctx.stroke();
        ctx.globalAlpha = 1;
      }
    }

    /* 계속 도는 것 — 검사 표식이 위에서 아래로, 바닥을 벗어나 다시 위에서 */
    {
      const u = frac(t / SCAN);
      const y = lerp(top - 10, bot + 18, u);
      ctx.globalAlpha = 0.55 + 0.45 * Math.sin(Math.PI * u);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(lx - 4, y); ctx.lineTo(lx + lw + 4, y);
      ctx.stroke();
      ctx.globalAlpha = 1;
    }

    if (zk > 0.05) {
      ctx.globalAlpha = clamp(zk * 1.6);
      ctx.textAlign = 'left';
      const s = spec.noneLabel || '충족 조건 없음';
      const fs = fit(s, 800, 11, lw + 6);
      ctx.font = disp(800, fs); ctx.fillStyle = tone('sub');
      ctx.fillText(s, lx, bot + 32);
      ctx.globalAlpha = 1;
    }

    /* ── 오른쪽: 주민 ─────────────────────────────── */
    const nV = spec.villagers || 3;
    const vx0 = 186, vgap = 54, vcy = 96;
    for (let i = 0; i < nV; i++) {
      const born = clamp(lk * (nV + 1) - i);
      if (born < 0.02) continue;
      const walk = Math.sin(frac(t / 1.9 + i / nV) * Math.PI * 2) * 7 * (1 - zk);
      const cx = vx0 + i * vgap + walk;
      const cy = vcy + Math.sin(frac(t / 1.35 + i / 3) * Math.PI * 2) * 2.5 * (1 - zk);
      const s = Math.min(1, spring(born));

      ctx.globalAlpha = clamp(born * 1.6);
      ctx.lineWidth = 3;
      ctx.strokeStyle = zk > 0.45 ? tone('sub') : tone('ink');
      ctx.beginPath(); ctx.arc(cx, cy, 14 * s, 0, Math.PI * 2); ctx.stroke();

      // 굳음 — 발밑에 못이 박힌다
      if (zk > 0.15) {
        const k = clamp((zk - 0.15) / 0.45);
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
        ctx.beginPath();
        ctx.moveTo(cx - 12 * k, cy + 22); ctx.lineTo(cx + 12 * k, cy + 22);
        ctx.stroke();
      }
      ctx.globalAlpha = 1;
    }

    /* ── 주민의 말 ────────────────────────────────── */
    const q = spec.quote || [];
    if (zk > 0.1 && q.length) {
      const k = clamp((zk - 0.1) / 0.5);
      const cx = 8, cy = 152, cw = w - 16, chh = 54;
      ctx.globalAlpha = k;
      setShadow(ctx, GLOW, 12, 0);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
      roundRect(ctx, cx, cy, cw, chh, 4); ctx.stroke();
      clearShadow(ctx);
      ctx.textAlign = 'left';
      let fs = 13;
      ctx.font = disp(800, fs);
      while (fs > 9.5 && q.some(l => ctx.measureText(l).width > cw - 24)) {
        fs -= 0.5; ctx.font = disp(800, fs);
      }
      ctx.fillStyle = tone('ink');
      q.slice(0, 2).forEach((l, i) => ctx.fillText(l, cx + 12, cy + 22 + i * (fs * 1.45)));
      ctx.globalAlpha = 1;
    }

    /* ── 끊긴 명령 경로 · 관람 ────────────────────── */
    if (wk > 0.02) {
      const k = clamp(wk * 1.4);
      const py = 246, pw = 74, ph = 26, px = 8;
      ctx.globalAlpha = k;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('sub');
      roundRect(ctx, px, py, pw, ph, 3); ctx.stroke();
      ctx.textAlign = 'center';
      ctx.font = disp(800, 10.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.playerLabel || '플레이어', px + pw / 2, py + 17);

      // 경로가 중간에서 끊긴다
      const ax = px + pw + 8, bx = w - 92;
      const cutAt = lerp(ax, bx, 0.42);
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.setLineDash([5, 5]);
      ctx.beginPath();
      ctx.moveTo(ax, py + ph / 2); ctx.lineTo(lerp(ax, cutAt, k), py + ph / 2);
      ctx.stroke();
      ctx.setLineDash([]);

      if (k > 0.35) {
        const kk = clamp((k - 0.35) / 0.4);
        const r = 6 * kk;
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
        ctx.beginPath();
        ctx.moveTo(cutAt - r, py + ph / 2 - r); ctx.lineTo(cutAt + r, py + ph / 2 + r);
        ctx.moveTo(cutAt + r, py + ph / 2 - r); ctx.lineTo(cutAt - r, py + ph / 2 + r);
        ctx.stroke();
      }

      // 관람 도장
      if (k > 0.45) {
        const kk = clamp((k - 0.45) / 0.5);
        const s = Math.min(1, spring(kk));
        const sw = 84, sh = 30, sx = w - 8 - sw, sy = py - 2;
        ctx.globalAlpha = kk;
        setShadow(ctx, GLOW, 14, 0);
        ctx.lineWidth = 4; ctx.strokeStyle = tone('accent');
        roundRect(ctx, sx + (sw - sw * s) / 2, sy + (sh - sh * s) / 2, sw * s, sh * s, 3);
        ctx.stroke();
        clearShadow(ctx);
        ctx.textAlign = 'center';
        ctx.font = disp(900, 16 * s); ctx.fillStyle = tone('accent');
        ctx.fillText(spec.watchLabel || '관람', sx + sw / 2, sy + 21);
        ctx.globalAlpha = 1;
      }

      // 왜 끊겼는가
      const note = spec.gapLabel || '';
      if (note && k > 0.55) {
        const kk = clamp((k - 0.55) / 0.4);
        ctx.globalAlpha = kk * 0.95;
        ctx.textAlign = 'left';
        const fs = fit(note, 700, 10.5, w - 16);
        ctx.font = disp(700, fs); ctx.fillStyle = tone('sub');
        ctx.fillText(note, 8, py + ph + 22);
        ctx.globalAlpha = 1;
      }
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
