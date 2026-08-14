"""ep16s-1 본문2 — 12 비트 · 16.07 초. **쓰러지는 순간에 넘겨서, 도구가 비석 옆에 선다.**

  0.00~4.29   「제가 아는 건 죽은 사람이 하나 늘었다는 것뿐이었어요」
     무덤 하나가 **또 하나 솟는다**(0.34). 늘어난 것은 그것뿐이다
  4.29~8.68   「그래서 쓰러지는 그 순간에 이름을 넘기게 했어요」
     가깝게(0.48). 곡괭이를 든 사람이 **일하다 무너지고**, 곡괭이가 **손을 떠나 구른다**
  8.68~12.01  「이제 비석 옆에 그 사람이 쓰던 도구가 서요」
     무덤이 그 자리를 대신하고, 그 곡괭이가 **비석 옆에 선다**(뜻층 보라 · 0.45)
  12.01~16.07 「도끼면 나무꾼, 물뿌리개면 밭 보던 사람이고요」
     카메라가 무리 A 를 지나며 **물뿌리개 · 곡괭이 · 도끼**를 차례로 지난다(0.30)

🔴 **막대(사망 카운터)를 걷어냈다**(2026-08-14). 첫 판은 「죽은 사람이 하나 늘었다」를
   계기 막대 한 칸으로 그렸는데, 사용자 판정에서 계기가 **뜻이 안 읽히는 것**으로 지목됐다.
   늘어난 것은 화면에서도 **무덤 하나**로 그린다 — 이 세계에 이미 있는 물건이다.
🔴 손에 붙은 것과 땅에 구르는 것은 **다른 물건 둘**이다(부모를 프레임마다 갈면 결정성이 죽는다).
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import stage
import village

SHOT, SID = 'body2', 'S2'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 12
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

L = [C.line_at(SID, i) for i in range(4)]
HERO = 1                                   # 되살려 보여 주는 사람 — 곡괭이(mine)
LATE = 4                                   # 첫 줄에서 하나 더 솟는 무덤
v, arms, graves = C.build()
HOME = [tuple(a.location) for a in arms]
C.dead_hide(arms)

HELD = stage.hold(arms[HERO], village.pickaxe(), 'hand.R', (0.0, 0.02, 0.0))
FALLEN = village.pickaxe()
FALLEN.rotation_mode = 'XYZ'
STOOD = [C.tool(kind, tinted=True) for _job, _p, _s, kind in C.DEAD]
for i, (_job, (x, y), _spot, _kind) in enumerate(C.DEAD):
    C.tool_stand(STOOD[i], x, y, face=C.home_face(i))
    C.tool_show(STOOD[i], False)

FALL_AT = L[1] + 1.05
DROP_AT = FALL_AT + 0.40
HX, HY = C.spot_of(HERO)

K0 = C.cam_at(C.mid_of((LATE, 3)), frac=0.34, lens_mm=C.LENS_MID, yaw=-96, elev=19, dz=0.55)
# 🔴 +24° 쪽은 옆 무덤이 **앞을 가린다**(접점 시트). 반대쪽으로 돌아 들어간다.
K1 = C.cam_at(HERO, frac=0.48, lens_mm=C.LENS_CLOSE, yaw=C.face_yaw(HERO, -38), elev=17,
              dz=0.66)
K2 = C.cam_at(HERO, frac=0.45, lens_mm=C.LENS_CLOSE, yaw=C.face_yaw(HERO, -22), elev=16,
              dz=0.50)
K3 = C.cam_at(C.mid_of(C.GROUP_A), frac=0.30, lens_mm=C.LENS_MID, yaw=-88, elev=20, dz=0.50)
K4 = C.cam_at(C.mid_of(C.GROUP_A), frac=0.30, lens_mm=C.LENS_MID, yaw=-116, elev=18, dz=0.50)
KEY = [(0.00, K0[0], K0[1]),
       (L[1] - 0.20, K1[0], K1[1]),
       (L[2] + 0.30, K2[0], K2[1]),
       (L[3] + 0.30, K3[0], K3[1]),
       (DUR, K4[0], K4[1])]

cam = stage.light_camera()
C.lens(cam, C.LENS_MID)
stage.key_from_view(*K1)
C.report('body2-16-1', KEY, C.LENS_MID)
print('[body2-16-1] %d비트 · %.2f초 · 줄 %s' % (BEATS, DUR, [round(x, 2) for x in L]))


def draw(fi):
    t = fi / C.FPS
    alive = L[1] - 0.30 <= t < L[2] - 0.25      # 되살려 보여 주는 구간
    for i, g in enumerate(graves):
        if i == LATE:
            C.grave_rise(g, C.ease((t - 1.30) / 0.70))   # 하나가 **더** 는다
        else:
            C.grave_show(g, not (alive and i == HERO))
            C.grave_rise(g, 1.0)
    hero = arms[HERO]
    C.hide_person(hero, alive)
    if alive:
        C.fall(hero, HERO, HOME[HERO], t, FALL_AT)
    C.tool_show(HELD, alive and t < DROP_AT)
    rolling = alive and t >= DROP_AT
    C.tool_show(FALLEN, rolling)
    if rolling:
        k = C.ease((t - DROP_AT) / 0.55)
        FALLEN.location = (HX + 0.30 * k, HY - 0.34 * k, 0.26 - 0.22 * k)
        FALLEN.rotation_euler = (math.radians(90 - 76 * k), 0.0,
                                 C.home_face(HERO) + 1.15 * k)
    for i, root in enumerate(STOOD):
        on = (t >= L[2] - 0.10) if i == HERO else (t >= L[3] - 0.05 + 0.12 * (i % 3))
        C.tool_show(root, on)
    C.work(arms[C.LIVE_I], C.CAST[C.LIVE_I][0], t)
    C.fly(cam, KEY, t)


stage.bake(OUT, NF, C.FPS, draw, 'body2-16-1')
