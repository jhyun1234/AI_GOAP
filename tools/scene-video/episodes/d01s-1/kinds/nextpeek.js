import {
  disp, ease, clamp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* nextpeek — 아웃트로(4단 고정 구조 ④). 다음 편에서 다룰 장면을 0.5초 루프로 돌린다.

   🔴 재료는 **같은 원문의 뒤쪽 사건**에서 뽑았다 — `devlog/sessions/2026-08-11.md`
   14:38 W5R-A2 커밋 본문:
   > "예약제가 낳은 표현 결함을 사용자가 잡았다: 지형 기억(탐험됨) 위에 개체까지 그려져,
   >  어두운 기억 지대에서 회수가 **눈앞의 증발**로 보였다 -- '아 이 사람 주민 근처에
   >  다가오면 적을 없애게 해뒀구나.'"
   > "채택한 문법은 RTS 다 -- **지형은 기억으로 남고 유닛은 시야가 닿아야 보인다.**"
   → 화면에서 움직이는 것 = **땅 모양은 그대로 남고, 그 위에 있던 것만 사라진다.**

   🔴 **원인(시야)도 반경도 안 그렸다 — 그건 `d01s-2` 의 고유물이다.** 여기서 그리는
   것은 사용자가 본 그대로의 **증발**뿐이고, 그래서 예고 음성(「적이 눈앞에서 사라진
   이야기예요」)·`outro.next` 카드·이 그림 **셋이 같은 것 하나**를 가리킨다.
   🔴 **수를 글자로 한 개도 안 적었다.** 그 편이 소유한 수(시야 10 · 부화 15 · 회수 20 ·
   배회 5 · 3단계)를 끌어오면 그 수가 이 편의 사건인 것처럼 읽히고, 게다가 그중 셋은
   같은 날 뒤쪽 커밋(15:03 · 시야 10→7)에서 값이 바뀌었다. 미리보기가 지는 것은 **동작**이다.

   🔴 본문의 도형(마을 네모 · 무리 · 잡아 둔 자리)을 통째로 끌고 오지 않았다 — 여기
   남는 것은 **땅 칸과 그 위의 표식**뿐이고, 이 편이 「없는 것은 못 센다」였다면 다음 편은
   「있는 것이 안 보인다」라 축이 다르다.

   0.5초 루프 : 땅 칸 다섯은 계속 숨을 쉬고, 그 위 표식 둘이 스르르 없어진다 →
   그 자리에 강조색 빈 테두리만 잠깐 떴다가 → 표식이 돌아온다.

   🔴 겹침 감사(캔버스 352×307 · 선 반폭 1.5 · 글로우 blur 6 포함):
     축 A — 🔴 **같은 자리에서 흰 것이 강조색으로 바뀌므로 시간으로 갈랐다:**
         표식 알파는 `u = 0.32` 에 0 이 되고, 강조 빈 테두리는 `u = 0.38` 부터 뜬다.
         돌아올 때는 테두리가 `u = 0.76` 에 0 이 되고 표식이 같은 지점에서 출발한다.
       **겹치는 프레임 0** (양쪽 여유 Δu 0.06 = 0.03초 = 30fps 로 0.9프레임).
     축 A — 좌표로도 갈렸다: 테두리 글로우 바닥 **164.5**(= 140 + 17 + 1.5 + 6)
       ↔ 땅 칸 획 바깥 꼭대기 **184.5** = **20px**.
     축 B(서로 다른 것을 가리키는 흰 글자끼리): 흰 글자가 「다음 편」 하나뿐이라
       성립하지 않는다.
     흰↔흰(서로 다른 물건): 표식 바닥 **155.5** ↔ 땅 칸 꼭대기 **184.5** = 29px.

   세로 예산: 최저 = 땅 칸 획 바깥 **225.5**. 81px 남는다.
   가로: 최우 = 다섯째 칸 획 바깥 **309.5**. 42.5px 남는다.

   ⏱ 등장은 예고 자막 `cue(peekCue)` 에, 루프는 `t` 에 물렸다.
   🔴 소개 자막(cue 1)에는 아무것도 안 걸었다 — 마지막 3.0초는 아웃트로 카드가 `.vis` 를
      픽셀 동일 좌표로 덮으므로 그 구간에 그린 것은 한 프레임도 안 보인다.
      0.5초 루프는 카드가 뜨기 전까지 **6.9바퀴** 돈다(실측: 카드 시각 33.614s
      = TOTAL 36.614 − 3.000, SO 시작 30.175s → 3.439s ÷ 0.5 = **6.88**).

   계속 도는 것(30fps 기준):
     · 땅 칸 다섯의 둘레 5×2×(44+38) = **820px** 이 숨을 쉰다
       (`bre = 1.1·(1 + sin(9.6 t + i·0.7))` → 약 0.35px/프레임)
     · 표식 둘의 알파가 0.16초에 1 → 0 (그리고 다시 0 → 1)
     · 강조 테두리의 대시가 `-t × 24` 로 흐른다 */

const TILES = [56, 108, 160, 212, 264];
const TILE_Y = 186, TILE_W = 44, TILE_H = 38;
const MARKS = [130, 238];
const MARK_Y = 140;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    if (spec.label) {
      ctx.save();
      ctx.textAlign = 'left';
      ctx.font = disp(800, 12.5); ctx.fillStyle = tone('sub');
      ctx.fillText(spec.label, 20, 26);
      ctx.restore();
    }

    const pk = ease(cue(spec.peekCue ?? 0, 0.15, 0.30));
    if (pk <= 0.02) { ctx.textAlign = 'left'; return; }

    /* ── 땅 : 계속 남는다 (흰색) ────────────────────── */
    TILES.forEach((x, i) => {
      const bre = 1.1 * (1 + Math.sin(t * 9.6 + i * 0.7));
      ctx.save();
      ctx.globalAlpha = pk;
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, x + bre, TILE_Y + bre, TILE_W - bre * 2, TILE_H - bre * 2, 5);
      ctx.stroke();
      ctx.restore();
    });

    const u = frac(t / (spec.loopSec ?? 0.5));
    /* 사라졌다 돌아온다. u=0 과 u=1 에서 값이 같아 루프가 이어진다. */
    const markA = u < 0.5
      ? 1 - ease(clamp((u - 0.16) / 0.16))
      : ease(clamp((u - 0.76) / 0.16));
    /* 빈 자리 표시는 표식이 **완전히 사라진 뒤에만** 뜬다(0.32 < 0.38 · 0.76 에 0). */
    const ghostA = ease(clamp((u - 0.38) / 0.12)) * (1 - ease(clamp((u - 0.66) / 0.10)));

    for (const x of MARKS) {
      if (markA > 0.01) {                               // 있던 것 (흰색)
        ctx.save();
        ctx.globalAlpha = pk * markA;
        ctx.translate(x, MARK_Y);
        ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
        roundRect(ctx, -5, -14, 10, 28, 5);
        ctx.stroke();
        ctx.restore();
      }
      if (ghostA > 0.01) {                              // 사라진 자리 (강조색)
        ctx.save();
        ctx.globalAlpha = pk * ghostA;
        ctx.translate(x, MARK_Y);
        setShadow(ctx, GLOW, 6);
        ctx.strokeStyle = tone('accent'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
        ctx.setLineDash([6, 6]); ctx.lineDashOffset = -t * 24;
        roundRect(ctx, -11, -17, 22, 34, 6);
        ctx.stroke();
        ctx.setLineDash([]);
        clearShadow(ctx);
        ctx.restore();
      }
    }

    ctx.textAlign = 'left';
  }
};
