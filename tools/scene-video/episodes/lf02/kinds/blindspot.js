import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* blindspot — 스크린샷 한 장 안에 칸이 넷 있는데, 확인하려고 정해 둔 칸에만 초점
   테두리가 돈다. 바로 옆 칸에는 두 줄로 늘어난 목록과 겹친 글자가 처음부터 찍혀 있지만
   끝까지 테두리 밖이다.
   🔴 이 그림의 핵심은 "결함이 나중에 생기지 않는다"는 것이다 — 첫 프레임부터 있다.
   연속 모션 = 정해 둔 칸만 도는 초점 테두리. 사람의 확인은 계속 도는데 범위가 안 는다.
   문자열 출처: published/2026-08-08-unity-goap-m15-chronicle-archive.html 67행. */

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    const kBelief = ease(cue(0));  // 마지막 심판이라 믿었는데
    const kOnly = ease(cue(1));    // 마음먹은 것만 본다
    const kPass = ease(cue(2));    // 열렸으니 통과라고 적었다
    const kAlso = ease(cue(3));    // 같은 스크린샷 안에 이미

    // ── 스크린샷 액자 ──────────────────────────────
    const fx = M, fy = 38, fw = w - M * 2, fh = h - fy - 42;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, fx, fy, fw, fh, 4); ctx.stroke();
    ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('스크린샷 한 장', M, 28);

    const pad = 14;
    const cw = (fw - pad * 3) / 2, ch = (fh - pad * 3) / 2;
    const cells = [
      [fx + pad, fy + pad],
      [fx + pad * 2 + cw, fy + pad],
      [fx + pad, fy + pad * 2 + ch],
      [fx + pad * 2 + cw, fy + pad * 2 + ch],
    ];

    // 칸 내용 — 결함은 첫 프레임부터 있다
    for (let i = 0; i < 4; i++) {
      const [cx, cy] = cells[i];
      ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
      roundRect(ctx, cx, cy, cw, ch, 3); ctx.stroke();

      if (i === 0) {
        // 확인하려고 간 칸 : 패널이 열린다
        ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
        ctx.fillText('패널', cx + 10, cy + 18);
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
        roundRect(ctx, cx + 10, cy + 26, cw - 20, ch - 40, 3); ctx.stroke();
        if (kPass > 0.2) {
          ctx.font = disp(900, 15); ctx.fillStyle = tone('accent');
          const s = '열림';
          ctx.fillText(s, cx + 22, cy + ch - 22);
        }
      } else if (i === 1) {
        // 이미 어긋나 있던 칸 : 두 줄로 늘어난 목록
        ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
        ctx.fillText('목록', cx + 10, cy + 18);
        for (let j = 0; j < 3; j++) {
          const ly = cy + 28 + j * 13;
          ctx.fillStyle = tone('sub');
          ctx.fillRect(cx + 10, ly, cw - 24, 5);
          if (j === 1) ctx.fillRect(cx + 10, ly + 7, cw - 60, 5);   // 두 줄로 늘어난 항목
        }
      } else if (i === 2) {
        // 겹친 글자
        ctx.font = disp(800, 15); ctx.fillStyle = tone('sub');
        const a = '겹친 글자';
        ctx.fillText(a, cx + 12, cy + ch / 2 + 6);
        ctx.fillStyle = tone('ink');
        ctx.fillText(a, cx + 20, cy + ch / 2 + 9);
      } else {
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        for (let j = 0; j < 2; j++) {
          ctx.beginPath();
          ctx.moveTo(cx + 12, cy + 24 + j * 16); ctx.lineTo(cx + cw - 16, cy + 24 + j * 16);
          ctx.stroke();
        }
      }
    }

    // ── 연속 모션 : 정해 둔 칸만 도는 초점 테두리 ────
    {
      const [cx, cy] = cells[0];
      const PR = 2.6, k = (t % PR) / PR;
      const per = 2 * (cw + ch);
      let d = k * per, px, py;
      if (d < cw) { px = cx + d; py = cy; }
      else if ((d -= cw) < ch) { px = cx + cw; py = cy + d; }
      else if ((d -= ch) < cw) { px = cx + cw - d; py = cy + ch; }
      else { d -= cw; px = cx; py = cy + ch - d; }
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 4;
      roundRect(ctx, cx - 5, cy - 5, cw + 10, ch + 10, 4); ctx.stroke();
      ctx.fillStyle = tone('ink');
      ctx.beginPath(); ctx.arc(px, py, 5, 0, Math.PI * 2); ctx.fill();
      /* 🔴 3차 개정 (검수 N4): baseline 이 `cy - 12`(= 40) 이라 액자 윗선(y 38,
         lineWidth 3 → 36.5~39.5)이 글자 **아래 절반을 그어 지웠다.** 같은 흰색이라
         잘림·팔레트 게이트가 구조적으로 못 본다(증거 `build/stills/709s.png`).
         → 액자 **밖 머리줄**(`fy - 10` = 28, `ONE SCREENSHOT` 과 같은 줄)로 올린다.
         x 는 칸 가로 중앙이라 `ONE SCREENSHOT`(x 16~99)과 겹치지 않고,
         엔진 HUD 챕터 칩 자리(x ≳ 490)에도 닿지 않는다. */
      ctx.font = disp(700, 10); ctx.fillStyle = tone('ink');
      ctx.fillText('확인하러 간 곳', cx + cw * 0.5, fy - 10);
    }

    // 나머지 칸은 끝까지 테두리 밖 — 그 사실을 kAlso 에서 짚는다
    if (kAlso > 0.05) {
      ctx.save(); ctx.globalAlpha = clamp(kAlso);
      ctx.setLineDash([6, 6]);
      ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
      for (const i of [1, 2]) {
        const [cx, cy] = cells[i];
        roundRect(ctx, cx - 4, cy - 4, cw + 8, ch + 8, 4); ctx.stroke();
      }
      ctx.setLineDash([]);
      ctx.font = disp(700, 10); ctx.fillStyle = tone('sub');
      const s = '이미 찍혀 있던 것';
      /* 3차 개정 (N4): `cells[1][1] - 11`(= 41) 도 같은 액자선에 잘렸다 → 머리줄로.
         🔴 v2 한국어화로 두 머리 라벨이 길어졌다 — 「확인하러 간 곳」의 오른쪽 끝과
         이 문자열의 시작이 겹치지 않도록 x 를 폭으로 재서 오른쪽으로 민다. */
      const leftEnd = cells[0][0] + cw * 0.5 + ctx.measureText('확인하러 간 곳').width;
      ctx.fillText(s, Math.max(cells[1][0] - 4, leftEnd + 14), fy - 10);
      ctx.restore();
    }

    // 판정
    if (kBelief > 0.05 || kOnly > 0.05) {
      ctx.save();
      ctx.globalAlpha = clamp(Math.max(kBelief, kOnly));
      ctx.font = disp(900, 15); ctx.fillStyle = tone('ink');
      const s = kOnly > 0.3 ? '마음먹은 것만 본다' : '사람의 눈은 마지막 심판이 아니었다';
      const sw = ctx.measureText(s).width;
      ctx.fillText(s, Math.min(M, w - M - sw), h - 12);
      ctx.restore();
    }
  },
};
