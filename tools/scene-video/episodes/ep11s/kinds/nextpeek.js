import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* nextpeek — 아웃트로 샷(4단 고정 구조 ④). 다음 편 기능의 0.5초 테스트 장면을 루프로 돌린다.

   🔴 **2026-08-08 2차 개정 — 가리키는 편이 틀려서 통째로 다시 그렸다.**
   초안은 「강조색 재해가 구역 안의 밭 시설을 부순다」였는데, 그건 **ep12s-3 의 사건**이다.
   정본(`state/schedule.json` 의 `order`)에서 ep11s 의 다음 편은 **`ep12s-1`** 이고,
   그 편의 사건은 재해가 아니라 **재해를 내리기 전에 찾은 낡은 숫자**다.

   무엇을 보여 주나 — `episodes/ep12s-1/scene.json` 과 그 원문
   (`2026-07-26-unity-goap-m9-zone-disaster-flood.html`)에 실재하는 것만 그린다:
   > "겨울 대비 목표가 「조리식 20개」로 못 박혀 있었는데, 예전에 주민이 10명이던 시절에 잡은
   >  숫자였습니다. 그 뒤 주민을 4명으로 줄였는데도 20이라는 숫자는 그대로 남았습니다."
   > "10명이 먹을 양을 4명이 나눠 먹으니 겨울이 안락할 수밖에요."

   그래서 화면에서 도는 것은 이것 하나다 — **아래에서 주민이 10명에서 4명으로 줄어드는
   동안, 위의 WINTER GOAL 20 은 한 픽셀도 안 움직인다.** 반 초마다 되풀이된다.

   🔴 색을 이렇게 나눈 이유. 이 회차 문법에서 흰색은 **살아 움직이는 것**(주민 표식)이고
   강조색은 **값**이었다(품삯 · 빚). 여기서도 그대로다 — 줄어드는 주민이 흰색, 안 줄어드는
   숫자가 강조색이다. 4막의 문장(「흰 것이 화면에서 없어진다」)이 그대로 이어지고,
   본문 S1 에서 품삯이 빠지던 자리와 같은 말을 한다. 다만 이번엔 **없어져도 값이 안 따라온다.**
   🔑 숫자는 일부러 **완전히 정지**시켰다. 그게 이 편의 요지다. 대신 숫자를 두른 강조색
   점선 테두리가 계속 흐르게 해서 「잠겨 있다」는 뜻과 정적 방지를 함께 얻는다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — 행을 갈랐다.
      강조색: 숫자 테두리 `FY+45 … FY+111` · 밑줄 `FY+120`.
      중립(track) 가르는 선 `FY+134`. 흰색: 라벨 `FY+158` · 주민 표식 `FY+190 ± 8`.
      가장 가까운 쌍이 밑줄(FY+120)과 라벨(FY+158)이라 **38px** 떨어진다.

   🔴 화면에 올린 수는 **20** 하나이고 ep12s-1 원문에 실재한다. 주민 10명·4명은 **숫자로
      안 쓰고 표식 개수로만** 보인다 — 세면 나오는 것을 굳이 적지 않는다.

   ⏱ 등장(cue) 과 루프(t) 를 갈랐다.
      - **등장**은 예고 자막 `cue(0)` 에 물린다 — 자막이 부르기 전에는 아무것도 없다.
      - **루프**만 `t` 로 돈다(LOOP 0.5초, 사용자 지시의 「0.5초 미리보기」).
   🔴 소개 자막(cue 1)에는 아무것도 안 걸었다. 마지막 3.0초는 아웃트로 카드가 `.vis` 를
      픽셀 동일 좌표로 덮으므로 그 구간에 그린 것은 한 프레임도 안 보인다. */

