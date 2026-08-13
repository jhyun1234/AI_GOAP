"""ep16s-4 본문3 — 9 비트 · 11.47 초. **방향이 반대가 됐다.**

  0.00~3.45  「이제 무덤을 누르면 누구의 단짝이었는지가 같이 떠요」
     무덤에서 뻗은 실이 **산 사람에게** 이어진다
  3.45~7.55  「산 사람 목록에서 죽은 사람이 빠지는 건 그대로 옳고요」
     산 사람 곁의 실은 **없는 채로** 둔다 — 그쪽은 안 고쳤다
  7.55~11.47 「죽음이 못 지우는 게 하나 생겼어요」
     무덤에서 뻗은 그 실이 **그대로 걸려 있다.** 마을은 계속 돌아간다

🔑 앞 샷과 같은 실인데 **읽는 방향이 반대**다 — 산 사람에게서 죽은 사람으로 가던 것이
   무덤에서 산 사람으로 온다. 그것이 이 편의 착지다.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import instrument
import stage
import village

SHOT, SID = 'body3', 'S3'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 9
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

L = [C.line_at(SID, i) for i in range(3)]
v, arms, grave = C.build(grave=True)
HOME = [tuple(a.location) for a in arms]
C.hide_person(arms[C.A], False)
VIOLET, CYAN = C.mats()
AX, AY = C.CAST[C.A][1]
BX, BY = C.CAST[C.B][1]
TOOL = village.hammer()
TOOL.location = (AX + 0.42, AY - 0.20, 0.0)
TOOL.rotation_mode = 'XYZ'
TOOL.rotation_euler = (math.radians(90), 0.0, 0.0)
TOOL.scale = (1.5, 1.5, 1.5)
for ch in TOOL.children:
    ch.data.materials.clear()
    ch.data.materials.append(VIOLET)
KEPT = [instrument.thread((AX + 0.42, AY - 0.20, 0.62), (BX, BY, 0.92), VIOLET, w=0.018),
        instrument.thread((AX + 0.42, AY - 0.20, 0.62), (C.CAST[C.C][1][0],
                          C.CAST[C.C][1][1], 0.92), VIOLET, w=0.018)]

KEY = [(0.00, C.GRAVE_CAM[0], C.GRAVE_CAM[1]),
       (L[1] - 0.10, (-0.65, -6.55, 1.75), (-1.55, -2.85, 0.90)),
       (L[2] - 0.05, (-0.35, -7.45, 2.15), (-1.15, -2.65, 0.95)),
       (DUR, C.EYE[0], C.EYE[1])]

cam = stage.light_camera()
C.lens(cam, C.MID_LENS)
stage.key_from_view(*C.GRAVE_CAM)
print('[body3-16-4] %d비트 · %.2f초 · 줄 %s' % (BEATS, DUR, [round(x, 2) for x in L]))


def draw(fi):
    t = fi / C.FPS
    C.grave_rise(grave, 1.0)
    for th in KEPT:
        th.hide_render = False
    C.all_work(arms, t, HOME, skip=(C.A,))
    C.fly(cam, KEY, t)


stage.bake(OUT, NF, C.FPS, draw, 'body3-16-4')
