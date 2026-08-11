import {
  ease, clamp, frac,
  fitCanvas, mkCanvas, roundRect, tone, disp, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* nextpeek — 아웃트로(4단 고정 구조 ④). 다음에 할 일의 0.5초 테스트 장면.

   🔴 dNNs 는 **다음 회차를 모른다.** 그래서 이 그림은 「다음 편」이 아니라
   **원문이 이월한 다음 항목**을 그린다(ADR-D-3). 재료는 패키지 ④ 안에서만:
   > "다음 항목 = W6 (위험 기억). 이제 당할 일이 생겼으므로 배울 거리가 있다."
   > "W6 (위험 기억) — W5R Play 확인 후."

   🔴 **기능 상세를 지어내지 않았다.** 로그는 「위험 기억」이 무엇인지 풀지 않았고,
   가진 문장은 위 둘뿐이다. 그래서 그릴 수 있는 층위 = **「당한 자리에 표가 남는다」**
   까지다. 무엇을 어떻게 배우는지(벌점·가중치·경로 회피)는 한 획도 안 그렸다.
   🔴 수도 한 글자 안 올렸다 — 패키지 ④ 문장 안에 수가 없다.

   0.5초 루프 : 표식 하나가 제자리에서 흔들리고(당한다) → 그 발밑에 강조색 표가
   찍혀 남는다 → 되감긴다. 땅 무늬는 앞 본문과 같은 배경이라 「같은 마을」이 읽힌다.

   🔴 본문의 도형(원 둘·삼각형)은 하나도 안 끌고 왔다 — 이 편이 「보이느냐」였다면
   다음 항목은 「기억하느냐」라 축이 다르다.

   ⏱ 등장은 예고 자막 `cue(0)` 에, 루프는 `t` 에 물렸다.
   🔴 소개 자막(cue 1)에는 아무것도 안 걸었다 — 마지막 3.0초는 아웃트로 카드가
      `.vis` 를 픽셀 동일 좌표로 덮으므로 그 구간에 그린 것은 한 프레임도 안 보인다.
      0.5초 루프는 **SO+1.05초에 알파 1.0** 에 닿고 카드까지 **2.37초 완전 노출**된다 —
      **총 6.8바퀴 · 완전히 도는 것 4.7바퀴**(검수 실측 4-4).
      ⚠️ 초고 주석의 「약 3.5초에 최소 7바퀴」는 낙관이었다. 뜻은 같지만 수를 고쳐 둔다.

   ── 겹침 감사 (선 반폭 1.5·2 · 흔들림 3 · GLOW blur 8 포함) ──
   축 A(강조색↔흰색): 강조색 표 위 꼭짓점 216 − 12 − 8(GLOW) = 196 /
     흰 표식 아래 끝 183 + 1.5 = 184.5 → **11.5px**. 좌우로는 흔들림 3px 를 더해도
     표식 x 165.5~186.5 · 표 x 164~188 로 **같은 세로축을 공유하는 의도한 쌍**이다
     (당한 자리 = 그 표식이 서 있던 자리).
   축 B(흰색↔흰색): 흰 것은 표식 하나와 라벨 하나뿐이고 라벨은 (20,26),
     표식은 y 153~183 이라 **127px** 떨어져 있다.
   배경 격자·테두리는 알파 0.12 `track` 이라 배경으로 둔다(본문 세 샷과 같은 문법).

   세로 예산(캔버스 307): 최저 = 격자 아래 획 바깥 245.5. 61px 남는다.
   가로(캔버스 352): 최좌 = 격자 왼 획 54.5 · 최우 = 격자 오른 획 297.5.

   계속 도는 것(30fps 기준): 흔들림이 6px 폭을 0.32초에 다섯 번 왕복 →
   약 6.3px/프레임. 표가 24px 를 0.15초에 커진다 → 5.3px/프레임. */

const BAND = { x: 56, y: 128, w: 240, h: 116 };
const MARK_Y = 168, HIT_Y = 216;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    ctx.lineJoin = 'round';

    if (spec.label) {
      ctx.save();
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5);
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.label, 20, 26);
      ctx.restore();
    }

    const pk = ease(cue(spec.peekCue ?? 0, 0.15, 0.30));
    if (pk <= 0.02) { ctx.textAlign = 'left'; return; }

    const bre = 1.2 * (1 + Math.sin(t * 10.8));
    const B = BAND;

    ctx.save();
    ctx.globalAlpha = pk;
    ctx.strokeStyle = tone('track');
    ctx.lineWidth = 3;
    roundRect(ctx, B.x + bre, B.y + bre, B.w - bre * 2, B.h - bre * 2, 8);
    ctx.stroke();
    ctx.clip();
    for (let i = -5; i < 12; i++) {
      const x0 = B.x + i * 26;
      ctx.beginPath();
      ctx.moveTo(x0, B.y);
      ctx.lineTo(x0 + B.h, B.y + B.h);
      ctx.stroke();
    }
    ctx.restore();

    const u = frac(t / (spec.loopSec ?? 0.5));
    const vis = ease(clamp(u / 0.06)) * (1 - ease(clamp((u - 0.86) / 0.14)));
    const hit = ease(clamp((u - 0.10) / 0.02)) * (1 - ease(clamp((u - 0.36) / 0.06)));
    const mark = ease(clamp((u - 0.45) / 0.30));

    /* ── 당한다 ── */
    const sh = 3 * hit * Math.sin(u * 96);
    ctx.save();
    ctx.globalAlpha = pk * vis;
    ctx.strokeStyle = tone('ink');
    ctx.lineWidth = 3;
    roundRect(ctx, 176 - 6 + sh, MARK_Y - 15, 12, 30, 6);
    ctx.stroke();
    ctx.restore();

    /* ── 그 자리에 표가 남는다 ── */
    if (mark > 0.02) {
      const r = 12 * mark;
      ctx.save();
      ctx.globalAlpha = pk * vis;
      setShadow(ctx, GLOW, 8);
      ctx.strokeStyle = tone('accent');
      ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.moveTo(176, HIT_Y - r);
      ctx.lineTo(176 + r, HIT_Y);
      ctx.lineTo(176, HIT_Y + r);
      ctx.lineTo(176 - r, HIT_Y);
      ctx.closePath();
      ctx.stroke();
      clearShadow(ctx);
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