const LOOP = 0.5;
const HEADS = 10;        // 줄기 전 주민 수 (원문 「주민이 10명이던 시절」)
const KEEP = 4;          // 줄인 뒤 남는 수 (원문 「주민을 4명으로 줄였는데도」)

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

    const FX = 18, FW = w - 36, FY = 40, FB = h - 46;
    const cx = w / 2;

    /* ── 상단 라벨 ────────────────────────────────── */
    if (spec.label) {
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.label, 12, 26);
    }

    /* ── 등장 : 네 모서리 괄호가 안쪽으로 들어온다 ── */
    const ak = ease(cue(spec.peekCue ?? 0, 0.15, 0.30));
    if (ak <= 0.02) { ctx.textAlign = 'left'; return; }

    const slide = lerp(14, 0, ak);
    const bx0 = FX - slide, bx1 = FX + FW + slide;
    const by0 = FY - slide, by1 = FB + slide;
    const arm = 26;
    ctx.save();
    ctx.globalAlpha = clamp(ak * 1.6);
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    const bracket = (x, y, sx, sy) => {
      ctx.beginPath();
      ctx.moveTo(x + sx * arm, y); ctx.lineTo(x, y); ctx.lineTo(x, y + sy * arm);
      ctx.stroke();
    };
    bracket(bx0, by0, 1, 1); bracket(bx1, by0, -1, 1);
    bracket(bx0, by1, 1, -1); bracket(bx1, by1, -1, -1);
    ctx.restore();

    if (ak < 0.5) { ctx.textAlign = 'left'; return; }
    const inner = clamp((ak - 0.5) / 0.5);

    /* ── 0.5초 테스트 장면 ────────────────────────── */
    const ph = frac(t / LOOP);
    const cut = ease(clamp(ph / 0.55));               // 10 → 4
    const back = ease(clamp((ph - 0.86) / 0.14));     // 루프가 되감긴다
    const gone = clamp(cut - back);

    /* ── 위 : 안 움직이는 기준 수 ─────────────────── */
    if (spec.goalLabel) {
      ctx.globalAlpha = inner;
      ctx.textAlign = 'center';
      ctx.font = disp(800, fit(spec.goalLabel, 800, 12, FW * 0.6, 9));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.goalLabel, cx, FY + 28);
      ctx.globalAlpha = 1;
    }

    const numText = String(spec.goalNumber ?? '');
    if (numText) {
      const nfs = fit(numText, 900, 52, FW * 0.5, 20);
      ctx.font = disp(900, nfs);
      const nw = ctx.measureText(numText).width;

      /* 잠긴 것을 두르는 점선 테두리 — 숫자는 안 움직이고 테두리만 흐른다 */
      ctx.save();
      ctx.globalAlpha = inner * 0.6;
      ctx.setLineDash([11, 8]);
      ctx.lineDashOffset = -(t * 22) % 19;
      ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3;
      const bw = Math.min(FW - 24, nw + 46);
      roundRect(ctx, cx - bw / 2, FY + 45, bw, 66, 10); ctx.stroke();
      ctx.restore();

      ctx.save();
      ctx.globalAlpha = inner;
      setShadow(ctx, GLOW, 14);
      ctx.textAlign = 'center';
      ctx.font = disp(900, nfs);
      ctx.fillStyle = tone('accent');
      ctx.fillText(numText, cx, FY + 96);
      clearShadow(ctx);
      /* 못 박힌 자리 — 이 밑줄도 루프 내내 길이가 안 변한다 */
      ctx.fillStyle = tone('accent');
      ctx.fillRect(cx - 42, FY + 120, 84, 5);
      ctx.restore();
    }

    /* ── 가르는 선 ────────────────────────────────── */
    ctx.save();
    ctx.globalAlpha = inner;
    ctx.setLineDash([8, 9]);
    ctx.lineDashOffset = (t * 13) % 17;
    ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(FX + 14, FY + 134); ctx.lineTo(FX + FW - 14, FY + 134); ctx.stroke();
    ctx.restore();

    /* ── 아래 : 줄어드는 주민 ─────────────────────── */
    if (spec.headLabel) {
      ctx.globalAlpha = inner;
      ctx.textAlign = 'left';
      ctx.font = disp(800, fit(spec.headLabel, 800, 12, FW * 0.6, 9));
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.headLabel, FX + 14, FY + 158);
      ctx.globalAlpha = 1;
    }

    const gap = Math.min(30, (FW - 44) / (HEADS - 1));
    const x0 = cx - gap * (HEADS - 1) / 2;
    const dy = FY + 190;
    for (let i = 0; i < HEADS; i++) {
      const x = x0 + i * gap;
      /* 오른쪽부터 차례로 꺼진다. i = KEEP..HEADS-1 만 꺼지고 왼쪽 넷은 끝까지 남는다. */
      let local = 0;
      if (i >= KEEP) {
        const k = (HEADS - 1 - i) / (HEADS - 1 - KEEP);   // 맨 오른쪽 0 → 맨 왼쪽 1
        local = clamp((gone - k * 0.45) / 0.55);
      }
      if (local < 0.98) {
        ctx.save();
        ctx.globalAlpha = inner * (1 - local);
        ctx.fillStyle = tone('ink');
        ctx.beginPath(); ctx.arc(x, dy, 8, 0, Math.PI * 2); ctx.fill();
        ctx.restore();
      }
      if (local > 0.02) {
        ctx.save();
        ctx.globalAlpha = inner * local * 0.75;
        ctx.setLineDash([5, 5]);
        ctx.lineDashOffset = (t * 9) % 10;
        ctx.strokeStyle = tone('track'); ctx.lineWidth = 3;
        ctx.beginPath(); ctx.arc(x, dy, 8, 0, Math.PI * 2); ctx.stroke();
        ctx.restore();
      }
    }

    ctx.textAlign = 'left';
  }
};
