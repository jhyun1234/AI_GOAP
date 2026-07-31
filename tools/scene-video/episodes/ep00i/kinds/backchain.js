import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* backchain — 목표에서 거꾸로 짚어 내려온 뒤, 실행은 거슬러 오른다.

   GOAP 를 처음 보는 사람에게 설명해야 하는 것은 A* 도 노드 수도 아니라 **방향이 둘이라는 것**
   하나뿐이다. 짜는 방향과 하는 방향이 반대다. 그래서 이 그림에는 세로축이 하나뿐이고
   그 위에서 두 번, 서로 반대로 훑는다 — 오른쪽 가장자리는 아래로(짠다), 왼쪽 가장자리는
   위로(한다).

   회차의 기본 도형 안에서 이 그림의 몫은 '위에서 내려가 본 뒤, 아래에서 되짚어 오른다'.
   계속 도는 것 = 연결선을 따라 위로 흐르는 점.

   🔴 카드 세 장의 이름(먹기·요리하기·열매 따기)은 화면에만 둔다 — 자막은 '순서'라고만 부른다. */

const FLOW = 2.6;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const gk = ease(cue(spec.goalCue ?? 0, 0.15, 0.5));
    const bk = ease(cue(spec.backCue ?? 1, 0.12, 0.65));
    const rk = ease(cue(spec.runCue ?? 2, 0.12, 0.6));

    const chain = spec.chain || [];
    const cx = w / 2;

    ctx.textBaseline = 'alphabetic';
    ctx.textAlign = 'center';

    /* ── 목표 카드 (맨 위) ─────────────────────── */
    const gw = 164, gh = 42, gy = 16;
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
    ctx.fillText(spec.goalLabel || '목표', cx, gy - 5);

    {
      const s = spring(clamp(gk * 1.5));
      ctx.save(); ctx.translate(cx, gy + gh / 2); ctx.scale(s, s);
      setShadow(ctx, GLOW, 14, 0);
      ctx.lineWidth = 4; ctx.strokeStyle = tone('accent'); ctx.fillStyle = '#000';
      roundRect(ctx, -gw / 2, -gh / 2, gw, gh, 8);
      ctx.fill(); ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
      ctx.fillStyle = tone('accent'); ctx.font = disp(900, 17);
      ctx.fillText(spec.goal || '', cx, gy + gh / 2 + 6);
    }

    /* ── 사슬 카드 셋 ──────────────────────────── */
    const cw = 142, ch = 36;
    const tops = [84, 140, 196];

    // 세로 연결선 — 목표 아래부터 마지막 카드까지
    const railTop = gy + gh;
    const railBot = tops[2];
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(cx, railTop); ctx.lineTo(cx, railBot); ctx.stroke();

    for (let i = 0; i < 3; i++) {
      // 켜지는 순서 = 위에서 아래(목표에서 거꾸로 짚어 내려간다)
      const k = clamp((bk - i * 0.24) / 0.5);
      if (k <= 0.01) continue;
      const s = spring(k);
      const ty = tops[i];
      ctx.save(); ctx.translate(cx, ty + ch / 2); ctx.scale(s, s);
      ctx.lineWidth = 3; ctx.strokeStyle = tone('ink'); ctx.fillStyle = '#000';
      roundRect(ctx, -cw / 2, -ch / 2, cw, ch, 6);
      ctx.fill(); ctx.stroke();
      ctx.restore();
      ctx.globalAlpha = k;
      ctx.fillStyle = tone('ink'); ctx.font = disp(800, 15);
      ctx.fillText(chain[i] || '', cx, ty + ch / 2 + 5);
      ctx.globalAlpha = 1;
    }

    /* ── 오른쪽 가장자리 : 짜는 방향 (아래로) ───── */
    if (bk > 0.02) {
      const rx = cx + cw / 2 + 22;
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      ctx.globalAlpha = 0.8;
      const y0 = railTop + 6, y1 = lerp(y0, railBot + ch - 6, bk);
      ctx.beginPath(); ctx.moveTo(rx, y0); ctx.lineTo(rx, y1); ctx.stroke();
      // 아래 방향 화살촉
      ctx.fillStyle = tone('sub');
      ctx.beginPath();
      ctx.moveTo(rx, y1 + 8); ctx.lineTo(rx - 5, y1 - 1); ctx.lineTo(rx + 5, y1 - 1);
      ctx.closePath(); ctx.fill();
      ctx.globalAlpha = 1;
    }

    /* ── 왼쪽 가장자리 : 하는 방향 (위로) ───────── */
    if (rk > 0.02) {
      const lx = cx - cw / 2 - 22;
      setShadow(ctx, GLOW, 10, 0);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 4;
      const y0 = railBot + ch - 6, y1 = lerp(y0, railTop + 6, rk);
      ctx.beginPath(); ctx.moveTo(lx, y0); ctx.lineTo(lx, y1); ctx.stroke();
      ctx.fillStyle = tone('accent');
      ctx.beginPath();
      ctx.moveTo(lx, y1 - 8); ctx.lineTo(lx - 5, y1 + 1); ctx.lineTo(lx + 5, y1 + 1);
      ctx.closePath(); ctx.fill();
      clearShadow(ctx);
    }

    /* ── 계속 도는 것 : 연결선을 따라 위로 흐르는 점 ── */
    {
      const u = frac(t / FLOW);
      const y = lerp(railBot + ch, railTop, u);
      ctx.globalAlpha = 0.5 + 0.5 * Math.sin(u * Math.PI);
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(cx, y, 3.5, 0, Math.PI * 2); ctx.fill();
      ctx.globalAlpha = 1;
    }

    /* ── 두 방향의 이름표 ──────────────────────── */
    const key = (y, color, label, down) => {
      ctx.fillStyle = color;
      ctx.beginPath();
      if (down) { ctx.moveTo(28, y - 4); ctx.lineTo(22, y - 12); ctx.lineTo(34, y - 12); }
      else { ctx.moveTo(28, y - 12); ctx.lineTo(22, y - 4); ctx.lineTo(34, y - 4); }
      ctx.closePath(); ctx.fill();
      ctx.textAlign = 'left';
      ctx.fillStyle = color; ctx.font = disp(700, 11);
      ctx.fillText(label, 42, y - 4);
      ctx.textAlign = 'center';
    };
    if (bk > 0.02) { ctx.globalAlpha = bk; key(262, tone('sub'), spec.backLabel || '', true); ctx.globalAlpha = 1; }
    if (rk > 0.02) { ctx.globalAlpha = rk; key(286, tone('accent'), spec.runLabel || '', false); ctx.globalAlpha = 1; }

    ctx.textAlign = 'left';
  }
};
