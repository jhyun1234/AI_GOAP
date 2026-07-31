import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, GLOW_INK, spring
} from '../../../engine/lib.js';

/* balk — 내려온 명령이 되튄다.

   이 게임의 정체성 한 줄("주민은 명령을 거부할 수 있다", CLAUDE.md 3행)이 걸린 자리다.
   그래서 명령 카드는 주민에게 **닿지 않는다** — 머리 위 얇은 호에 걸려 계속 되튄다.
   말풍선의 문장은 지어낸 것이 아니라 Personality_Docile.asset 38행의 원문 그대로다.

   회차의 기본 도형 안에서 이 그림의 몫은 '위에서 내려온 것이 아래에 못 닿는다' —
   S1 unbidden 의 끊긴 토막이 여기서는 되튀는 물체가 된다.
   계속 도는 것 = 카드 자체의 상하 튐(위치 이동).

   🔴 카드가 주민 원에 겹치면 "닿았다"로 읽힌다. 최저점을 호 위에서 멈춘다. */

const BOUNCE = 1.9;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const dk = ease(cue(spec.dropCue ?? 0, 0.15, 0.55));
    const bk = ease(cue(spec.balkCue ?? 1, 0.12, 0.5));
    const yk = ease(cue(spec.sayCue ?? 2, 0.12, 0.5));

    const cx = w / 2;
    ctx.textBaseline = 'alphabetic';
    ctx.textAlign = 'center';

    /* ── 주민 & 방어 호 ────────────────────────── */
    const ay = 178, ar = 17;
    const shieldY = ay - ar - 12;

    /* ── 명령 카드 ─────────────────────────────── */
    const cw = 150, chh = 46;
    // 등장: 위(-40)에서 내려와 정박 높이로. 그다음 계속 튄다.
    const rest = 96;
    const bob = Math.abs(Math.sin(frac(t / BOUNCE) * Math.PI));   // 0..1..0
    const amp = lerp(6, 22, bk);                                   // 거부 뒤 더 크게 튄다
    const cyTop = lerp(-46, rest, dk) - amp * bob;

    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
    if (dk > 0.2) ctx.fillText(spec.orderLabel || '명령', cx, cyTop - 7);

    {
      const s = spring(clamp(dk * 1.4));
      ctx.save(); ctx.translate(cx, cyTop + chh / 2); ctx.scale(s, s);
      setShadow(ctx, GLOW_INK, 12, 0);
      ctx.lineWidth = 4; ctx.strokeStyle = tone('ink'); ctx.fillStyle = '#000';
      roundRect(ctx, -cw / 2, -chh / 2, cw, chh, 8);
      ctx.fill(); ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
      ctx.fillStyle = tone('ink'); ctx.font = disp(900, 18);
      ctx.fillText(spec.order || '', cx, cyTop + chh / 2 + 7);
    }

    // 카드에서 아래로 향한 짧은 화살촉 — 내려가려는 의지
    if (dk > 0.4) {
      ctx.fillStyle = tone('sub');
      const ty = cyTop + chh + 9;
      ctx.beginPath();
      ctx.moveTo(cx, ty + 8); ctx.lineTo(cx - 6, ty - 1); ctx.lineTo(cx + 6, ty - 1);
      ctx.closePath(); ctx.fill();
    }

    /* ── 되튐 호 — balkCue 에서 팽팽해진다 ─────── */
    if (bk > 0.02) {
      setShadow(ctx, GLOW, 14, 0);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 5;
      ctx.beginPath();
      // 위로 볼록한 호. bk 가 클수록 양옆으로 넓어진다.
      const half = lerp(30, 62, bk);
      ctx.moveTo(cx - half, shieldY + 10);
      ctx.quadraticCurveTo(cx, shieldY - 12, cx + half, shieldY + 10);
      ctx.stroke();
      clearShadow(ctx);
    }

    // 주민
    ctx.lineWidth = 4; ctx.strokeStyle = tone('ink'); ctx.fillStyle = '#000';
    ctx.beginPath(); ctx.arc(cx, ay, ar, 0, Math.PI * 2); ctx.fill(); ctx.stroke();
    ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
    ctx.fillText(spec.agentLabel || '주민', cx, ay + ar + 15);

    /* ── 거부 대사 말풍선 ──────────────────────── */
    if (yk > 0.02) {
      const lines = spec.refuse || [];
      const s = spring(clamp(yk * 1.5));
      const bwMax = w - 56;
      ctx.font = disp(800, 15);
      let tw = 0;
      for (const l of lines) tw = Math.max(tw, ctx.measureText(l).width);
      const bw = Math.min(bwMax, tw + 40);
      const bh = 24 + lines.length * 23;
      const by = 224;

      ctx.save(); ctx.translate(cx, by + bh / 2); ctx.scale(s, s);
      ctx.lineWidth = 4; ctx.strokeStyle = tone('ink'); ctx.fillStyle = '#000';
      roundRect(ctx, -bw / 2, -bh / 2, bw, bh, 10);
      ctx.fill(); ctx.stroke();
      // 꼬리 — 위쪽 주민을 향해
      ctx.beginPath();
      ctx.moveTo(-9, -bh / 2); ctx.lineTo(0, -bh / 2 - 10); ctx.lineTo(9, -bh / 2);
      ctx.closePath(); ctx.fillStyle = '#000'; ctx.fill();
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.moveTo(-9, -bh / 2); ctx.lineTo(0, -bh / 2 - 10); ctx.lineTo(9, -bh / 2);
      ctx.stroke();
      ctx.restore();

      ctx.globalAlpha = yk;
      ctx.fillStyle = tone('ink'); ctx.font = disp(800, 15);
      lines.forEach((l, i) => ctx.fillText(l, cx, by + 30 + i * 23));
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
