"""ep16s-4 본문2 — 10 비트 · 13.75 초. **지우기 전에 사진을 찍었다.**

  0.00~3.30   「고칠 자리는 그 정리 함수라고 적어 뒀었는데,」
     끊긴 실 끝이 **허공에 떠 있다**(굳지 않은 시안 토막)
  3.30~6.35   「결국 한 글자도 안 고쳤어요」
     화면이 **그대로다** — 아무것도 안 바뀐다. 🔴 정적 상한(3초)에 걸리므로 카메라만
     아주 천천히 흐르고, 앞뒤 줄이 그 정지를 감싼다
  6.35~9.85   「지워지기 전에 사진을 한 장 찍어 뒀거든요」
     끊기기 직전의 실이 **보라로 굳어** 무덤 쪽에 남는다
  9.85~13.75  「죽는 순간의 단짝과 원한을 그 사람 기록에 남긴 거예요」
     비석 옆 도구에서 실 **두 가닥**이 뻗는다(단짝 · 원한)

🔴 「한 글자도 안 고쳤다」는 이 회차에서 **화면이 안 바뀌는 것으로 지는 유일한 줄**이다.
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

SHOT, SID = 'body2', 'S2'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 10
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

L = [C.line_at(SID, i) for i in range(4)]
v, arms, grave = C.build(grave=True)
HOME = [tuple(a.location) for a in arms]
C.hide_person(arms[C.A], False)               # A 는 이미 무덤이다
VIOLET, CYAN = C.mats()

AX, AY = C.CAST[C.A][1]
BX, BY = C.CAST[C.B][1]
STUB = instrument.thread((BX, BY, 0.92), ((AX + BX) / 2, (AY + BY) / 2, 0.92), CYAN, w=0.016)
TOOL = village.hammer()
TOOL.location = (AX + 0.42, AY - 0.20, 0.0)
TOOL.rotation_mode = 'XYZ'
TOOL.rotation_euler = (math.radians(90), 0.0, 0.0)
TOOL.scale = (1.5, 1.5, 1.5)          # 🔴 1.0 은 화면에서 보라 점이다(ep16s-1 과 같은 값)
for ch in TOOL.children:
    ch.data.materials.clear()
    ch.data.materials.append(VIOLET)
KEPT = [instrument.thread((AX + 0.42, AY - 0.20, 0.62), (BX, BY, 0.92), VIOLET, w=0.018),
        instrument.thread((AX + 0.42, AY - 0.20, 0.62),
                          (C.CAST[C.C][1][0], C.CAST[C.C][1][1], 0.92), VIOLET, w=0.018)]

FREEZE_AT = L[2] + 0.75
SECOND_AT = L[3] + 0.55

KEY = [(0.00, C.MID[0], C.MID[1]),
       (L[1] - 0.10, (-1.05, -6.05, 1.55), (-1.75, -2.85, 0.85)),
       (L[1] + 1.40, (-0.95, -5.95, 1.50), (-1.70, -2.85, 0.82)),
       (L[2] + 0.60, C.GRAVE_CAM[0], C.GRAVE_CAM[1]),
       (DUR, (-0.45, -6.95, 1.95), (-1.65, -2.75, 0.90))]

cam = stage.light_camera()
C.lens(cam, C.MID_LENS)
stage.key_from_view(*C.MID)
print('[body2-16-4] %d비트 · %.2f초 · 줄 %s' % (BEATS, DUR, [round(x, 2) for x in L]))


def draw(fi):
    t = fi / C.FPS
    C.grave_rise(grave, 1.0)
    STUB.hide_render = t >= FREEZE_AT
    for ch in TOOL.children:
        ch.hide_render = t < FREEZE_AT
    KEPT[0].hide_render = t < FREEZE_AT
    KEPT[1].hide_render = t < SECOND_AT
    C.all_work(arms, t, HOME, skip=(C.A,))
    C.fly(cam, KEY, t)


stage.bake(OUT, NF, C.FPS, draw, 'body2-16-4')
