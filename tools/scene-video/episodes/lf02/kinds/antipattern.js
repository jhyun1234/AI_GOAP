import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* antipattern — "C 키가 정말 비어 있는가". 키 판을 하나씩 열어 보니 대부분은
   이미 임자가 있고, 딱 한 칸만 비어 있었다.
   이 회차의 표기대로 임자가 있는 칸은 흰 칸, 새로 들어갈 빈 칸만 강조 칸이다.
   아래에는 안티패턴 이름표 한 줄이 남는다 — 「기억 기반 설계」.
   연속 모션 = 키 판을 훑는 점검 띠. 실사는 명세마다 매번 다시 도는 절차다.
   문자열 출처: M15 글 35행(1~9 배속 / Y·N 방랑자 / 에디터 전용 / C 는 비어 있었다). */

/* 🔴 v2 — 화면 글자 한국어화(`ADR-V-10` 2026-08-10 개정).
   키 이름(`1-9`·`Y / N`·`C`·`?`)만 남는다 — 키보드에 새겨진 기호라 옮길 것이 없다.
   임자 이름은 전부 M15 글 35행의 한국어 그대로다(배속 · 방랑자 · 에디터 전용). */
const KEYS = [
  { k: '1-9', v: '배속' },
  { k: 'Y / N', v: '방랑자' },
  { k: '?', v: '에디터 전용' },
  { k: 'C', v: '' },
];

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    const kCheck = ease(cue(0));   // 사소해 보이는 확인도 한다
    const kC = ease(cue(1));       // C 키가 정말 비어 있는가
    const kClash = ease(cue(2));   // 안 봤으면 충돌하는 토글을 만들 뻔
    const kName = ease(cue(3));    // 안티패턴 이름표

    const gap = 12;
    const cw = (w - M * 2 - gap * 3) / 4;
    const cy = 54, ch = 84;

    // 연속 모션 : 키 판을 훑는 점검 띠
    {
      const PR = 3.0, k = (t % PR) / PR;
      const bx = lerp(M - 30, w - M + 30, k);
      ctx.fillStyle = tone('track');
      ctx.fillRect(bx - 20, cy - 8, 40, ch + 16);
    }

    for (let i = 0; i < KEYS.length; i++) {
      const cx = M + i * (cw + gap);
      const taken = i < 3;
      const kIn = clamp((kCheck - i * 0.12) / 0.45);
      const free = i === 3;
      const kFree = free ? kC : 0;

      ctx.strokeStyle = kIn > 0.1 ? (free && kFree > 0.2 ? tone('accent') : tone('ink')) : tone('track');
      ctx.lineWidth = free && kFree > 0.2 ? 4 : 3;
      roundRect(ctx, cx, cy, cw, ch, 4); ctx.stroke();

      // 키 이름
      let fs = 26;
      ctx.font = disp(900, fs);
      while (ctx.measureText(KEYS[i].k).width > cw - 24 && fs > 13) {
        fs -= 1; ctx.font = disp(900, fs);
      }
      const kwid = ctx.measureText(KEYS[i].k).width;
      ctx.fillStyle = free && kFree > 0.2 ? tone('accent') : tone('ink');
      ctx.fillText(KEYS[i].k, cx + (cw - kwid) / 2, cy + 42);

      // 임자
      ctx.font = disp(700, 11);
      if (taken && kIn > 0.2) {
        ctx.fillStyle = tone('sub');
        const vw = ctx.measureText(KEYS[i].v).width;
        ctx.fillText(KEYS[i].v, cx + (cw - vw) / 2, cy + 68);
      }
      if (free && kFree > 0.3) {
        ctx.fillStyle = tone('accent');
        const s = '비어 있음';
        const vw = ctx.measureText(s).width;
        ctx.fillText(s, cx + (cw - vw) / 2, cy + 68);
      }
    }

    ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('키 배정', M, 34);

    // 충돌 경고 — 안 봤으면 배속·방랑자 칸과 겹쳤을 것이다
    if (kClash > 0.05) {
      ctx.save(); ctx.globalAlpha = clamp(kClash);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      const c3 = M + 3 * (cw + gap) + cw / 2;
      for (const j of [0, 1]) {
        const cj = M + j * (cw + gap) + cw / 2;
        ctx.beginPath();
        ctx.moveTo(c3, cy + ch + 8);
        ctx.quadraticCurveTo((c3 + cj) / 2, cy + ch + 34, cj, cy + ch + 8);
        ctx.stroke();
      }
      ctx.restore();
    }

    // 안티패턴 이름표
    if (kName > 0.05) {
      ctx.save(); ctx.globalAlpha = clamp(kName);
      const s = '기억 기반 설계';
      ctx.font = disp(900, 17);
      const sw = ctx.measureText(s).width;
      const bx = M, by = h - 42;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
      roundRect(ctx, bx, by, sw + 26, 26, 3); ctx.stroke();
      ctx.fillStyle = tone('ink');
      ctx.fillText(s, bx + 13, by + 19);
      ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText('하면 안 되는 것 목록', bx + sw + 36, by + 18);
      ctx.restore();
    }
  },
};
