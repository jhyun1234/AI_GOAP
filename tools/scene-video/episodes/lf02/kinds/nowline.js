import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* nowline — 지금 상태 칸 셋. 둘은 채워지고 하나는 비어 있으며, 비어 있는 이유가
   사람 쪽 표식으로 그 칸 안에 적혀 있다.
   🔴 울타리·문·계획 고스트가 찍힌 클립은 수확에 0개다. 그래서 이 마디는 캔버스로만
   그리고, 캔버스 샷에는 「지금 빌드 화면」 배지가 붙지 않으므로 화면이 스스로
   사실관계를 지킨다(브리프 ⑦).
   연속 모션 = 가운데 칸에서 계속 휘둘러지는 도구 호. 「정지 그림의 시대를 끝냈다」를
   말하는 샷이 멈추면 자기모순이다.
   문자열 출처: devlog/sessions/2026-08-09.md 세션 마감 · 다음에 할 일 ①. */

const CARDS = [
  { k: '울타리 · 문', v: 'M22' },
  { k: '행동 모션 5종', v: 'M23' },
  { k: '늑대 · 곰 그림', v: '' },
];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    const kBuild = ease(cue(0));   // 울타리와 문을 짓는 중
    const kMotion = ease(cue(1));  // 몸짓으로 보이게
    const kGap = ease(cue(2));     // 늑대와 곰은 아직 색 원

    const gap = 16;
    const cw = (w - M * 2 - gap * 2) / 3;
    const cy = 52, ch = h - cy - 44;

    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('NOW', M, 34);

    for (let i = 0; i < 3; i++) {
      const cx = M + i * (cw + gap);
      const k = i === 0 ? kBuild : (i === 1 ? kMotion : kGap);
      const empty = i === 2;
      ctx.strokeStyle = k > 0.1 ? (empty ? tone('ink') : tone('accent')) : tone('track');
      ctx.lineWidth = 3;
      if (empty) ctx.setLineDash([8, 6]);
      roundRect(ctx, cx, cy, cw, ch, 4); ctx.stroke();
      ctx.setLineDash([]);

      // 제목
      let fs = 17; ctx.font = disp(900, fs);
      while (ctx.measureText(CARDS[i].k).width > cw - 22 && fs > 10) {
        fs -= 1; ctx.font = disp(900, fs);
      }
      const sw = ctx.measureText(CARDS[i].k).width;
      ctx.fillStyle = k > 0.1 ? (empty ? tone('ink') : tone('accent')) : tone('sub');
      ctx.fillText(CARDS[i].k, cx + (cw - sw) / 2, cy + 28);

      if (CARDS[i].v) {
        ctx.font = mono(700, 9); ctx.fillStyle = tone('sub');
        const vw = ctx.measureText(CARDS[i].v).width;
        ctx.fillText(CARDS[i].v, cx + cw - vw - 10, cy + 18);
      }

      // 0번 : 이어진 줄 (조각이 붙어 하나가 된다)
      if (i === 0 && k > 0.05) {
        const n = 6, sy = cy + ch / 2 + 6;
        const bw = (cw - 40) / n;
        for (let j = 0; j < n; j++) {
          const kj = clamp((k - j * 0.09) / 0.4);
          if (kj <= 0.02) continue;
          ctx.fillStyle = tone('accent');
          const jx = cx + 20 + j * bw;
          ctx.fillRect(jx, sy - 8 * ease(kj), Math.max(3, bw - 3), 16 * ease(kj));
        }
        // 문 한 칸
        if (k > 0.6) {
          ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
          const dx = cx + 20 + 3 * bw;
          ctx.strokeRect(dx - 2, sy - 12, bw + 1, 24);
        }
      }

      // 1번 : 계속 휘둘러지는 도구 호 (연속 모션)
      if (i === 1) {
        const mx = cx + cw / 2, my = cy + ch / 2 + 12;
        const sw2 = Math.sin(t * 3.2);
        const a0 = -2.5 + 0.9 * sw2;
        ctx.strokeStyle = k > 0.1 ? tone('accent') : tone('sub');
        ctx.lineWidth = 4;
        ctx.beginPath();
        ctx.arc(mx, my, 26, a0, a0 + 1.1);
        ctx.stroke();
        ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(mx, my);
        ctx.lineTo(mx + 24 * Math.cos(a0 + 0.55), my + 24 * Math.sin(a0 + 0.55));
        ctx.stroke();
        ctx.beginPath(); ctx.arc(mx, my + 14, 7, 0, Math.PI * 2); ctx.stroke();
      }

      // 2번 : 비어 있는 칸 + 사람 몫 표식
      if (i === 2) {
        const mx = cx + cw / 2, my = cy + ch / 2 + 8;
        // 아직은 색 원
        ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.arc(mx, my - 6, 15, 0, Math.PI * 2); ctx.stroke();
        if (k > 0.3) {
          ctx.save(); ctx.globalAlpha = clamp((k - 0.3) / 0.5);
          ctx.font = disp(900, 13); ctx.fillStyle = tone('ink');
          const s = '사람 몫 — 에셋 조달';
          let fs2 = 13;
          ctx.font = disp(900, fs2);
          while (ctx.measureText(s).width > cw - 20 && fs2 > 9) {
            fs2 -= 1; ctx.font = disp(900, fs2);
          }
          const sw3 = ctx.measureText(s).width;
          ctx.fillText(s, cx + (cw - sw3) / 2, cy + ch - 16);
          ctx.restore();
        }
      }
    }
  },
};
