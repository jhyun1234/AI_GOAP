import {
  disp, mono,
  ease, clamp, lerp, spring, depthGrad, setShadow, clearShadow, GLOW,
  fitCanvas, mkCanvas, tone, roundRect
} from '../lib.js';

/* metrics — 수치 여러 개를 한 화면에. 가이드 SKILL.md 패턴 16(paired metric cards).

   이 회차에서 되살린 자리다. 원문의 진단 로그
   `ByReason=[Plan=3, Res=0, Path=0, Pre=0]` 를 정본 ep01 은 콘솔 텍스트로 뿌렸고
   (검정 화면에 잉크가 거의 없어 가이드의 '텍스트만 세그먼트'에 가장 가까웠다)
   쇼츠 컷 초안은 아예 지웠다. 둘 다 틀렸다 — 이건 핵심 수치 4개 동시라
   결정 트리가 가리키는 답이 처음부터 카드 그리드였다.
   숫자만 크게 놓으면 로그를 읽을 줄 몰라도 "원인이 한 칸에 몰렸다"가 보인다.

   가이드 패턴 16 핵심 원칙 그대로:
     숫자가 주인공 / 라벨은 짧게 보조색 / 하나만 강조 / 등장은 시간차. */

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue, nLines }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const cards = spec.cards || [];
    const cols = spec.cols || 2;
    const rows = Math.ceil(cards.length / cols);

    // 위아래로 조금 띄운다 — 강조 카드의 그림자가 프레임 경계에서 잘리지 않게
    const capH = spec.caption ? 26 : 0;
    const PAD = 8, gap = 10;
    const cw = (w - gap * (cols - 1)) / cols;
    const chh = (h - capH - PAD * 2 - gap * (rows - 1)) / rows;

    const reveal = cue(spec.showCue ?? 0, 0.25, 0.85);

    cards.forEach((c, i) => {
      // 시간차 등장 — 동시에 켜면 "0 세 개와 3 하나"가 한 덩어리로 뭉친다
      const k = clamp(reveal * cards.length - i * 0.75);
      if (k <= 0) return;
      /* 두 번째 자막("길도 자원도 아니었어요")에서 0 인 칸들이 차례로 물러나고 강조 칸만 남는다.
         카드가 다 깔린 뒤 화면이 6초간 완전히 멈추던 자리다(가이드 상한 3초). 다만 이건
         정적 구간을 메우려고 넣은 장식이 아니라 나레이션이 실제로 하는 말이다 —
         "셋 다 계획을 못 짠 거였죠"가 화면에서 그대로 일어난다. */
      const dim = c.accent ? 0
        : ease(clamp(cue(spec.dimCue ?? 1, 0.2, 0.75) * cards.length - i * 0.8)) * 0.62;
      const s = spring(k);
      const bx = (i % cols) * (cw + gap), by = PAD + Math.floor(i / cols) * (chh + gap);
      const cx = bx + cw / 2, cy = by + chh / 2;
      // 기준 크기를 0.95 로 줄여 두고 스프링을 곱한다 — 최대 1.05 배가 원래 칸이 된다
      const bw = cw * 0.95 * s, bh = chh * 0.95 * s;

      ctx.globalAlpha = ease(clamp(k * 1.4)) * (1 - dim);
      ctx.lineWidth = 3;
      if (c.accent) {
        /* 강조 카드의 빛이 천천히 숨 쉰다. 카드가 다 깔린 뒤로는 화면이 완전히 멈추는데,
           실측으로 이 샷의 정적 구간이 6초였다(가이드 상한 3초). 눈이 가야 할 곳을
           계속 가리키는 움직임이라 장식도 아니다. 진폭은 그림자 세기만 — 자리는 안 움직인다. */
        const breathe = 22 + Math.sin(t * 2.2) * 9;
        setShadow(ctx, GLOW, breathe, 4);
        ctx.strokeStyle = tone('accent');
        ctx.fillStyle = 'rgba(0,255,136,.13)';
      } else {
        ctx.strokeStyle = tone('track');
        ctx.fillStyle = 'transparent';
      }
      roundRect(ctx, cx - bw / 2, cy - bh / 2, bw, bh, 5);
      if (c.accent) ctx.fill();
      ctx.stroke();
      clearShadow(ctx);

      // 숫자 — 주인공
      ctx.textAlign = 'center';
      ctx.font = disp(900, Math.round(bh * 0.42));
      ctx.fillStyle = c.accent
        ? depthGrad(ctx, cx, cy - bh * 0.3, cx, cy + bh * 0.12, 'accent')
        : tone('ink');
      ctx.fillText(String(c.value), cx, cy + bh * 0.10);

      // 라벨 — 보조. 가이드: 라벨은 짧게, 0.7 이 허용 최소치
      ctx.font = disp(700, 12);
      ctx.fillStyle = c.accent ? tone('accent') : tone('sub');
      ctx.fillText(c.label, cx, cy + bh * 0.38);
      ctx.globalAlpha = 1;
    });

    // 원문 로그 한 줄 — 개발자 시청자에게는 이게 증거다. 카드가 다 선 뒤에 붙는다
    if (spec.caption) {
      ctx.globalAlpha = ease(clamp(reveal * 1.1 - 0.45)) * 0.85;
      ctx.textAlign = 'left';
      ctx.fillStyle = tone('sub');
      ctx.font = mono(700, 11);
      ctx.fillText(spec.caption, 0, h - 6);
      ctx.globalAlpha = 1;
    }
    ctx.textAlign = 'left';
  }
};
