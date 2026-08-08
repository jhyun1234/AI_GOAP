import {
  disp, mono, ease, clamp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* nextpeek — 아웃트로 샷(4단 고정 구조 ④). 다음 편 기능의 0.5초 테스트 장면을 루프로 돌린다.

   무엇을 보여 주나 — 다음 편은 **`ep12s-2`** 다(정본 = `state/schedule.json` 의 `order`).
   그 편의 대본(`episodes/ep12s-2/scene.json`)과 같은 글 원문에 실재하는 것만 그린다:
   > "겨울이 끝나는 시점에 식량 잔량이 아슬아슬해야 하고, 30을 넘으면 \"안락 실패\"로 보고
   >  재조정하기로요." / "결과는 40이었습니다." / "재해까지 넣었는데도 겨울이 여전히
   >  편했던 겁니다."

   그래서 화면에서 도는 것은 이것 하나다 — **겨울 잔량 막대가 자라 실패선(FAIL 30)을
   지나쳐 40 에서 멈춘다.** 반 초마다 되풀이된다. 재해를 켜 뒀다는 사실은 아래
   `DISASTERS ON` 이 진다.

   🔴 **수치는 kind 상수가 아니라 전부 `spec` 으로 받는다.** 직전 편(ep11s)의 같은 자리에서
   `HEADS`/`KEEP` 를 파일 상수로 박아 둔 것이 검수 지적을 받았다. 눈금 위치(`failAt`·`resultAt`)
   도 마찬가지로 spec 이고, 두 값은 **30 : 40 의 비율을 그대로 옮긴 것**(0.60 : 0.80)이다.
   축의 최댓값은 화면에 안 적는다 — 적으면 원문에 없는 수가 된다.

   🔴 색을 이렇게 나눈 이유. 이 회차 문법에서 **흰색은 사람이 정해 둔 눈금**(실패선 · 트랙 ·
   라벨)이고 **강조색은 게임이 낸 값**(잔량 막대와 그 숫자)이다. S2 의 1인 몫 막대, S3 의
   결과 카드, S4 의 FoodDaysLeft 막대가 전부 그 규칙이었고 여기서도 그대로다.
   이번엔 **게임이 낸 값이 사람이 그은 선을 넘어간다** — 그게 다음 편의 사건이다.

   🔴 겹침은 **두 축으로** 잰다 — 강조색↔흰색(불투명 덮음)과 **흰색↔흰색(글자 뭉개짐)**.
      ⚠️ 초안은 앞의 것만 재고 뒤의 것을 안 쟀다가 반려됐다: `WINTER LEFTOVER`(baseline 66)와
      `FAIL 30`(baseline 74)이 **캔버스 간격 0.3px** 로 붙고 x 를 절반 넘게 공유해
      한 덩어리로 읽혔다(검수 스틸 픽셀 실측). 노출 시간 전부가 그 상태였다.
      🟢 **고침(2026-08-08):** 계기 이름을 `GAUGE_Y` 66 → **54** 로 올리고 **좌측정렬**해
      레인을 갈랐다 — 세로로 **11.7px**, 가로로는 x 범위가 아예 안 겹친다.

      흰색: 상단 라벨 y 24 · 구분선 y 36 · 계기 이름 y 44~54(**left, x16**) · FAIL 라벨
            y 66~74(center, fx≈207) · 실패선 표식 y 80~92 · 트랙 획 y 112~146 · 바닥 라벨 y 223~232.
      강조색: 잔량 막대 y 118~140(트랙 획 **안쪽 6px**) · 결과 숫자 글리프 y 175~200.
      · 흰↔흰 최소: 계기 이름 ↔ FAIL 라벨 **11.7px**(+ x 분리) · FAIL 라벨 ↔ 표식 6px
        (**같은 것을 가리키는 한 쌍**이라 붙는 것이 맞다) · 표식 ↔ 트랙 20px · 구분선 ↔ 계기 이름 8px.
      · 강조색↔흰색 최소: 표식 아래(92) ↔ 막대 후광 위(104) **12px** ·
        숫자 후광 아래(214) ↔ 바닥 라벨 글리프 위(223) **9px**.
      🔑 **막대가 실패선을 「지나친다」고 해서 그 위를 덮지는 않는다** — 표식은 트랙 바깥
      위쪽 레인에 있고 막대는 트랙 안에 있어 **x 만 공유하고 y 는 안 겹친다.**

   ⏱ 등장(cue) 과 루프(t) 를 갈랐다.
      - **등장**은 예고 자막 `cue(peekCue)` 에 물린다 — 자막이 부르기 전에는 아무것도 없다.
      - **루프**만 `t` 로 돈다(기본 0.5초, 사용자 지시의 「0.5초 미리보기」).
   🔴 소개 자막(cue 1)에는 아무것도 안 걸었다. 마지막 3.0초는 아웃트로 카드가 `.vis` 를
      픽셀 동일 좌표로 덮으므로 그 구간에 그린 것은 한 프레임도 안 보인다.
      루프는 `t` 로 도니 카드가 뜨기 전 구간이 정적이 될 일도 없다. */

const RULE_Y = 36;
/* 🔴 GAUGE_Y 는 66 에서 올린 값이다 — 66 이면 FAIL_LBL_Y 74 와 글리프가 맞닿는다(간격 0.3px).
   좌측정렬과 함께 써야 x 공유까지 없어진다. 둘 중 하나만 하면 반쪽짜리 수리다. */
const GAUGE_Y = 54, FAIL_LBL_Y = 74, TIP_T = 80, TIP_B = 92;
const TR_Y = 112, TR_H = 34;
const BAR_Y = 118, BAR_H = 22;
const NUM_Y = 200;
const FOOT_Y = 232;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    const cx = w / 2;

    const fitFont = (txt, weight, start, max, min = 8, fam = disp) => {
      let fs = start; ctx.font = fam(weight, fs);
      while (fs > min && ctx.measureText(txt).width > max) { fs -= 0.5; ctx.font = fam(weight, fs); }
      return fs;
    };

    const ak = ease(cue(spec.peekCue ?? 0, 0.15, 0.30));
    if (ak <= 0.02) { ctx.textAlign = 'left'; return; }

    /* ── 상단 라벨 + 구분선 : 왼쪽에서 오른쪽으로 열린다 ── */
    if (spec.label) {
      ctx.globalAlpha = ak;
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5);
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.label, 14, 24);
      ctx.globalAlpha = 1;
    }
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath();
    ctx.moveTo(16, RULE_Y); ctx.lineTo(16 + (w - 32) * ak, RULE_Y); ctx.stroke();

    const inner = clamp((ak - 0.35) / 0.65);
    if (inner <= 0.01) { ctx.textAlign = 'left'; return; }

    /* ── 0.5초 테스트 장면 ────────────────────────── */
    const loop = spec.loop ?? 0.5;
    const ph = frac(t / loop);
    const grow = ease(clamp(ph / 0.62));
    const back = ease(clamp((ph - 0.86) / 0.14));
    const k = clamp(grow - back);

    const IX = 22, IW = w - 44;
    const failAt = spec.failAt ?? 0.6;
    const resultAt = spec.resultAt ?? 0.8;
    const barW = IW * resultAt * k;
    const passed = clamp((resultAt * k - failAt) / Math.max(0.01, resultAt - failAt));

    const fx = IX + IW * failAt;

    /* ── 계기 이름 — **좌측정렬**. 아래 FAIL 라벨과 레인을 가른다 ──
       폭 상한도 FAIL 라벨의 왼쪽 끝 앞에서 끊어, 이름이 길어져도 x 가 절대 안 겹친다. */
    if (spec.gaugeLabel) {
      ctx.globalAlpha = inner;
      ctx.textAlign = 'left';
      const cap = Math.max(90, Math.min(w - 70, fx - 46));
      ctx.font = disp(800, fitFont(spec.gaugeLabel, 800, 12.5, cap, 9));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.gaugeLabel, 16, GAUGE_Y);
      ctx.globalAlpha = 1;
    }

    /* ── 사람이 그어 둔 실패선 — 트랙 바깥 위쪽 레인 ── */
    if (spec.failNumber) {
      const txt = `${spec.failLabel ?? 'FAIL'} ${spec.failNumber}`;
      ctx.globalAlpha = inner;
      ctx.textAlign = 'center';
      ctx.font = mono(700, fitFont(txt, 700, 12, 110, 8, mono));
      ctx.fillStyle = tone('sub');
      ctx.fillText(txt, Math.min(w - 44, Math.max(44, fx)), FAIL_LBL_Y);
      ctx.globalAlpha = 1;
    }
    ctx.save();
    ctx.globalAlpha = inner;
    ctx.fillStyle = tone('ink');
    ctx.beginPath();
    ctx.moveTo(fx - 7, TIP_T); ctx.lineTo(fx + 7, TIP_T); ctx.lineTo(fx, TIP_B);
    ctx.closePath(); ctx.fill();
    ctx.restore();

    /* ── 트랙(흰 획)과 그 안의 잔량 막대(강조색) ───── */
    ctx.globalAlpha = inner;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    roundRect(ctx, 16, TR_Y, w - 32, TR_H, 8); ctx.stroke();
    ctx.globalAlpha = 1;

    if (barW > 2) {
      ctx.save();
      ctx.globalAlpha = inner;
      setShadow(ctx, GLOW, 14);
      ctx.fillStyle = tone('accent');
      roundRect(ctx, IX, BAR_Y, barW, BAR_H, 5); ctx.fill();
      clearShadow(ctx);
      /* 막대 안 홈이 흐른다 — 글자가 없는 도형이라 클립해도 안전하다 */
      roundRect(ctx, IX, BAR_Y, barW, BAR_H, 5); ctx.clip();
      ctx.fillStyle = tone('bg');
      const off = (t * 21) % 22;
      for (let d = -22; d < barW + 22; d += 22) ctx.fillRect(IX + d + off, BAR_Y, 4, BAR_H);
      ctx.restore();
    }

    /* ── 실패선을 넘은 뒤에야 결과 숫자가 뜬다 ─────── */
    const res = String(spec.resultNumber ?? '');
    if (res && passed > 0.02) {
      const fs = fitFont(res, 900, 34, 120, 16);
      ctx.save();
      ctx.globalAlpha = inner * clamp(passed * 1.6);
      setShadow(ctx, GLOW, 14);
      ctx.textAlign = 'center';
      ctx.font = disp(900, fs);
      ctx.fillStyle = tone('accent');
      ctx.fillText(res, Math.min(w - 40, Math.max(40, IX + barW)), NUM_Y);
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 재해를 켜 둔 채였다 ───────────────────────── */
    if (spec.footLabel) {
      ctx.globalAlpha = inner;
      ctx.textAlign = 'center';
      ctx.font = mono(700, fitFont(spec.footLabel, 700, 11.5, w - 70, 8, mono));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.footLabel, cx, FOOT_Y);
      ctx.globalAlpha = 1;
    }

    ctx.textAlign = 'left';
  }
};
