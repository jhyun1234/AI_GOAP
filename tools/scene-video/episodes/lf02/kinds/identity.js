import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* identity — 🔴 **v2 전면 재설계** (`ADR-V25-15` 라벨 가림 시험 미달).
   v1 은 규칙 문서 두 줄을 판에 새기는 그림이었는데, **인용문 자체가 내용**이라
   라벨을 가리면 아무것도 안 남았다.

   v2 의 사건 = **두 판의 위아래가 뒤바뀐다.**
   문서에서는 04행이 위, 05행이 아래인데 — 05행이 스스로 「최우선 가치」라고 적혀 있어서
   **위계로는 05가 04 위**다. 그 어긋남이 이 샷의 전부이고, 판이 실제로 자리를 바꾼다.
   라벨을 전부 가려도 「아래 있던 것이 위로 올라가 다른 것 위에 섰다」가 남는다.

   🔴 판에는 **자막이 말하지 않는 것만** 둔다(v1 검수 C8 — 판이 자막을 복사하면 반려).
   본문 두 문장은 자막이 지고, 판은 **문서에서의 자리(04행·05행)와 그 자리에 붙은
   이름표**만 진다. 이름표 「게임 정체성」·「최우선 가치」는 `Docs/CLAUDE.md` 4·5행이
   문장 앞머리에 실제로 달아 둔 말이다 — 지어낸 라벨이 아니다.

   🔴 행 번호는 v1 5차 정정에서 실측으로 못 박은 값이다: 3행은 "Unity 탑다운 마을 생존
   시뮬레이션…"이라는 다른 문장, 6행은 빈 줄. **4행 = 게임 정체성 · 5행 = 최우선 가치.**

   연속 모션 = 두 판을 가로로 훑는 각인 광택(52px 폭). 판보다 먼저 그린다. */

const A_TAG = '04행', A_LABEL = '게임 정체성';
const B_TAG = '05행', B_LABEL = '최우선 가치';

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    const kDoc  = ease(cue(0));   // 규칙 문서 맨 위에 한 줄로
    const kA    = ease(cue(1));   // 04행이 놓인다
    const kB    = ease(cue(2));   // 05행이 그 아래 놓인다
    const kSwap = ease(cue(3));   // 05가 04 위로 올라간다
    const kBar  = ease(cue(4));   // 맨 위가 확정된다

    const px = M, pw = w - M * 2;
    const slotTop = 74, slotBot = 150, plateH = 60;

    // 연속 모션 : 두 판을 훑는 각인 광택 (판보다 먼저)
    {
      const PR = 3.8, k = (t % PR) / PR;
      const gx = lerp(px - 40, px + pw + 40, k);
      ctx.save();
      ctx.beginPath(); ctx.rect(px, slotTop - 12, pw, (slotBot + plateH + 12) - (slotTop - 12));
      ctx.clip();
      ctx.fillStyle = tone('track');
      ctx.fillRect(gx - 26, slotTop - 12, 52, (slotBot + plateH + 12) - (slotTop - 12));
      ctx.restore();
    }

    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('Docs/CLAUDE.md', M, 26);
    ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('맨 위 두 줄', M + 118, 26);

    /* 판 하나 그리기 — 태그(행 번호) + 이름표. 본문 문장은 넣지 않는다.
       🔴 두 판이 세로로 자리를 바꾸므로 **교차 구간에서 겹칠 수 있다.**
       그래서 바꾸는 동안 판이 좁아지며 하나는 왼쪽, 하나는 오른쪽으로 갈라선다
       (`bulge` = sin, 시작·끝에서 0 이라 제자리에서는 둘 다 전폭이다).
       좌표가 아니라 **폭의 부등식**으로 겹침을 막는다: 두 판 폭의 합이 언제나
       `pw - 20` 이하이므로 x 구간이 교집합을 가질 수 없다. */
    const plate = (x, pwIn, y, tag, label, k, accent) => {
      if (k <= 0.02) return;
      const col = accent ? tone('accent') : tone('ink');
      ctx.save(); ctx.globalAlpha = clamp(0.25 + 0.75 * k);

      ctx.strokeStyle = col; ctx.lineWidth = 3;
      roundRect(ctx, x, y, pwIn, plateH, 4); ctx.stroke();

      // 행 번호 태그
      const tagX = x + 14, tagW = 54, tagY = y + (plateH - 26) / 2;
      ctx.strokeStyle = col; ctx.lineWidth = 3;
      roundRect(ctx, tagX, tagY, tagW, 26, 3); ctx.stroke();
      ctx.font = disp(700, 12); ctx.fillStyle = col;
      const tw = ctx.measureText(tag).width;
      ctx.fillText(tag, tagX + (tagW - tw) / 2, tagY + 18);

      // 이름표 — 폭을 재서 판 안에 가둔다
      let fs = 22;
      const avail = pwIn - (tagW + 14 + 20 + 16);
      ctx.font = disp(900, fs);
      while (ctx.measureText(label).width > avail && fs > 10) {
        fs -= 1; ctx.font = disp(900, fs);
      }
      if (ctx.measureText(label).width <= avail) {
        ctx.fillStyle = col;
        ctx.fillText(label, tagX + tagW + 20, y + plateH / 2 + fs * 0.36);
      }

      ctx.restore();
    };

    // 자리 바꿈 — 05 가 위로, 04 가 아래로. 같은 k 를 반대로 쓴다.
    const sw = ease(clamp(kSwap));
    const yA = lerp(slotTop, slotBot, sw);
    const yB = lerp(slotBot, slotTop, sw);
    const bulge = Math.sin(Math.PI * sw);
    const pwIn = (pw - 20) * (1 - 0.52 * bulge);
    const xA = px + 10;
    const xB = px + pw - 10 - pwIn;

    // 뒤에 놓이는 판을 먼저 그린다(겹치는 구간에서 올라오는 판이 위에 보이게)
    plate(xA, pwIn, yA, A_TAG, A_LABEL, kA, false);
    plate(xB, pwIn, yB, B_TAG, B_LABEL, kB, true);

    /* 맨 위 표식 — 자리 바꿈이 끝나면 위쪽 판 위에 굵은 강조 막대가 걸린다.
       「무엇이 맨 위에 섰나」를 글자 없이 남기는 자리다. */
    if (kBar > 0.05) {
      const kk = clamp(kBar);
      ctx.save(); ctx.globalAlpha = clamp(kk);
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 6;
      const y = slotTop - 12;
      const x0 = px + 10, x1 = px + pw - 10;
      ctx.beginPath();
      ctx.moveTo(lerp((x0 + x1) / 2, x0, ease(kk)), y);
      ctx.lineTo(lerp((x0 + x1) / 2, x1, ease(kk)), y);
      ctx.stroke();
      ctx.restore();
    }

    // 문서 순서를 잃지 않도록, 왼쪽 바깥에 원래 줄 순서를 세로 눈금으로 남긴다
    if (kDoc > 0.05) {
      ctx.save(); ctx.globalAlpha = clamp(kDoc) * 0.9;
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(px + 4, slotTop); ctx.lineTo(px + 4, slotBot + plateH);
      ctx.stroke();
      ctx.restore();
    }
  },
};
