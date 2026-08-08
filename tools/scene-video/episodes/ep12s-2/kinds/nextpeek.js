import {
  disp, mono, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* nextpeek — 아웃트로 샷(4단 고정 구조 ④). **다음 편 기능의 0.5초 테스트 장면**을 루프로 돌린다.

   무엇을 보여 주나 — 다음 편은 `ep12s-3` 이고(정본 = state/schedule.json 의 order, 그리고
   episodes/ep12s-3/scene.json 이 실물로 있다), 그 편이 여는 사건은 이것 하나다:
   > "마을에 홍수를 내렸어요. 작물 쓸어가라고요." / "근데 로그는 작물 0개 소실. 다시 돌려도 0개."
   화면 문자열도 그 편에서 빌려 왔다 — `FLOOD`(그 편 spec.floodLabel) ·
   `CROPS LOST`(그 편 spec.logLabel 「FLOOD: CROPS LOST」) · `0`(원문 '홍수 발동 — 작물 0개 소실').
   🔴 수치는 하드코딩하지 않고 **전부 spec 경유**다(`lostNumber`) — 다음 편이 바뀌면 이
   파일이 아니라 씬 JSON 이 바뀐다.

   반 초마다 되풀이되는 것은 이것 하나다 —
   **강조색 물띠가 밭 위를 훑고 지나가는데, 아래 흰 밭도 로그 줄의 0 도 한 픽셀 안 변한다.**
   이 회차 본문이 「때렸는데 값이 안 움직인다」를 계속 그렸으므로(재조정을 넷이나 걸고도
   기둥이 안 내려왔다) 미리보기도 같은 문장으로 다음 편을 연다. 때린 것이 재조정이 아니라
   **재해**라는 것만 다르고, 그게 다음 편의 사건이다.

   🔴 **배치를 「위에 큰 수 · 아래에 움직이는 것」으로 짜지 않았다.** 그 배치는 다른 회차의
   아웃트로가 이미 쓴 모양이라, 같은 뜻을 내더라도 그림이 겹친다. 여기서는 **위가 무대**
   (물띠 + 밭)이고 **아래가 로그 한 줄**이다 — 콘솔에서 결과를 읽는 모양이라
   「돌려봤는데 로그가 0 이더라」는 다음 편의 첫 자막과도 같은 몸짓이다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다.
      강조색: 물띠 104~132(잔물결로 101 까지) · `FLOOD` 라벨 baseline 96 · 로그 수 baseline 262.
      흰색:   `NEXT` 26 · `FIELD` 라벨 150 · 밭 띠 158~196 · `CROPS LOST` 라벨 262(왼쪽).
      ⚠️ 세로로 가장 가까운 쌍이 물띠 아래끝(132)과 `FIELD` 글자 위끝(~141)이라 9px 이다 —
      **그래서 물띠에는 글로우를 안 걸었다.** 걸었으면 그 9px 이 통째로 혼합 픽셀이 된다.
      글로우를 쓰는 것은 로그의 강조색 수 하나뿐이고(blur 14 → 위로 ~217), 그 위 최근접
      흰 것인 밭 띠 아래끝(196)과 **21px** 떨어져 있다.
      ⚠️ 로그 줄은 흰 라벨과 강조색 수가 **같은 baseline** 이지만 좌우로 갈라 놓았다 —
      라벨은 왼쪽 정렬, 수는 오른쪽 정렬이고 사이를 잇는 점선은 중립(track)이다.

   ⏱ 등장(cue) 과 루프(t) 를 갈랐다.
      - **등장**은 예고 자막 `cue(0)` 에 물린다 — 자막이 부르기 전에는 아무것도 없다.
      - **루프**만 `t` 로 돈다(LOOP 0.5초, 사용자 지시의 「0.5초 미리보기」).
   🔴 소개 자막(cue 1)에는 아무것도 안 걸었다. 마지막 3.0초는 아웃트로 카드가 `.vis` 를
      픽셀 동일 좌표로 덮으므로 그 구간에 그린 것은 한 프레임도 안 보인다.
   🔴 밭 고랑은 **안 흐르게** 뒀고 로그 수도 완전히 정지시켰다. 둘 중 하나라도 움직이면
      「아무 일도 안 일어난다」가 깨진다 — 정적 방지는 화면 폭을 도는 물띠와 테두리 점선이 진다. */

const LOOP = 0.5;
const WAVE_T = 104, WAVE_B = 132;
const FIELD_LABEL_Y = 150;
const FIELD_T = 158, FIELD_B = 196;
const LOG_Y = 262;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w, h } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const fit = (txt, weight, start, max, min = 8) => {
      let fs = start; ctx.font = disp(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = disp(weight, fs); }
      return fs;
    };

    const FX = 16, FW = w - 32, FT = 40, FB = h - 14;

    /* ── 구획 이름 ────────────────────────────────── */
    if (spec.label) {
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.label, 12, 26);
    }

    /* ── 등장 : 테두리가 위아래에서 닫힌다 ─────────── */
    const ak = ease(cue(spec.peekCue ?? 0, 0.15, 0.30));
    if (ak <= 0.02) { ctx.textAlign = 'left'; return; }

    const open = lerp(26, 0, ak);
    ctx.save();
    ctx.globalAlpha = clamp(ak * 1.6);
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.setLineDash([12, 9]);
    ctx.lineDashOffset = (t * 20) % 21;
    roundRect(ctx, FX, FT + open, FW, (FB - FT) - open * 2, 12);
    ctx.stroke();
    ctx.restore();

    if (ak < 0.5) { ctx.textAlign = 'left'; return; }
    const inner = clamp((ak - 0.5) / 0.5);

    /* ── 0.5초 테스트 장면 : 물띠가 한 번 지나간다 ──── */
    const ph = frac(t / LOOP);
    const wx = lerp(-88, w + 88, ph);
    const HALF = 82;
    /* 루프 양 끝에서 알파로 들고 난다 — 창 경계에서 툭 끊기지 않게 */
    const waveA = ease(clamp(ph / 0.12)) * ease(clamp((1 - ph) / 0.12));

    /* ── 무대 위 : 홍수 (강조색, 글로우 없음) ───────── */
    /* 🔴 **미리보기 창 안쪽으로 클립한다** (2026-08-08 반려 수정).
       물띠는 x 가 −88 → w+88 로 지나가므로 클립이 없으면 **캔버스 좌우 가장자리 띠
       (바깥 2px)에 잉크를 남긴다** — check.mjs 의 「좌우 가장자리 잘림 없음」이
       `SO 53% 구간 · 최대 166px` 로 잡았다. `checks.edge` 예외를 선언하는 게 아니라
       그리는 범위를 창 안으로 가둔다(형제 편 `ep12s-3` 이 같은 문제를 푼 방식).
       클립 사각형은 **애니메이션 중인 테두리와 같은 사각형**을 3px 안쪽으로 줄인 것이라
       테두리 획(3px)이 덮이지 않는다.
       ⚠️ 이 회차가 지켜 온 「클립 두 장을 맞대어 내용을 가르지 않는다」는 그대로다 —
       여기 클립은 **한 장이고 담는 용도**이며, 경계에 맞닿는 다른 클립이 없다. */
    ctx.save();
    ctx.beginPath();
    roundRect(ctx, FX + 3, FT + open + 3, FW - 6, (FB - FT) - open * 2 - 6, 9);
    ctx.clip();
    ctx.globalAlpha = inner * waveA;
    ctx.fillStyle = tone('accent');
    ctx.beginPath();
    ctx.moveTo(wx - HALF, WAVE_B);
    for (let i = 0; i <= 26; i++) {
      const k = i / 26;
      const x = wx - HALF + HALF * 2 * k;
      const crest = Math.sin(k * Math.PI);
      const ripple = Math.sin(k * Math.PI * 3 + t * 9) * 3;
      ctx.lineTo(x, WAVE_B - (WAVE_B - WAVE_T) * crest + ripple);
    }
    ctx.lineTo(wx + HALF, WAVE_B);
    ctx.closePath();
    ctx.fill();
    if (spec.floodLabel) {
      ctx.textAlign = 'center';
      ctx.font = disp(900, 13);
      ctx.fillStyle = tone('accent');
      ctx.fillText(spec.floodLabel, clamp(wx, FX + 34, FX + FW - 34), 96);
    }
    ctx.restore();

    /* ── 무대 아래 : 밭 — 한 픽셀도 안 변한다 ───────── */
    if (spec.fieldLabel) {
      ctx.globalAlpha = inner;
      ctx.textAlign = 'left';
      ctx.font = disp(800, fit(spec.fieldLabel, 800, 12, FW * 0.5, 9));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.fieldLabel, FX + 26, FIELD_LABEL_Y);
      ctx.globalAlpha = 1;
    }
    ctx.save();
    ctx.globalAlpha = inner;
    const bx = FX + 24, bw = FW - 48;
    ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3;
    roundRect(ctx, bx, FIELD_T, bw, FIELD_B - FIELD_T, 6);
    ctx.stroke();
    /* 고랑 — 비어 있다. 흐르지 않는다(점선 오프셋에 t 가 안 들어간다). */
    ctx.strokeStyle = tone('sub'); ctx.lineWidth = 3;
    ctx.setLineDash([6, 7]);
    for (let i = 1; i <= 3; i++) {
      const x = bx + (bw / 4) * i;
      ctx.beginPath();
      ctx.moveTo(x, FIELD_T + 7); ctx.lineTo(x, FIELD_B - 7);
      ctx.stroke();
    }
    ctx.restore();

    /* ── 로그 한 줄 — 왼쪽에 이름(흰색), 오른쪽에 수(강조색) ── */
    const numText = String(spec.lostNumber ?? '');
    let numLeft = FX + FW - 26;
    if (numText) {
      ctx.save();
      ctx.globalAlpha = inner;
      setShadow(ctx, GLOW, 14);
      ctx.textAlign = 'right';
      const nfs = fit(numText, 900, 42, FW * 0.35, 20);
      ctx.font = disp(900, nfs);
      numLeft = FX + FW - 26 - ctx.measureText(numText).width;
      ctx.fillStyle = tone('accent');
      ctx.fillText(numText, FX + FW - 26, LOG_Y);
      clearShadow(ctx);
      ctx.restore();
    }
    if (spec.lostLabel) {
      ctx.save();
      ctx.globalAlpha = inner;
      ctx.textAlign = 'left';
      ctx.font = mono(700, fit(spec.lostLabel, 700, 13, FW * 0.55, 9));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.lostLabel, FX + 26, LOG_Y);
      const lw = ctx.measureText(spec.lostLabel).width;
      /* 잇는 점선은 중립색이라 어느 쪽과도 안 섞인다 */
      const a = FX + 26 + lw + 12, b = numLeft - 16;
      if (b - a > 20) {
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.setLineDash([4, 9]);
        ctx.beginPath();
        ctx.moveTo(a, LOG_Y - 5); ctx.lineTo(b, LOG_Y - 5);
        ctx.stroke();
      }
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
