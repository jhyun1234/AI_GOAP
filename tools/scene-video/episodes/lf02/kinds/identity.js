import { ease, clamp, lerp, tone, disp, mono, fitCanvas, mkCanvas, roundRect }
  from '../../../engine/lib.js';

/* identity — 규칙 문서 맨 위 두 줄이 판에 새겨진다.
   이 회차의 표기 규약대로 「무엇을 만드는 게임인가」는 흰 칸, 「무엇이 최우선인가」는
   강조 칸이다. 문자열은 Docs/CLAUDE.md **4행 · 5행** 그대로다.

   🔴 2차 개정 (검수 R1): 1차본은 라벨 `IDENTITY`(baseline rowY−16) 위에 23px 헤드라인
   (baseline rowY)이 얹혀 라벨이 판독 불가였다(증거 `build/stills/150s.png`).
   → **라벨을 헤드라인 왼쪽 칼럼으로 빼고**(가로 분리) 세로 간격을 28px 로 벌렸다.

   🔴 2차 개정 (검수 C8): 판이 자막을 그대로 복사했다(자막 5줄 중 4줄이 캔버스에 그대로).
   → ①본문 두 줄(「주민은 명령을 거부할 수 있다」·「기술적 완성도는 …」)을 판에서 **빼서**
   자막에만 남기고 ②판에는 자막에 없는 것을 넣었다 — **규칙 문서에서의 위치**(04행 · 05행)와
   **위계**(05 가 「최우선」이라 04 위에 선다는 TOP 표식). 이제 판은 자막이 말하지 않는
   「이게 문서 어디에 어떤 순서로 박혀 있는가」를 진다.

   🔴 5차 정정 (마스터 반려 M-R1): 그 행 번호가 **틀렸다.** 배지가 `03`·`06` 이었는데
   `Docs/CLAUDE.md` 실측은 **4행 = 게임 정체성 · 5행 = 최우선 가치**다
   (3행은 "Unity 탑다운 마을 생존 시뮬레이션…" 이라는 다른 문장이고 6행은 빈 줄).
   1차본 `source` 가 「정체성 3~4행 · 최우선 가치 6행」이라고 적은 것을 배지가 그대로
   옮겨 받은 것이 원인이다 — **범위 서술(3~6행)을 행 번호로 바꿀 때 다시 세지 않았다.**
   → `04`·`05` 로 정정. 🔑 부수 효과가 하나 더 있다: 자막 「그 아래 줄에는」이
   3과 6 사이에서는 거짓이었는데 **4 → 5 는 실제로 인접**이라 이제 참이 된다.
   🔴 배지를 지우는 선택지도 있었지만(마스터 권고 중 「가장 짧은 처방」) 남겼다 —
   이 배지는 2차 C8(「판이 자막을 복사한다」)을 닫으려고 넣은 **자막에 없는 정보**의
   절반이라, 지우면 그 판정이 다시 열린다. 정정은 두 글자이고 검증도 grep 한 줄이다.

   연속 모션 = 새김판을 지나는 각인 광택(52px 폭 × 판 높이). 글자가 아니라 판이 움직인다. */

const L1 = '자기 생각이 있는 AI와 협상하는 게임';
const L2 = '게임이 재밌어야 한다';

