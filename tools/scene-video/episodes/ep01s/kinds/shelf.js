import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* shelf — 후보 목록. 이 회차의 두 번째 되받기.

   이 글의 해결은 문자 그대로 "목록에서 빼는 것"이다. 그러니 목록이라는 그릇이 두 번 나와야
   뜻이 선다.
     mode:"scan" (S5)  탐색 머리가 위쪽 후보들만 오르내리다 되돌아간다.
                       맨 아래 '철 캐기'에는 **한 번도 안 닿는다**. 그 사이 예산 고리가 다 찬다.
     mode:"trim" (S7)  못 찾은 자원 두 칸이 목록에서 지워져 접히고, 짧아진 줄에서
                       탐색 머리가 처음으로 맨 아래에 닿는다.
   같은 화면인데 하나는 못 닿고 하나는 닿는다 — 회차의 기본 도형(뻗었다 되돌아오는 획)이
   목록 위에서 세로로 서 있는 형태다.

   🔴 후보에 가격 숫자를 안 붙인다. 원문은 세 행동을 "값싸 보이는"이라고만 했지 값을 준 적이
      없다. 싸다는 것은 **먼저 열린다**는 순서로만 표현한다.
   🔴 칸 높이를 '남은 영역의 몇 할'로 잡지 않는다. 머리·발을 px 로 떼고 남은 것을 칸 수로 나눈다.

   계속 도는 것 = 목록을 오르내리는 탐색 머리와 그 줄을 훑는 띠. */

