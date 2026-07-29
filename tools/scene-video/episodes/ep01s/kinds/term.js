import {
  disp, CJK,
  ease, easeOut, clamp, lerp, spring, depthGrad, setShadow, clearShadow, GLOW,
  fitCanvas, mkCanvas, tone
} from '../../../engine/lib.js';

/* term — 이름을 붙이는 샷. 이 회차에서 새로 생긴 자리다.

   원문에 이렇게 있다: "이 프로젝트 내부에서는 이런 상황을 무해(無解)라고 부릅니다.
   한자 그대로 '풀이가 없다'는 뜻입니다." 그런데 정본 ep01 도 쇼츠 컷 초안도
   이 단어를 씬 파일의 source 주석에만 두고 화면에는 한 번도 안 올렸다.
   쇼츠에서 이건 손해다 — 이름 붙은 개념 하나가 시청자가 들고 나가는 물건이고,
   이름이 없으면 "답이 없다"를 나올 때마다 다시 풀어 설명하게 된다.

   한글 우선 규칙(SKILL.md 227절): 주인공은 한글 '무해'다.
   한자는 뜻풀이의 근거로 뒤에 큰 글자로 깔고, 원어 표기는 보조로만 둔다. */

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, cue, nLines }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    const cx = w / 2, cy = h * 0.44;

    const nameK = clamp(cue(spec.nameCue ?? 0, 0.25, 0.6));
    const glossK = ease(clamp(cue(spec.glossCue ?? nLines - 1, 0.2, 0.55)));

    ctx.textAlign = 'center';

    /* 한자 — 한글 위에 얹는 적층 구성.
       🔴 처음엔 화면 폭만 한 한자를 뒤에 깔고 그 위에 한글을 얹었는데, 실제 프레임을
       뽑아 보니 획이 한글 사이로 삐져나와 글자가 서로 갉아먹었고 좌우로 잘리기까지 했다.
       배경이 아니라 요소로 대접한다 — 한글보다 작게, 겹치지 않게 위에 둔다. */
    if (spec.origin) {
      ctx.globalAlpha = ease(clamp(nameK * 1.2)) * 0.85;
      ctx.fillStyle = tone('sub');
      ctx.font = `900 ${Math.round(h * 0.17)}px ${CJK}`;
      ctx.fillText(spec.origin.split('').join(' '), cx, cy - h * 0.19);
      ctx.globalAlpha = 1;
    }

    /* 주인공 — 한글 이름. 스프링으로 들어온다 */
    const s = spring(nameK);
    ctx.save();
    ctx.translate(cx, cy); ctx.scale(s, s); ctx.translate(-cx, -cy);
    ctx.globalAlpha = ease(clamp(nameK * 1.5));
    setShadow(ctx, GLOW, 30, 6);
    ctx.font = disp(900, Math.round(h * 0.30));
    ctx.fillStyle = depthGrad(ctx, cx, cy - h * 0.18, cx, cy + h * 0.06, 'accent');
    ctx.fillText(spec.name || '', cx, cy);
    clearShadow(ctx);
    ctx.globalAlpha = 1;
    ctx.restore();

    /* 뜻풀이 — 밑줄이 그어지고 그 위에 앉는다.
       밑줄을 먼저 그어야 "이게 정의다"가 서고, 글자만 뜨면 그냥 한 줄 더가 된다 */
    if (spec.gloss && glossK > 0.01) {
      const uy = h * 0.78, half = w * 0.36 * easeOut(glossK);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineCap = 'round';
      ctx.beginPath(); ctx.moveTo(cx - half, uy); ctx.lineTo(cx + half, uy); ctx.stroke();

      ctx.globalAlpha = ease(clamp(glossK * 1.6 - 0.25));
      ctx.fillStyle = tone('ink');
      ctx.font = disp(900, 19);
      ctx.fillText(spec.gloss, cx, uy - 14);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
