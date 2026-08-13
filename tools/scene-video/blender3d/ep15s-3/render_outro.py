"""ep15s-3 아웃트로 — 4 비트. **다음 편의 무대: 모닥불이 하나둘 늘기 시작한다.**

🔑 예고 자막은 「집 주변이 모닥불로 뒤덮여요」다. 여기서는 **시작만** 보여 준다 —
   뒤덮인 화면은 다음 편의 것이고, 여기서 다 쓰면 그 편이 반복으로 읽힌다.
🔴 엔진이 이 샷 위에 아웃트로 카드를 얹는다. 그림이 조용해야 카드가 읽힌다.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from mathutils import Vector
import motions, stage, village

SHOT, SID = 'outro', 'SO'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 4
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

v, arms, fire, _f = C.build()
HOME = [tuple(a.location) for a in arms]
house = village.house(C.HOUSE_AT[0], C.HOUSE_AT[1], -18)
# 🔑 다음 편의 씨앗 — 집 둘레에 모닥불이 **하나씩** 는다
EXTRA = [(-2.35, -2.35), (-0.45, -3.65), (-2.55, -3.75)]
# 🔴 불만 세우면 **허공에 뜬 불꽃**이 된다. 모닥불은 돌 테두리·장작·불꽃 셋이다(PROPS.md).
_m = village.mats()
pits = [village._fire_pit(Vector((x, y, 0)), _m['stone'], _m['char']) for x, y in EXTRA]
fires = [village.flame(Vector((x, y, 0))) for x, y in EXTRA]
AT = [1.1 * BEAT, 2.0 * BEAT, 2.9 * BEAT]
cam = stage.light_camera()
# 🔴 카메라를 (1.90,-5.60) 에 두고 새 집(-1.35,-3.05)을 겨눴더니 **집이 화면을 통째로
#    덮었다**(3m 앞이었다). 같은 방향을 더 멀리서, 조금 위에서 본다.
CAM_A = ((3.90, -6.60, 2.30), (-0.40, -1.40, 0.75))
CAM_B = ((3.05, -7.15, 2.55), (-1.05, -2.55, 0.70))
stage.key_from_view(*CAM_B)
print('[outro-3] %d비트 · %.2f초 · 모닥불 %d개가 는다' % (BEATS, DUR, len(EXTRA)))


def draw(fi):
    t = fi / C.FPS
    for i, arm in enumerate(arms):
        stage.pose(arm, motions.MOTIONS['hammer' if i == C.CARPENTER else
                                        ('stop' if i == C.HOMELESS else C.CAST[i][0])](t))
        arm.location = HOME[i]
    for parts, pit, t0 in zip(fires, pits, AT):
        on = t > t0
        pit.hide_render = not on
        for o in parts:
            o.hide_render = not on
        if on:
            village.flicker(parts, t - t0)
    village.flicker(fire, t)
    k = C.ease(t / DUR)
    stage.aim(cam, tuple(a + (b - a) * k for a, b in zip(CAM_A[0], CAM_B[0])),
              tuple(a + (b - a) * k for a, b in zip(CAM_A[1], CAM_B[1])))


stage.bake(OUT, NF, C.FPS, draw, 'outro-3')