const SWEEP = 2.9;   // 탐색 머리가 목록을 한 번 왕복하는 초
const GAP = 8;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, dur, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const trim = spec.mode === 'trim';
    const items = spec.items || [];
    const n = Math.max(1, items.length);

    const HEAD = 46;
    const FOOT = spec.test ? 42 : 20;
    const rowH = (h - HEAD - FOOT - GAP * (n - 1)) / n;

    const nameK = ease(cue(spec.nameCue ?? 1, 0.2, 0.55));
    const missK = ease(cue(spec.missCue ?? 2, 0.2, 0.5));
    const drainK = ease(cue(spec.drainCue ?? 3, 0.2, 0.5));
    const offK = ease(cue(spec.offCue ?? 1, 0.2, 0.6));
    const pickK = ease(cue(spec.pickCue ?? 2, 0.2, 0.5));

    ctx.textBaseline = 'alphabetic';

    /* ── 무엇을 고르는 중인가 ─────────────────── */
    if (spec.goal) {
      ctx.font = disp(800, 11);
      const gw = ctx.measureText(spec.goal).width + 18;
      ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
      roundRect(ctx, 12, 8, gw, 24, 3); ctx.stroke();
      ctx.fillStyle = tone('accent');
      ctx.textAlign = 'left'; ctx.textBaseline = 'middle';
      ctx.fillText(spec.goal, 21, 21);
      ctx.textBaseline = 'alphabetic';
    }

    /* ── 예산 고리 ────────────────────────────────
       S2 의 도넛이 작게 다시 온다. 여기서 마지막 칸까지 찬다 —
       "그 4,096이 어디로 갔나"를 같은 도형이 답한다. */
    if (!trim && spec.budget) {
      const bx = w - 28, by = 24, br = 14;
      const auto = 0.18 + 0.66 * clamp(t / Math.max(1, (dur || 12) * 0.72));
      const k = Math.max(auto, drainK);
      ctx.lineWidth = 7; ctx.lineCap = 'butt';
      ctx.strokeStyle = tone('track');
      ctx.beginPath(); ctx.arc(bx, by, br, 0, Math.PI * 2); ctx.stroke();
      ctx.strokeStyle = tone('accent');
      if (k > 0.002) {
        setShadow(ctx, GLOW, 10, 0);
        ctx.beginPath();
        ctx.arc(bx, by, br, -Math.PI / 2, -Math.PI / 2 + Math.PI * 2 * k);
        ctx.stroke();
        clearShadow(ctx);
      }
      ctx.textAlign = 'right';
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
      ctx.fillText(spec.budgetLabel || '', bx - br - 8, by - 3);
      if (k > 0.995 && spec.spentTag) {
        ctx.fillStyle = tone('accent'); ctx.font = disp(900, 11);
        ctx.fillText(spec.spentTag, bx - br - 8, by + 12);
      }
    }

    /* ── 줄 세우기 ────────────────────────────────
       빠지는 칸은 접히고 아래가 위로 올라온다. */
    const hgt = [], top = [], alive = [];
    let y = HEAD;
    items.forEach((it, i) => {
      const off = trim && it.state === 'off';
      const col = off ? clamp((offK - 0.45) / 0.55) : 0;
      const a = 1 - col;
      hgt.push(rowH * a); top.push(y); alive.push(a);
      y += rowH * a + GAP * (i < n - 1 ? a : 0);
    });
    const center = top.map((ty, i) => ty + hgt[i] / 2);

    /* ── 탐색 머리 ────────────────────────────────
       scan 은 마지막 줄에 못 닿는다. trim 은 목록이 짧아진 뒤에야 닿는다. */
    const liveIdx = items.map((_, i) => i).filter(i => alive[i] > 0.15);
    const lastI = liveIdx[liveIdx.length - 1];
    const prevI = liveIdx.length > 1 ? liveIdx[liveIdx.length - 2] : lastI;
    const reachI = (trim && pickK > 0.5) ? lastI : prevI;
    const u = frac(t / SWEEP);
    const tri = u < 0.5 ? u * 2 : 2 - u * 2;
    const headY = lerp(center[liveIdx[0]], center[reachI], tri);

    /* ── 칸 ───────────────────────────────────── */
    items.forEach((it, i) => {
      if (alive[i] <= 0.02) return;
      const ty = top[i], th = hgt[i];
      const near = clamp(1 - Math.abs(headY - center[i]) / (rowH * 0.65));

      let border = tone('track'), text = tone('sub'), dashed = false, filled = false;
      if (!trim) {
        const lit = it.named != null ? clamp(nameK * 3 - it.named) : 0;
        if (it.target) { dashed = true; }
        else if (it.muted) { border = tone('track'); }
        else if (lit > 0.5) { border = tone('ink'); text = tone('ink'); }
      } else {
        if (it.state === 'on') { border = tone('ink'); text = tone('ink'); }
        else if (it.state === 'off') { border = tone('ink'); text = tone('ink'); }
        else if (it.state === 'pick') {
          if (pickK > 0.5) { filled = true; } else { dashed = true; }
        }
      }

      ctx.globalAlpha = alive[i];
      ctx.save();
      if (dashed) ctx.setLineDash([7, 6]);
      ctx.fillStyle = filled ? tone('accent') : '#000';
      roundRect(ctx, 16, ty, w - 32, th, 5); ctx.fill();
      if (!filled) {
        ctx.lineWidth = 3; ctx.strokeStyle = border;
        roundRect(ctx, 16, ty, w - 32, th, 5); ctx.stroke();
        // 지금 열어보는 줄
        if (near > 0.02 && th > 6) {
          ctx.globalAlpha = alive[i] * near * 0.9;
          ctx.strokeStyle = tone('accent');
          roundRect(ctx, 16, ty, w - 32, th, 5); ctx.stroke();
          ctx.globalAlpha = alive[i];
        }
      }
      ctx.restore();

      if (th > 12) {
        ctx.textAlign = 'left'; ctx.textBaseline = 'middle';
        ctx.fillStyle = filled ? '#000' : text;
        ctx.font = disp(it.muted ? 700 : 900, it.muted ? 12 : 13);
        ctx.fillText(it.label || '', 32, ty + th / 2 + 1);

        // 닿지 않은 칸의 딱지
        if (!trim && it.target && missK > 0.02 && it.tag) {
          ctx.globalAlpha = alive[i] * clamp(missK * 1.5);
          ctx.textAlign = 'right';
          ctx.fillStyle = tone('accent'); ctx.font = disp(800, 11);
          ctx.fillText(it.tag, w - 30, ty + th / 2 + 1);
          ctx.globalAlpha = alive[i];
        }
        // 빠지는 칸의 사유
        if (trim && it.state === 'off' && offK > 0.05 && it.reason) {
          ctx.textAlign = 'right';
          ctx.fillStyle = tone('sub'); ctx.font = disp(700, 11);
          ctx.fillText(it.reason, w - 30, ty + th / 2 + 1);
        }
      }

      // 목록에서 빠진다 — 줄이 그어지고 접힌다
      if (trim && it.state === 'off' && offK > 0.02) {
        const sk = clamp(offK / 0.45);
        ctx.globalAlpha = alive[i];
        ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
        ctx.beginPath();
        ctx.moveTo(22, ty + th / 2);
        ctx.lineTo(22 + (w - 44) * sk, ty + th / 2);
        ctx.stroke();
      }
      ctx.globalAlpha = 1;
    });

    /* ── 탐색 머리 그리기 ─────────────────────── */
    ctx.fillStyle = tone('accent');
    setShadow(ctx, GLOW, 10, 0);
    ctx.beginPath();
    ctx.moveTo(14, headY); ctx.lineTo(3, headY - 7); ctx.lineTo(3, headY + 7);
    ctx.closePath(); ctx.fill();
    clearShadow(ctx);
    ctx.globalAlpha = 0.4;
    ctx.lineWidth = 3; ctx.strokeStyle = tone('accent');
    ctx.beginPath(); ctx.moveTo(18, headY); ctx.lineTo(w - 18, headY); ctx.stroke();
    ctx.globalAlpha = 1;

    /* ── 판정 기준 ────────────────────────────────
       원문에 있는데 자막은 안 읽는 두 가지. 화면에만 있다. */
    if (spec.test && offK > 0.02) {
      const chips = spec.test;
      ctx.font = disp(800, 10);
      const ws = chips.map(c => ctx.measureText(c).width + 18);
      let lab = 0;
      if (spec.testLabel) { ctx.font = disp(700, 10); lab = ctx.measureText(spec.testLabel).width + 8; }
      const total = ws.reduce((a, b) => a + b, 0) + 8 * (ws.length - 1) + lab;
      let x = Math.max(12, (w - total) / 2);
      const cy = h - 22;
      ctx.globalAlpha = clamp(offK * 1.6);
      ctx.textBaseline = 'middle';
      if (spec.testLabel) {
        ctx.textAlign = 'left';
        ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
        ctx.fillText(spec.testLabel, x, cy);
        x += lab;
      }
      chips.forEach((c, i) => {
        ctx.lineWidth = 3; ctx.strokeStyle = tone('ink');
        roundRect(ctx, x, cy - 11, ws[i], 22, 3); ctx.stroke();
        ctx.textAlign = 'center';
        ctx.fillStyle = tone('ink'); ctx.font = disp(800, 10);
        ctx.fillText(c, x + ws[i] / 2, cy + 1);
        x += ws[i] + 8;
      });
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left'; ctx.textBaseline = 'alphabetic';
  }
};
