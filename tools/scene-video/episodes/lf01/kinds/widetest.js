import { disp, ease, clamp, fitCanvas, mkCanvas, tone } from '../../../engine/lib.js';

/* widetest — W1 가로 프로필 시험용. 가로 캔버스의 네 모서리·중앙을 한눈에 확인한다.
   ① 바깥 테두리(3px) — 잘림이 있으면 모서리가 사라져 바로 보인다
   ② 중앙 라벨 — spec.label 을 fitFont 로 폭을 재서 찍는다
   ③ 자막에 물린 진행 막대 — cue(0) 로 자막 진행과 화면이 동기함을 확인
   본편 대본이 오면 이 그림은 통째로 버려진다. */

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    // ① 바깥 테두리 — 3px, 흰색. 네 변이 다 보여야 잘림이 없다
    ctx.strokeStyle = tone('ink');
    ctx.lineWidth = 3;
    ctx.strokeRect(1.5, 1.5, w - 3, h - 3);

    // ② 중앙 라벨 — 폭을 재서 넣는다 (캔버스 밖으로 그리면 잘린다)
    const label = spec.label || 'WIDE';
    let fs = 44;
    ctx.font = disp(900, fs);
    while (fs > 10 && ctx.measureText(label).width > w * 0.8) { fs -= 1; ctx.font = disp(900, fs); }
    ctx.fillStyle = spec.phase ? tone('accent') : tone('ink');
    const tw = ctx.measureText(label).width;
    ctx.fillText(label, (w - tw) / 2, h / 2 + fs * 0.35);

    // ③ 자막 진행 막대 — cue(0) 0~1 을 그대로 폭으로
    const p = ease(clamp(cue(0)));
    ctx.fillStyle = tone('accent');
    ctx.fillRect(w * 0.1, h * 0.78, w * 0.8 * p, 5);
  },
};
