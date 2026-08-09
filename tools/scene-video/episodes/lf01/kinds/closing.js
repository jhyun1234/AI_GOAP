import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect } from '../../../engine/lib.js';

/* closing — 지금 상태를 정직하게 세워 두고 끝낸다.
   비축 게이지는 기준선을 계속 넘나든다 — '아슬아슬해야 한다'가 목표치라서
   가만히 차 있으면 그 자체로 틀린 그림이다.
   미완 항목의 칸은 끝까지 비워 둔다(에셋 조달은 아직 사람 몫이다).
   🔴 판정 줄은 마지막 자막이 아니라 그 앞 자막에서 완성시킨다 —
   마지막 3초는 아웃트로 카드가 이 캔버스를 덮기 때문이다.
   출처: Docs/CLAUDE.md 게임 건강 기준 · devlog/sessions/2026-08-09.md [세션 마감]. */

const CHIPS = ['재설계', '늑대', '화폐', '성격', '표현층'];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 18, L = M, R = w - M, FW = R - L;

    const kRule = ease(cue(0));
    const kStock = ease(cue(1));
    const kTodo = ease(cue(2));
    const kSay = ease(cue(4, 0.15, 0.45));
    const kSeries = ease(cue(5, 0.15, 0.28));

    // ── 건강 기준 ─────────────────────────────────
    if (kRule > 0.02) {
      ctx.globalAlpha = clamp(kRule);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, L, 18, FW, 40, 4); ctx.stroke();
      ctx.font = disp(900, 20); ctx.fillStyle = tone('ink');
      ctx.fillText('방치가 안락하면 실패다', L + 14, 45);
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      const tag = 'HEALTH RULE';
      ctx.fillText(tag, R - 14 - ctx.measureText(tag).width, 44);
      ctx.globalAlpha = 1;
    }

    // ── 비축 게이지 — 기준선을 계속 넘나든다 ──────────
    const gy = 74, gh = 26;
    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('WINTER STOCK', L, gy - 6);
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    roundRect(ctx, L + 110, gy, FW - 110, gh, 3); ctx.stroke();
    const inner = FW - 110 - 6;
    const lvl = 0.5 + 0.34 * Math.sin(t * 0.9);
    ctx.fillStyle = tone('ink');
    ctx.fillRect(L + 113, gy + 3, inner * lvl * clamp(0.2 + 0.8 * kStock), gh - 6);
    ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
    const lx = L + 113 + inner * 0.5;
    ctx.beginPath(); ctx.moveTo(lx, gy - 6); ctx.lineTo(lx, gy + gh + 6); ctx.stroke();

    // ── 미완 항목 ─────────────────────────────────
    if (kTodo > 0.02) {
      ctx.globalAlpha = clamp(kTodo);
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.strokeRect(L + 2, 122, 18, 18);
      ctx.font = disp(800, 14); ctx.fillStyle = tone('ink');
      ctx.fillText('늑대 · 곰 스프라이트', L + 32, 137);
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText('STILL A COLOURED CIRCLE', L + 190, 136);
      ctx.globalAlpha = 1;
    }

    // ── 시리즈 칩이 흐른다 ─────────────────────────
    if (kSeries > 0.02) {
      ctx.save();
      ctx.beginPath(); ctx.rect(L, 152, FW, 26); ctx.clip();
      ctx.globalAlpha = clamp(kSeries);
      ctx.font = disp(800, 13);
      const unit = 118, span = unit * CHIPS.length;
      const off = (t * 34) % span;
      for (let i = -1; i < CHIPS.length + 2; i++) {
        const x = L + i * unit - off;
        const s = CHIPS[((i % CHIPS.length) + CHIPS.length) % CHIPS.length];
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        roundRect(ctx, x, 154, unit - 14, 22, 3); ctx.stroke();
        ctx.fillStyle = tone('sub');
        ctx.fillText(s, x + 14, 170);
      }
      ctx.globalAlpha = 1;
      ctx.restore();
    }

    // ── 판정 ─────────────────────────────────────
    if (kSay > 0.02) {
      ctx.globalAlpha = clamp(kSay);
      const a = '자율성을 포기하지 않고,';
      const b = '대신 보이게 만든다.';
      let fs = 27;
      ctx.font = disp(900, fs);
      while (fs > 14 && Math.max(ctx.measureText(a).width, ctx.measureText(b).width) > FW - 6) {
        fs -= 1; ctx.font = disp(900, fs);
      }
      ctx.fillStyle = tone('ink');
      ctx.fillText(a, L, 202);
      ctx.fillStyle = tone('accent');
      ctx.fillText(b, L, 230);
      ctx.globalAlpha = 1;
    }
  },
};
