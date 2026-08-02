import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* rehearsal — 과제 하나를 코드 없이 끝낸다.

   맨 위에 과제 카드. 그 아래 왼쪽은 실제로 한 일 넷이 하나씩 체크되고, 오른쪽은 편집기다 —
   줄 번호는 1 에서 움직이지 않고 커서만 깜빡인다. 넷이 다 체크되는 동안 '코드 0줄'은
   끝까지 0 이다. 화면에서 유일하게 변하지 않는 숫자가 이 편의 증명이다.

   마지막에 주민 머리 위 말풍선이 켜지고 '돌 채집'이 뜬다(원문 43·53행). 이 프로젝트에서
   실제로 화면에 뜬 문구라 그대로 쓴다.

   회차의 기본 도형 안에서 이 그림의 몫은 '아무것도 안 쓰고 하나를 더한다'.
   계속 도는 것 = 편집기 커서, 그리고 주민의 호흡. */

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const steps = spec.steps || [];
    const ns = steps.length || 4;

    const tk = ease(cue(spec.taskCue ?? 0, 0.15, 0.6));
    const sk = ease(cue(spec.stepCue ?? 1, 0.15, 0.75));
    const bk = ease(cue(spec.bubbleCue ?? 2, 0.15, 0.6));

    ctx.textBaseline = 'alphabetic';

    /* ── 과제 ─────────────────────────────────────── */
    {
      const cw = w - 24, cy = 8, ch = 40;
      const s = Math.min(1, spring(clamp(tk * 1.5)));
      ctx.globalAlpha = clamp(tk * 2);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
      roundRect(ctx, 12 + (cw - cw * s) / 2, cy + (ch - ch * s) / 2, cw * s, ch * s, 4);
      ctx.stroke();
      ctx.textAlign = 'center';
      const txt = spec.task || '';
      let fs = 12.5; ctx.font = disp(800, fs);
      while (fs > 9 && ctx.measureText(txt).width > cw - 24) { fs -= 0.5; ctx.font = disp(800, fs); }
      ctx.fillStyle = tone('ink'); ctx.font = disp(800, fs * s);
      ctx.fillText(txt, w / 2, cy + 26);
      ctx.globalAlpha = 1;
    }

    /* ── 한 일 넷 ─────────────────────────────────── */
    const listX = 12, listW = 190, listTop = 66, pitch = 23;
    for (let i = 0; i < ns; i++) {
      const y = listTop + i * pitch;
      const k = clamp(sk * (ns + 1.2) - i * 1.0);
      if (k < 0.02) continue;
      ctx.globalAlpha = clamp(k * 2.2);

      // 체크 상자
      ctx.lineWidth = 3; ctx.strokeStyle = k > 0.55 ? tone('accent') : tone('sub');
      roundRect(ctx, listX, y - 10, 13, 13, 2); ctx.stroke();
      if (k > 0.55) {
        const ck = clamp((k - 0.55) / 0.35);
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(listX + 2, y - 4);
        ctx.lineTo(listX + 2 + 4 * Math.min(1, ck * 2), y);
        if (ck > 0.5) ctx.lineTo(listX + 6 + 6 * clamp((ck - 0.5) * 2), y - 7 * clamp((ck - 0.5) * 2));
        ctx.stroke();
      }

      ctx.textAlign = 'left';
      const txt = steps[i] || '';
      let fs = 10.5; ctx.font = disp(700, fs);
      while (fs > 7.5 && ctx.measureText(txt).width > listW - 24) { fs -= 0.5; ctx.font = disp(700, fs); }
      ctx.fillStyle = k > 0.55 ? tone('ink') : tone('sub');
      ctx.fillText(txt, listX + 22, y);
      ctx.globalAlpha = 1;
    }

    /* ── 편집기 : 줄 번호가 안 움직인다 ───────────── */
    {
      const ex = 216, ey = 62, ew = w - ex - 14, eh = 96;
      ctx.globalAlpha = clamp(tk * 2);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('track');
      roundRect(ctx, ex, ey, ew, eh, 3); ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(ex + 22, ey + 4); ctx.lineTo(ex + 22, ey + eh - 4); ctx.stroke();

      ctx.textAlign = 'right';
      ctx.fillStyle = tone('sub'); ctx.font = mono(700, 11);
      ctx.fillText('1', ex + 17, ey + 24);

      /* 계속 도는 것 — 커서. 줄은 끝내 안 늘어난다. */
      if (frac(t / 1.05) < 0.55) {
        ctx.fillStyle = tone('ink');
        ctx.fillRect(ex + 30, ey + 13, 7, 14);
      }

      ctx.textAlign = 'center';
      ctx.fillStyle = tone('sub'); ctx.font = disp(800, 10);
      ctx.fillText(spec.codeLabel || '작성한 코드', ex + ew / 2, ey + 58);
      setShadow(ctx, GLOW, 12, 0);
      ctx.fillStyle = tone('accent'); ctx.font = disp(900, 26);
      ctx.fillText(spec.codeValue || '0줄', ex + ew / 2, ey + 84);
      clearShadow(ctx);
      ctx.globalAlpha = 1;
    }

    /* ── 주민과 말풍선 ─────────────────────────────── */
    {
      const breathe = Math.sin(frac(t / 2.4) * Math.PI * 2) * 3;
      const vx = 60, vy = 262 + breathe;

      // 주민
      ctx.lineWidth = 3;
      ctx.strokeStyle = bk > 0.4 ? tone('accent') : tone('ink');
      ctx.beginPath(); ctx.arc(vx, vy, 15, 0, Math.PI * 2); ctx.stroke();

      // 말풍선
      const bw = 214, bx = 92, by = 186, bh = 52;
      const on = clamp(bk * 1.8 - 0.2);
      ctx.lineWidth = on > 0.3 ? 4 : 3;
      ctx.strokeStyle = on > 0.3 ? tone('accent') : tone('track');
      if (on > 0.3) setShadow(ctx, GLOW, 14, 0);
      roundRect(ctx, bx, by, bw, bh, 6); ctx.stroke();
      // 꼬리
      ctx.beginPath();
      ctx.moveTo(bx + 26, by + bh);
      ctx.lineTo(vx + 6, vy - 16);
      ctx.lineTo(bx + 50, by + bh);
      ctx.stroke();
      clearShadow(ctx);

      if (on > 0.05) {
        ctx.globalAlpha = clamp(on * 1.4);
        ctx.textAlign = 'center';
        const txt = spec.bubble || '';
        let fs = 21; ctx.font = disp(900, fs);
        while (fs > 13 && ctx.measureText(txt).width > bw - 28) { fs -= 0.5; ctx.font = disp(900, fs); }
        const s = Math.min(1, spring(on));
        ctx.font = disp(900, fs * s);
        ctx.fillStyle = tone('accent');
        ctx.fillText(txt, bx + bw / 2, by + 34);
        ctx.globalAlpha = 1;
      }
    }

    ctx.textAlign = 'left';
  }
};
