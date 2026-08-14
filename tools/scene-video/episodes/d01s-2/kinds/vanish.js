import {
  ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, disp, setShadow, clearShadow, GLOW, FAIL_GLOW
} from '../../../engine/lib.js';

/* vanish — 훅(4단 고정 구조 ②). 「눈앞의 증발」 그 장면 하나만 되풀이한다.

   근거(원문 = `devlog/sessions/2026-08-11.md` 14:38 커밋 본문):
   > "지형 기억(탐험됨) 위에 개체까지 그려져, 어두운 기억 지대에서 회수가 눈앞의
   >  증발로 보였다"
   → 화면에서 움직이는 것 = **땅은 그 자리에 그대로 있는데 그 위에 서 있던 무리만
     한꺼번에 꺼진다.**

   🔴 이 편은 형제 편(`d01s-1`)이 지는 몫을 한 조각도 안 그린다 — 배너·명령·죽음은
   여기 없다. 이 그림에 있는 것은 「땅 / 무리 / 멀어지는 사람」 셋뿐이다.

   🔴 라벨을 다 가려도 읽혀야 한다(ADR-V25-15): 격자 무늬 땅은 처음부터 끝까지 그
   자리에 있고, 그 위의 강조색 표식 셋만 사람이 멀어지는 순간 통째로 꺼진다.
   「자리는 남고 위에 있던 것만 없어진다」에 낱말이 하나도 안 든다.

   ⏱ 2.4초 루프이고 전부 `t` 위에 있다 — `cue` 를 안 쓴다.
   🔴 그래서 이 샷에는 효과음을 안 달았다(루프에 한 번만 내면 두 번째 바퀴부터
      그림과 어긋난다). 소리는 한 번만 일어나는 샷(S1·S3)에만 있다.

   ── 겹침 감사 (선 반폭 1.5 · 숨 진폭 1.2 · GLOW blur 8 포함) ──
   축 A(강조색↔흰색):
     · 강조색 삼각형 왼쪽 끝 153 − 1.5 = 151.5 / 걷기 시작점의 흰 표식 오른쪽 끝
       120 + 6 + 1.5 = 127.5 → **24.0px**, GLOW 8 을 빼도 **16.0px**.
     · 삼각형 위 끝 130 − 1.5 = 128.5 / 기억 지대 위 획 안쪽 104 + 1.5 + 1.2 = 106.7
       → **21.8px**, GLOW 를 빼면 **13.8px**.
     · 삼각형 아래 끝 188 + 1.5 = 189.5 / 지대 아래 획 안쪽 232 − 1.5 − 1.2 = 229.3
       → **39.8px**.
     ⓘ 지대 **안쪽 빗금**(알파 0.12)만은 강조색 아래를 지난다 — 그것이 이 그림의
       사건이다(「기억 위에 개체까지 그려져」). 빗금은 배경 텍스처이고 팔레트 검사도
       알파 64 미만이라 세지 않는다. 흰 **실선**(표식·지대 테두리)과는 위 값대로 뜬다.
   축 B(흰색↔흰색): 흰 것은 「사람 표식」과 「지대 테두리(track)」 둘뿐이고, 사람이
     테두리를 넘어 나가는 것이 이 그림의 동작이라 **의도한 쌍**이다. 서로 다른 것을
     가리키는 흰 글자 쌍은 이 샷에 없다(라벨이 하나뿐).

   세로 예산(캔버스 307): 최저 = 지대 아래 획 바깥 233.5. 73px 남는다.
   가로(캔버스 352): 최좌 = 걷기 끝점 44 − 7.5 = 36.5 · 최우 = 지대 오른 획 325.5.

   계속 도는 것(30fps 기준): 지대 둘레 2×(232+128) = 720px 가 `bre = 1.2·(1+sin(9.6t))`
   로 숨 쉰다 → 0.384px/프레임 → 약 276px². 걷기는 76px 를 0.86초에 → 2.9px/프레임. */

const PATCH = { x: 92, y: 104, w: 232, h: 128 };
const BAND = [[168, 158], [212, 188], [254, 156]];   // 삼각형 밑변 중앙
const TRI_H = 26, TRI_HW = 15;

function tri(ctx, cx, by) {
  ctx.beginPath();
  ctx.moveTo(cx, by - TRI_H);
  ctx.lineTo(cx + TRI_HW, by);
  ctx.lineTo(cx - TRI_HW, by);
  ctx.closePath();
}

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t }) {
    const { ctx } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';
    ctx.lineJoin = 'round';

    const bre = 1.2 * (1 + Math.sin(t * 9.6));

    if (spec.groundLabel) {
      ctx.save();
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5);
      ctx.fillStyle = tone('sub');
      ctx.fillText(spec.groundLabel, 20, 26);
      ctx.restore();
    }

    /* ── 다녀간 땅 = 기억 지대. 처음부터 끝까지 안 없어진다 ── */
    const P = PATCH;
    ctx.save();
    ctx.strokeStyle = tone('track');
    ctx.lineWidth = 3;
    roundRect(ctx, P.x + bre, P.y + bre, P.w - bre * 2, P.h - bre * 2, 8);
    ctx.stroke();
    ctx.clip();
    for (let i = -7; i < 13; i++) {
      const x0 = P.x + i * 22;
      ctx.beginPath();
      ctx.moveTo(x0, P.y);
      ctx.lineTo(x0 + P.h, P.y + P.h);
      ctx.stroke();
    }
    ctx.restore();

    /* ── 0.5바퀴 = 사람이 멀어진다 · 그다음 무리가 통째로 꺼진다 ── */
    const u = frac(t / (spec.loopSec ?? 2.4));
    const vis = ease(clamp(u / 0.06)) * (1 - ease(clamp((u - 0.86) / 0.14)));
    const walk = ease(clamp((u - 0.08) / 0.36));
    const gone = ease(clamp((u - 0.50) / 0.10));

    const ba = vis * (1 - gone);
    if (ba > 0.01) {
      ctx.save();
      ctx.globalAlpha = ba;
      setShadow(ctx, FAIL_GLOW, 8);
      ctx.strokeStyle = tone('fail');
      ctx.lineWidth = 3;
      for (const [cx, by] of BAND) { tri(ctx, cx, by); ctx.stroke(); }
      clearShadow(ctx);
      ctx.restore();
    }

    const vx = lerp(120, 44, walk);
    ctx.save();
    ctx.globalAlpha = vis;
    ctx.strokeStyle = tone('ink');
    ctx.lineWidth = 3;
    roundRect(ctx, vx - 6, 153, 12, 30, 6);
    ctx.stroke();
    ctx.restore();

    ctx.textAlign = 'left';
  }
};
