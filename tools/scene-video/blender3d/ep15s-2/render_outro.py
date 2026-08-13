"""ep15s-2 아웃트로 — 4 비트 · 4.55 초. **다음 편의 무대를 남긴다.**

  0.00~1.14  ① 마을이 조용하다. 돌아온 주민들이 제자리로 든다
  1.14~2.28  ② 카메라가 집 셋 쪽으로 흐른다
  2.28~3.41  ③ 집 앞에 한 사람이 서서 기다린다 — 다음 편의 주인공이다
  3.41~4.55  ④ 그 옆자리가 비어 있다(목수가 없는 자리)

🔑 예고 자막은 「목수가 떠나자 아무도 집을 못 얻어요」다. 화면은 **그 무대**만 세운다 —
   사건은 다음 편의 것이라 여기서 미리 쓰면 그 편이 반복으로 읽힌다.
🔴 엔진이 이 샷 위에 **아웃트로 카드**(형제 사다리)를 얹는다. 그림이 화면 아래쪽을
   비워 둘 필요는 없다(카드는 상자 안쪽 위에 뜬다) — 다만 **조용해야** 카드가 읽힌다.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import motions
import stage
import village

SHOT, SID = 'outro', 'SO'
OUT = C.out_dir(SHOT)
DUR = C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 4
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

# 집 앞에서 기다리는 사람 — 🔴 집을 **정면으로** 보게 세운다(village.HOUSES[1] 은 (0.2, 4.0))
WAIT_AT = (0.35, 2.45)
HOUSE = village.HOUSES[1]
EMPTY = (2.05, 2.15)          # 목수가 없는 자리

# 카메라 — 마을에서 집 쪽으로 흐른다
CAM_A = ((4.10, -5.60, 2.05), (-0.20, -0.60, 0.75))
CAM_B = ((2.35, -3.75, 2.35), (0.30, 2.10, 0.95))

v, arms, fire, flakes = C.build()
HOME = [tuple(a.location) for a in arms]
cam = stage.light_camera()
stage.key_from_view(*CAM_B)

print('[outro-2] %d비트 · %.2f초 · 기다리는 자리 (%.2f, %.2f)' % (BEATS, DUR, WAIT_AT[0], WAIT_AT[1]))


def draw(fi):
    t = fi / C.FPS
    u = t / DUR

    for i, arm in enumerate(arms):
        if i == C.HUNGRY:
            # 🔑 그 사람이 **집 앞으로 걸어와 기다린다** — 다음 편의 첫 장면이 여기서 시작한다
            k = C.ease(t / (2.4 * BEAT))
            x = HOME[i][0] + (WAIT_AT[0] - HOME[i][0]) * k
            y = HOME[i][1] + (WAIT_AT[1] - HOME[i][1]) * k
            walked = min(t, 2.4 * BEAT)
            ww = C.ease((2.4 * BEAT - t) / 0.35)
            stage.pose(arm, motions.blend(motions.stop(t), motions.walk(walked),
                                          max(0.0, min(1.0, ww))))
            arm.location = (x, y, 0)
            ang = math.atan2(y - HOUSE[1], x - HOUSE[0])
            arm.rotation_euler = (0, 0, ang)
            continue
        C.work(arm, C.CAST[i][0], t)
        arm.location = HOME[i]

    k = C.ease(u)
    stage.aim(cam,
              tuple(a + (b - a) * k for a, b in zip(CAM_A[0], CAM_B[0])),
              tuple(a + (b - a) * k for a, b in zip(CAM_A[1], CAM_B[1])))

    village.flicker(fire, t)
    village.snowfall(flakes, t)


stage.bake(OUT, NF, C.FPS, draw, 'outro-2')
