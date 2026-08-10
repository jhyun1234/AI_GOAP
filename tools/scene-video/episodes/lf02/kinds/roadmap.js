import { ease, clamp, lerp, spring, tone, disp, mono, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* roadmap — 이 영상이 답할 질문 다섯 칸. 검색으로 들어온 사람이 "내가 기다리는 답이
   몇 번째인가"를 알고 보게 하는 그림이다.
   네 번째 칸(AI가 실수하면)만 표식 없이 테두리가 계속 숨쉬며 남는다 — 콜드 오픈이
   열어 둔 고리가 거기서 회수되기 때문이고, 그 호흡이 이 샷의 연속 모션이다. */

const CELLS = [
  { n: '01', k: '결과물' },
  { n: '02', k: '방법' },
  { n: '03', k: '경계' },
  { n: '04', k: '실수' },
  { n: '05', k: '기억' },
];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    const kOpen = ease(cue(0));   // 다섯 칸으로 나눈다
    const kA = ease(cue(1));      // 앞 세 칸
    const kB = ease(cue(2));      // 뒤 두 칸
    const kMark = ease(cue(3));   // 네 번째를 지목

    const gap = 14;
    const cw = (w - M * 2 - gap * 4) / 5;
    const ch = 96, cy = 62;

    /* 🔴 2차 개정 (검수 C6): 연속 모션이 테두리 호흡과 화살 흔들림뿐이라 면적이
       문턱 아래였다. 다섯 칸을 차례로 훑는 46px 폭 띠를 세운다 — 「질문을 하나씩
       짚어 간다」가 이 샷의 내용 그대로다. 칸보다 **먼저** 그린다. */
    {
      const PR = 3.3, k = (t % PR) / PR;
      const bx = lerp(M - 20, w - M + 20, k);
      ctx.fillStyle = tone('track');
      ctx.fillRect(bx - 23, cy - 12, 46, ch + 24);
    }

    for (let i = 0; i < 5; i++) {
      const cx = M + i * (cw + gap);
      // 등장 : 왼쪽부터
      const kIn = clamp((kOpen - i * 0.11) / 0.5);
      if (kIn <= 0.02) continue;
      const s = spring(kIn) * 0.97;
      const px = cx + cw / 2, py = cy + ch / 2;

      ctx.save();
      ctx.translate(px, py); ctx.scale(s, s); ctx.translate(-px, -py);

      // 네 번째 칸 : 답이 아직 없는 칸 — 테두리가 계속 숨쉰다
      const open4 = i === 3;
      const breath = open4 ? 0.55 + 0.45 * (0.5 - 0.5 * Math.cos(t * 2.0)) : 1;
      ctx.globalAlpha = clamp(kIn) * breath;
      ctx.strokeStyle = open4 && kMark > 0.3 ? tone('accent') : tone('ink');
      ctx.lineWidth = open4 && kMark > 0.3 ? 4 : 3;
      roundRect(ctx, cx, cy, cw, ch, 4); ctx.stroke();
      ctx.globalAlpha = 1;

      // 번호
      ctx.font = mono(700, 11);
      ctx.fillStyle = tone('sub');
      ctx.fillText(CELLS[i].n, cx + 10, cy + 22);

      // 라벨 (폭을 재서 칸 안에 가둔다)
      let fs = 17;
      ctx.font = disp(900, fs);
      while (ctx.measureText(CELLS[i].k).width > cw - 20 && fs > 11) {
        fs -= 1; ctx.font = disp(900, fs);
      }
      const lw = ctx.measureText(CELLS[i].k).width;
      ctx.fillStyle = open4 && kMark > 0.3 ? tone('accent') : tone('ink');
      ctx.fillText(CELLS[i].k, cx + (cw - lw) / 2, cy + ch / 2 + 12);

      ctx.restore();

      // 표식 — 앞 세 칸은 kA, 뒤 두 칸은 kB. 네 번째는 표식이 안 앉는다.
      const kFill = i < 3 ? clamp((kA - i * 0.2) / 0.5) : (i === 4 ? clamp((kB - 0.25) / 0.5) : 0);
      if (kFill > 0.02) {
        ctx.fillStyle = tone('accent');
        ctx.fillRect(cx + 10, cy + ch - 14, (cw - 20) * ease(kFill), 5);
      }
    }

    // 아래 : 네 번째를 가리키는 표시
    if (kMark > 0.02) {
      const cx = M + 3 * (cw + gap);
      ctx.save(); ctx.globalAlpha = clamp(kMark);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      const ax = cx + cw / 2, ay = cy + ch + 10;
      const bob = 3 * Math.sin(t * 3.4);
      ctx.beginPath();
      ctx.moveTo(ax, ay + 20 + bob); ctx.lineTo(ax, ay + 4 + bob);
      ctx.moveTo(ax - 7, ay + 12 + bob); ctx.lineTo(ax, ay + 4 + bob);
      ctx.lineTo(ax + 7, ay + 12 + bob);
      ctx.stroke();
      ctx.restore();
    }

    // 상단 라벨 (v2: 한국어. 칸 번호 `01`~`05` 는 숫자라 그대로 둔다)
    ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('질문 다섯', M, 30);
  },
};
