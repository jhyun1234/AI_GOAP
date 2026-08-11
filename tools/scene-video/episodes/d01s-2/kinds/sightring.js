import {
  ease, clamp,
  fitCanvas, mkCanvas, roundRect, tone, disp, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* sightring — 규칙을 바꾸자 드러난 것. 이 편의 착지 그림이다.

   근거(원문 = `devlog/sessions/2026-08-11.md` 14:38 커밋 본문 · 「뒤집힌 것」 절):
   > "채택한 문법은 RTS 다 -- 지형은 기억으로 남고 유닛은 시야가 닿아야 보인다."
   > "핵심 발견: W5R 의 반경이 그대로 시야 마진이다. … 부화·회수 순간은 기하학적으로
   >  절대 화면에 안 보인다."
   > "믿었던 것: A2는 표현 수정이라 반경과 무관하다. 실제: W5R의 반경이 그대로 A2의
   >  정당성 증명이었다"
   → 화면에서 움직이는 것 = ①흰 원이 그려지자 **그 밖에 있던 강조색 표식이 꺼진다**
     ②더 바깥에 흰 점선 원이 생기고 **새 표식은 그 점선 위에서만 켜진다** — 안쪽
     흰 원과 한 번도 안 만난다.

   🔴 **수를 한 글자도 안 올렸다.** 원문에 반경 값(10·15·20)이 있지만 **같은 로그
   파일의 뒤쪽에서 사용자 판정으로 바뀌었다**(15:03 「주민 시야 10 -> 7」, 파생 부화
   15→10 · 회수 20→14). 화면에 박으면 하루 만에 낡는 수가 된다(lf02 교훈: 만드는
   사이에 화면이 낡는다). 게다가 원문이 쓴 사슬 「시야(10) < 부화(15) < 회수(20)−배회(5)」는
   20−5 = 15 라 **엄격 부등식이 아니다** — 숫자를 올리면 그 자리를 시청자가 붙잡는다.
   대신 **관계**만 그린다: 안쪽 원 < 바깥 원. 이 관계는 바뀐 값(7 < 10 < 14−5)에서도
   그대로 참이므로 낡지 않는다.

   🔴 새 표식을 **점선**으로 그린 것이 이 그림의 정직성이다 — 규칙상 시야 밖은
   화면에 안 보이므로, 「있지만 화면에는 안 나온다」를 점선이 진다.

   🔴 라벨을 다 가려도 읽힌다(ADR-V25-15): 원이 하나 서자 밖의 것이 꺼지고, 더 큰
   원이 생기자 새 것이 그 큰 원 쪽에서만 켜진다.

   ⏱ 전부 `since` 위에 있다(자막 길이가 바뀌어도 안 흔들린다).
     since(0) 0.15~0.75  땅 무늬가 또렷해진다        (「땅만 기억하고」)
     since(0) 1.45~2.05  흰 원이 한 바퀴 그려진다     (「움직이는 건 보일 때만」)
     since(0) 2.20~2.85  원 밖 강조색 표식 셋이 꺼진다 (「그리게 했죠」) ← latch 2,250ms
     since(1) 0.20~0.95  흰 점선 바깥 원이 그려진다
     since(1) 1.05~1.75  새 표식 둘이 점선 원 쪽에서 켜진다

   ── 겹침 감사 (선 반폭 2 · 숨 진폭 1.2 · GLOW blur 8 포함) ──
   중심 (176,168) · 안쪽 원 r 52 · 바깥 점선 원 r 92.
   축 A(강조색↔흰색) — 중심에서의 거리로 잰다:
     · 안쪽 원 바깥 면 52 + 2 + 1.2 = 55.2.
       꺼지는 표식 셋의 **가장 가까운 꼭짓점** = D1(176,90 밑변) 78.0 · D2(271,168) 95.0 ·
       D3(128,232 꼭대기) 80.0 → 최소 **22.8px**, GLOW 8 을 빼도 **14.8px**.
     · 바깥 점선 원 바깥 면 92 + 1.5 + 1.2 = 94.7.
       새 표식의 가장 가까운 꼭짓점 = TA(75,110) 116.5 · TB(283,218) 118.1
       → 최소 **21.8px**, GLOW 를 빼면 **13.8px**.
     · 흰 사람 표식(중심에서 최대 반지름 약 16.6) ↔ 안쪽 원 안쪽 면 48.8 → **32.2px**.
     ⓘ 바닥 격자·테두리(알파 0.12 `track`)는 **배경 텍스처**다 — 강조색 표식이 그 위에
       서는 것이 이 편 세 샷의 공통 문법이고(「기억 위에 개체까지 그려져」), 팔레트
       검사도 알파 64 미만은 안 센다. 위 값은 전부 **흰 실선**(원·사람 표식) 기준이다.
   축 A(강조색↔강조색, 시간으로 가르기): D1·D2·D3 과 TA·TB 는 자리가 가깝지만
     **같은 프레임에 함께 있지 않다.** 꺼짐이 끝나는 시각 = since(0) 2.85초이고,
     새 표식이 처음 알파 0.02 를 넘는 시각 = since(1) 1.05초 = 첫 줄이 끝난 뒤다.
     첫 줄은 19자라 **3.0초 아래로 짧아질 수 없으므로**(자당 190ms) 두 사건 사이에
     최소 **1.20초**가 비고, **겹치는 프레임은 0**이다.
   축 B(흰색↔흰색): 라벨 둘이 같은 밑선(292)에 있어 **x 로 갈랐다** —
     왼쪽은 x 20 에서 왼쪽 정렬(약 20~80), 오른쪽은 x 332 에서 오른쪽 정렬(약 260~332)
     → **180px**. 라벨 ↔ 흰 원: 안쪽 원 아래 끝 222.2, 라벨 글자 위 279.5 → **57.3px**.
     라벨 ↔ 흰 사람 표식: 세로로 **96px**.
     라벨 글자 위 279.5 ↔ 바닥 격자 아래 획 바깥 269.5 → **10px**(라벨과 배경이라 무해).

   세로 예산(캔버스 307): 최저 = 라벨 밑선 292(내림 약 296). 11px 남는다.
   가로(캔버스 352): 최좌 = TA 왼 끝 43.5 · 최우 = 오른쪽 라벨 끝 332.
   ⓘ 좌우 2px 띠·아래 한 줄에 닿는 것이 없다(잘림 검사).

   계속 도는 것: 안쪽 원 둘레 2π×52 = 327px 가 `bre = 1.2·sin(7.4t)` 로 숨 쉬고,
   가운데 표식이 `bob = 1.5·sin(6.8t)` 로 흔들린다 → 정적 구간이 안 생긴다. */

const CX = 176, CY = 168, R_SIGHT = 52, R_OUT = 92;
const TRI_H = 26, TRI_HW = 15;
const DIE = [[176, 90], [286, 168], [128, 258]];   // 꺼지는 표식 (밑변 중앙)
const NEW = [[60, 110], [298, 218]];               // 새로 켜지는 표식 (밑변 중앙)

function tri(ctx, cx, by) {
  ctx.beginPath();
  ctx.moveTo(cx, by - TRI_H);
  ctx.lineTo(cx + TRI_HW, by);
  ctx.lineTo(cx - TRI_HW, by);
  ctx.closePath();
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, since }) {
    const { ctx } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    ctx.lineJoin = 'round';

    const s0 = Math.max(0, since(spec.ruleCue ?? 0));
    const s1 = Math.max(0, since(spec.marginCue ?? 1));
    const bre = 1.2 * Math.sin(t * 7.4);
    const bob = 1.5 * Math.sin(t * 6.8);

    /* ── 다녀간 땅은 그대로 남는다 ── */
    const ga = ease(clamp((s0 - 0.15) / 0.60));
    if (ga > 0.02) {
      ctx.save();
      ctx.globalAlpha = ga;
      ctx.strokeStyle = tone('track');
      ctx.lineWidth = 3;
      roundRect(ctx, 26, 52, 300, 216, 10);
      ctx.stroke();
      ctx.clip();
      for (let i = -9; i < 13; i++) {
        const x0 = 26 + i * 26;
        ctx.beginPath();
        ctx.moveTo(x0, 52);
        ctx.lineTo(x0 + 216, 268);
        ctx.stroke();
      }
      ctx.restore();
    }

    /* ── 원 밖에 있던 것들 — 규칙이 걸리면서 꺼진다 ── */
    /* 🔴 바깥 가드는 **마지막 표식이 다 꺼지는 시각(2.85초)** 으로 잡는다.
       진행률로 잡으면 셋째가 알파 0.1 남은 채 통째로 잘려 뚝 끊긴다. */
    if (s0 < 2.90) {
      ctx.save();
      ctx.strokeStyle = tone('accent');
      ctx.lineWidth = 3;
      DIE.forEach(([cx, by], i) => {
        const a = 1 - ease(clamp((s0 - 2.20 - i * 0.10) / 0.45));
        if (a <= 0.01) return;
        ctx.globalAlpha = a;
        setShadow(ctx, GLOW, 8);
        tri(ctx, cx, by);
        ctx.stroke();
        clearShadow(ctx);
      });
      ctx.restore();
    }

    /* ── 보이는 데 = 흰 원. 한 바퀴 그어진다 ── */
    const sk = ease(clamp((s0 - 1.45) / 0.60));
    if (sk > 0.01) {
      ctx.save();
      ctx.strokeStyle = tone('ink');
      ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.arc(CX, CY, R_SIGHT + bre, -Math.PI / 2, -Math.PI / 2 + Math.PI * 2 * sk);
      ctx.stroke();
      ctx.restore();
    }

    /* ── 나타나는 데 = 더 바깥의 흰 점선 원 ── */
    const ok = ease(clamp((s1 - 0.20) / 0.75));
    if (ok > 0.01) {
      ctx.save();
      ctx.globalAlpha = ok;
      ctx.strokeStyle = tone('ink');
      ctx.lineWidth = 3;
      ctx.setLineDash([9, 9]);
      ctx.beginPath();
      ctx.arc(CX, CY, R_OUT, 0, Math.PI * 2);
      ctx.stroke();
      ctx.setLineDash([]);
      ctx.restore();
    }

    /* ── 새로 나타나는 것 — 점선이다. 있지만 화면엔 안 나온다 ── */
    ctx.save();
    ctx.strokeStyle = tone('accent');
    ctx.lineWidth = 3;
    ctx.setLineDash([7, 6]);
    NEW.forEach(([cx, by], i) => {
      const k = ease(clamp((s1 - 1.05 - i * 0.25) / 0.45));
      if (k <= 0.02) return;
      ctx.globalAlpha = k;
      setShadow(ctx, GLOW, 8);
      tri(ctx, cx, by);
      ctx.stroke();
      clearShadow(ctx);
    });
    ctx.setLineDash([]);
    ctx.restore();

    /* ── 가운데 = 보는 사람 ── */
    ctx.save();
    ctx.strokeStyle = tone('ink');
    ctx.lineWidth = 3;
    roundRect(ctx, CX - 6, CY - 15 + bob, 12, 30, 6);
    ctx.stroke();
    ctx.restore();

    ctx.font = disp(800, 12.5);
    ctx.fillStyle = tone('sub');
    if (spec.sightLabel && sk > 0.5) {
      ctx.save();
      ctx.globalAlpha = ease(clamp((s0 - 1.90) / 0.45));
      ctx.textAlign = 'left';
      ctx.fillText(spec.sightLabel, 20, 292);
      ctx.restore();
    }
    if (spec.outerLabel && ok > 0.5) {
      ctx.save();
      ctx.globalAlpha = ease(clamp((s1 - 0.60) / 0.45));
      ctx.textAlign = 'right';
      ctx.fillText(spec.outerLabel, 332, 292);
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
