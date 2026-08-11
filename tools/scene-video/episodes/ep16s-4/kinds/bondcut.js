import {
  disp, ease, clamp, lerp, frac,
  fitCanvas, mkCanvas, roundRect, tone, setShadow, clearShadow, GLOW
} from '../../../engine/lib.js';

/* bondcut — 훅. 한쪽이 쓰러지자 둘을 잇던 것과, 남은 쪽이 쥐고 있던 것까지 같이 없어진다.

   원문 근거(「문제: 무덤은 있는데 이름이 없었다」 절):
   > "주민이 사라질 때 호출되는 정리 함수가 관계 기록을 양방향으로 삭제하고 있었던 겁니다.
   >  단짝이 죽으면 남은 사람의 기억에서도 그 관계가 통째로 없어졌습니다."
   라벨 「단짝」은 원문 낱말 그대로, 「남은 사람 기억」은 같은 문장의 '남은 사람의 기억에서도'.

   🔴 훅은 **현상만** 말한다. 원인(뒷정리가 양쪽을 지운다)은 다음 샷의 몫이라 여기 안 그린다 —
   4단 함정 6(「자를 곳은 거의 항상 훅이 방금 말한 본문 줄」)을 설계 단계에서 피한 것이다.

   색 규약(회차 공통): **강조색 = 둘이 이어져 있었다는 기록 / 흰색 = 사람.**
   그래서 걷히는 선도, 남은 쪽 아래 조각도 강조색이고 표식만 흰색이다.

   🔴 흰색과 강조색을 같은 픽셀에 안 겹친다 — **가로·세로 좌표로 갈랐다.**
     · 관계 선 glow(blur 8) 오른끝 = 212 + 8 = 220.
       오른쪽 표식이 **쓰러지는 도중 가로로 가장 넓어지는 순간**의 왼끝 =
       258 − √(9² + 24²) − 1.5(선 반폭) = 258 − 25.63 − 1.5 = **230.9**. → **10.9px** 뜬다.
       (그 순간 선은 아직 온전하다 — 걷힘은 u 0.50 부터고 쓰러짐은 u 0.28~0.48 이다.)
     · 기억 조각 glow 위끝 = 208 − 8 = 200 / 왼쪽 표식 바닥 = 158 + 24 + 1.5 = **183.5**. → **16.5px**.
     · 라벨(흰) ↔ 강조: 「단짝」 글립 바닥 115 ↔ 선 glow 위 147 = 32px ·
       「남은 사람 기억」 글립 꼭대기 238 ↔ 조각 glow 바닥 226 = 12px.
   🔴 흰↔흰(서로 다른 것을 가리키는 둘): 「단짝」(x 158~194)과 「남은 사람 기억」(x 55~145)은
     세로로 **123px** 떨어져 있다. 라벨과 그것이 가리키는 표식은 의도된 쌍이다.

   세로 예산(캔버스 307): 최저 = 라벨2 글립 바닥 251. **56px** 남는다.
   가로(캔버스 352): 최대 = 쓰러진 표식 오른끝 258 + 25.63 + 1.5 = 285.1. **67px** 남는다.

   ⏱ 등장만 `cue(0)` 에 물렸고 사건은 전부 2.2초 `t` 루프다 — 훅은 자막이 한 줄이라
      cue 인덱스가 하나뿐이고, 루프가 그 한 줄 내내 사건을 두 번 보여준다.
   계속 도는 것(30fps 기준): 루프 자체(선 72px 이 0.48초에 걷힘 → 5.0px/프레임) +
      표식 숨 진폭 1.5px · 6.2rad/s → 0.31px/프레임. */

const MARK_L = 100, MARK_R = 258, MARK_CY = 158, MARK_HW = 9, MARK_HH = 24;
const BOND_X0 = 140, BOND_X1 = 212, BOND_Y = 155, BOND_H = 6;
const CHIP_X0 = 72, CHIP_X1 = 128, CHIP_Y = 208, CHIP_H = 10;