function fit(ctx, s, maxW, weight, start) {
  let fs = start;
  ctx.font = disp(weight, fs);
  while (ctx.measureText(s).width > maxW && fs > 10) { fs -= 1; ctx.font = disp(weight, fs); }
  return fs;
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const M = 16;

    const kPlate = ease(cue(0));   // 규칙 문서 맨 위에 한 줄로
    const kA = ease(cue(1));       // 정체성 줄
    const kB = ease(cue(2));       // 그 아래 줄
    const kTop = ease(cue(3));     // 최우선 가치 = 수단이 아니라 목적 판정

    const px = M, py = 34, pw = w - M * 2, ph = h - py - 26;

    // 연속 모션 : 판을 훑는 각인 광택 (판보다 먼저 — 강조색 위에 흰색을 얹지 않는다)
    {
      const PR = 3.8, k = (t % PR) / PR;
      const gx = lerp(px - 40, px + pw + 40, k);
      ctx.save();
      ctx.beginPath(); ctx.rect(px + 3, py + 3, pw - 6, ph - 6); ctx.clip();
      ctx.fillStyle = tone('track');
      ctx.fillRect(gx - 26, py + 3, 52, ph - 6);
      ctx.restore();
    }

    // 판
    ctx.globalAlpha = clamp(0.25 + 0.75 * kPlate);
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, px, py, pw, ph, 4); ctx.stroke();
    ctx.globalAlpha = 1;

    ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
    ctx.fillText('Docs/CLAUDE.md', M, 24);

    const tagX = px + 14, tagW = 36;
    const colX = px + 66;                 // 라벨·헤드라인 칼럼 (태그 오른쪽)
    const innerW = px + pw - 20 - colX;

    /* ── 행 하나 그리기 ─────────────────────────────
       라벨과 헤드라인은 **가로로 갈라 두지 않는다** — 라벨이 위(작게), 헤드라인이
       아래(크게)이되 baseline 간격을 28px 로 둬서 22px 글자가 라벨을 못 덮는다. */
    const row = (tagY, tagTxt, labelTxt, headTxt, k, accent, top) => {
      if (k <= 0.02) return;
      ctx.save(); ctx.globalAlpha = clamp(k);
      const col = accent ? tone('accent') : tone('ink');

      // 행 번호 태그
      ctx.strokeStyle = col; ctx.lineWidth = 3;
      roundRect(ctx, tagX, tagY, tagW, 24, 3); ctx.stroke();
      ctx.font = mono(700, 12); ctx.fillStyle = col;
      const tw = ctx.measureText(tagTxt).width;
      ctx.fillText(tagTxt, tagX + (tagW - tw) / 2, tagY + 17);

      // 라벨 (작게, 위)
      ctx.font = mono(700, 10); ctx.fillStyle = tone('sub');
      ctx.fillText(labelTxt, colX, tagY + 10);

      // 헤드라인 (크게, 28px 아래) — 왼쪽부터 새겨진다
      const fs = fit(ctx, headTxt, innerW, 900, 22);
      ctx.font = disp(900, fs);
      const hw = ctx.measureText(headTxt).width;
      ctx.save();
      ctx.beginPath(); ctx.rect(colX, tagY + 14, hw * ease(k), 30); ctx.clip();
      ctx.fillStyle = col;
      ctx.fillText(headTxt, colX, tagY + 38);
      ctx.restore();

      // 위계 표식 — 05 행만
      if (top) {
        const tk = clamp(kTop);
        ctx.save(); ctx.globalAlpha = clamp(tk);
        ctx.fillStyle = tone('accent');
        const ax = tagX + tagW / 2;
        ctx.beginPath();
        ctx.moveTo(ax, tagY - 16); ctx.lineTo(ax - 8, tagY - 5); ctx.lineTo(ax + 8, tagY - 5);
        ctx.closePath(); ctx.fill();
        ctx.font = mono(700, 9);
        ctx.fillText('TOP', tagX + tagW + 8, tagY - 6);
        ctx.restore();
      }
      ctx.restore();
    };

    row(py + 24, '04', 'IDENTITY', L1, kA, false, false);
    row(py + 108, '05', 'TOP VALUE', L2, kB, true, true);

    // 두 행을 잇는 순서 선 — 05 가 04 를 이긴다는 것을 선으로도 남긴다
    if (kTop > 0.05) {
      ctx.save(); ctx.globalAlpha = clamp(kTop) * 0.9;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      ctx.setLineDash([7, 6]);
      const lx = tagX + tagW / 2;
      ctx.beginPath();
      ctx.moveTo(lx, py + 24 + 24 + 6); ctx.lineTo(lx, py + 108 - 22);
      ctx.stroke();
      ctx.setLineDash([]);
      ctx.restore();
    }
  },
};
