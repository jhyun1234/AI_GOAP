import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, tone, roundRect, setShadow, clearShadow, GLOW, spring
} from '../../../engine/lib.js';

/* flip — 이름과 방향을 동시에 뒤집는다.

   회전축을 중심으로 반원 회전. 앞면에는 HungerLevel/↓/30, 뒷면에는 SatietyLevel/↑/70.
   회전은 flipCue 로 진행되고, settleCue 에서 뒷면 강조가 들어온다. 반원 회전은 두 값(이름과
   방향)을 한 번에 담는다 — 이 시리즈에 없었던 움직임이다.

   회차의 기본 도형 안에서 이 그림의 몫은 '반대를 뒤집는다'.
   계속 도는 것 = 회전축을 기준으로 오르내리는 값 표식. */

const BOB = 2.6;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const before = spec.before || {};
    const after = spec.after || {};
    const fk = ease(cue(spec.flipCue ?? 1, 0.15, 0.6));
    const sk = ease(cue(spec.settleCue ?? 2, 0.15, 0.5));

    const cx = w / 2;
    const cy = h / 2 - 8;
    const cardW = 220;
    const cardH = 140;

    ctx.textBaseline = 'alphabetic';

    // 회전축 — 가로선 (뒤에 그림자로 남기)
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(cx - cardW / 2 - 20, cy);
    ctx.lineTo(cx + cardW / 2 + 20, cy);
    ctx.stroke();

    // 회전 각도 : 0 → PI. Math.cos 으로 스케일 → -1 ~ +1
    // fk=0 : 정면(before), fk=0.5 : 옆(안 보임), fk=1 : 반전(after)
    const angle = fk * Math.PI;
    const scaleY = Math.cos(angle);          // 1 → -1
    const showAfter = scaleY < 0;
    const absScale = Math.abs(scaleY);
    const drawn = showAfter ? after : before;

    // 카드
    const drawCard = () => {
      ctx.save();
      ctx.translate(cx, cy);
      ctx.scale(1, absScale);

      // 배경
      ctx.fillStyle = '#000';
      roundRect(ctx, -cardW / 2, -cardH / 2, cardW, cardH, 8);
      ctx.fill();
      // 테두리
      const emphasis = showAfter ? tone('accent') : tone('ink');
      if (showAfter) setShadow(ctx, GLOW, 14, 0);
      ctx.lineWidth = 3; ctx.strokeStyle = emphasis;
      roundRect(ctx, -cardW / 2, -cardH / 2, cardW, cardH, 8);
      ctx.stroke();
      clearShadow(ctx);

      // 이름
      ctx.textAlign = 'center';
      ctx.fillStyle = emphasis; ctx.font = mono(700, 16);
      ctx.fillText(drawn.name || '', 0, -cardH / 2 + 30);

      // 방향 화살표 (큰 글자)
      ctx.font = disp(900, 44);
      ctx.fillStyle = emphasis;
      ctx.fillText(drawn.sign || '', 0, 6);

      // 값
      ctx.fillStyle = emphasis; ctx.font = disp(900, 26);
      ctx.fillText(String(drawn.value ?? ''), 0, cardH / 2 - 34);

      // 부제
      ctx.fillStyle = tone('sub'); ctx.font = disp(700, 10);
      ctx.fillText(drawn.note || '', 0, cardH / 2 - 14);

      ctx.restore();
    };
    drawCard();

    // 회전 중간(옆) — 얇은 선만 남는다는 사실을 이용해 접혀 있음을 보여준다
    if (absScale < 0.06) {
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 5;
      setShadow(ctx, GLOW, 12, 0);
      ctx.beginPath();
      ctx.moveTo(cx - cardW / 2, cy); ctx.lineTo(cx + cardW / 2, cy);
      ctx.stroke();
      clearShadow(ctx);
    }

    /* ── 계속 도는 것 : 카드 위 값 표식 ─────────
       회전과 상관없이 위·아래로 오르내린다 — 이 편이 다루는 '방향' 자체를 계속 상기시킨다. */
    {
      const u = frac(t / BOB);
      const bob = Math.sin(u * Math.PI * 2);   // -1 ~ 1
      const mx = cx + cardW / 2 + 40;
      const my = cy - bob * 24;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      ctx.globalAlpha = 0.55;
      // 위로 향한 삼각형 or 아래
      if (bob > 0) {
        ctx.beginPath();
        ctx.moveTo(mx, my - 6); ctx.lineTo(mx - 5, my + 3); ctx.lineTo(mx + 5, my + 3);
        ctx.closePath(); ctx.fillStyle = tone('ink'); ctx.fill();
      } else {
        ctx.beginPath();
        ctx.moveTo(mx, my + 6); ctx.lineTo(mx - 5, my - 3); ctx.lineTo(mx + 5, my - 3);
        ctx.closePath(); ctx.fillStyle = tone('ink'); ctx.fill();
      }
      ctx.globalAlpha = 1;
    }

    // 완료 표식 : settleCue 에서 카드 아래 밑줄
    if (sk > 0.02 && showAfter) {
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      setShadow(ctx, GLOW, 10, 0);
      const uw = cardW - 60;
      const ux = cx - uw / 2;
      const uy = cy + cardH / 2 + 16;
      ctx.beginPath();
      ctx.moveTo(ux, uy); ctx.lineTo(ux + uw * ease(sk), uy);
      ctx.stroke();
      clearShadow(ctx);
    }

    ctx.textAlign = 'left';
  }
};