export default {
  build(root) { root.innerHTML = ''; mkCanvas(root); },

  draw(root, { spec, t, cue }) {
    const { ctx, w } = fitCanvas(root.querySelector('canvas'));
    ctx.textBaseline = 'alphabetic';

    const ctext = (txt, x, y, weight, size, color, maxW, alpha) => {
      if (!txt || alpha <= 0.01) return;
      let fs = size; ctx.font = disp(weight, fs);
      while (fs > 8 && ctx.measureText(txt).width > maxW) { fs -= 0.5; ctx.font = disp(weight, fs); }
      const tw = ctx.measureText(txt).width;
      ctx.save();
      ctx.globalAlpha = alpha;
      ctx.textAlign = 'center'; ctx.fillStyle = color;
      ctx.fillText(txt, clamp(x, 14 + tw / 2, w - 14 - tw / 2), y);
      ctx.restore();
    };

    const st = ease(cue(spec.bondCue ?? 0, 0.15, 0.30));
    if (st <= 0.01) { ctx.textAlign = 'left'; return; }

    /* 라벨은 루프와 무관하게 계속 서 있다 — 그림의 틀이지 사건이 아니다 */
    ctext(spec.bondLabel, 176, 112, 900, 13, tone('ink'), 120, st);
    ctext(spec.keepLabel, MARK_L, 248, 800, 12.5, tone('sub'), 150, st);

    const u = frac(t / (spec.loopSec ?? 2.2));
    /* 루프 되감기 — 0~0.06 에 들어오고 0.90~1.00 에 나간다. 알파로만 되감으므로
       쓰러진 표식이 갑자기 일어서는 프레임이 없다. */
    const loopA = st * Math.min(
      ease(clamp(u / 0.06)),
      1 - ease(clamp((u - 0.90) / 0.10)));
    if (loopA <= 0.01) { ctx.textAlign = 'left'; return; }

    const fall = ease(clamp((u - 0.28) / 0.20));   // 오른쪽이 눕는다
    const cut  = ease(clamp((u - 0.50) / 0.22));   // 선이 양끝에서 안으로 걷힌다
    const chip = 1 - ease(clamp((u - 0.56) / 0.18)); // 남은 쪽 조각도 같이 꺼진다

    /* ── 관계 선 (강조색) — 양끝에서 동시에 안으로 걷힌다 ── */
    const bx0 = lerp(BOND_X0, 176, cut), bx1 = lerp(BOND_X1, 176, cut);
    if (bx1 - bx0 > 0.5) {
      ctx.save();
      ctx.globalAlpha = loopA * (0.82 + 0.18 * (0.5 + 0.5 * Math.sin(t * 4.1)));
      setShadow(ctx, GLOW, 8);
      ctx.fillStyle = tone('accent');
      ctx.fillRect(bx0, BOND_Y, bx1 - bx0, BOND_H);
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 남은 쪽이 쥐고 있던 조각 (강조색) ── */
    if (chip > 0.01) {
      ctx.save();
      ctx.globalAlpha = loopA * chip;
      setShadow(ctx, GLOW, 8);
      ctx.fillStyle = tone('accent');
      ctx.fillRect(CHIP_X0, CHIP_Y, CHIP_X1 - CHIP_X0, CHIP_H);
      clearShadow(ctx);
      ctx.restore();
    }

    /* ── 표식 둘 (흰색) — 오른쪽만 눕는다 ── */
    for (let i = 0; i < 2; i++) {
      const cx = i === 0 ? MARK_L : MARK_R;
      const br = 1.5 * (0.5 + 0.5 * Math.sin(t * 6.2 + i * 1.1));
      const k = i === 1 ? fall : 0;
      ctx.save();
      ctx.globalAlpha = loopA;
      ctx.translate(cx, lerp(MARK_CY, MARK_CY + 15, k) + br);
      ctx.rotate(k * Math.PI / 2);
      ctx.strokeStyle = tone('ink'); ctx.lineWidth = 3; ctx.lineJoin = 'round';
      roundRect(ctx, -MARK_HW, -MARK_HH, MARK_HW * 2, MARK_HH * 2, 8);
      ctx.stroke();
      ctx.restore();
    }

    ctx.textAlign = 'left';
  }
};
