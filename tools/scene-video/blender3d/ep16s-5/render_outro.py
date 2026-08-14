"""ep16s-5 아웃트로 — 7 비트 · 8.72 초. **밭을 지나쳐 불가로 간다.**

  0.00~4.25 「다음 편에서는, 집도 밭도 있는데 왜 굶어 죽었을까요」
     밭 옆에서 사람이 **아무것도 안 심고 지나간다**(`walk`)
  4.25~8.72 「곳간에 조금이라도 남으면 아무도 씨를 안 뿌리고 있었거든요」
     그 사람이 밭을 지나쳐 **불가로 가서 몸을 쬔다**(`warm`)

🔴 예고는 **다음 회차 원문**(M14 가을과 겨울)에서 뽑았다 — 파종 방아쇠가 「식량 3일치
   이하 + 빈 밭」에 묶여 있어 '가난해진 다음에야 대비를 시작하는 규칙만 있었습니다'.
   이 글(M13)의 마지막 문단은 근거로 쓰지 않았다(런북 함정 8).
🔴 모닥불(앰버)은 뜻층이다. 이 샷에서는 보라를 안 켜므로 예산 안(1색)이다.
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
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 7
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

L = [C.line_at(SID, i) for i in range(2)]
v, arms, _g = C.build()
HOME = [tuple(a.location) for a in arms]
FIRE = village.flame(village.SPOTS['fire'])
HERO = 1                                     # 밭 옆에 있던 사람
FIRE_SPOT = (village.SPOTS['fire'].x - 0.85, village.SPOTS['fire'].y - 0.75)
GO_AT, GO_SPAN = 0.85, 3.30

KEY = [(0.00, (-1.35, -6.55, 2.05), (-2.15, -1.65, 0.95)),
       (L[1] - 0.10, (-0.85, -6.15, 2.15), (-1.15, -1.05, 1.05)),
       (DUR, (-0.35, -6.55, 2.35), (-0.45, -0.15, 1.15))]

cam = stage.light_camera()
C.lens(cam, C.MID_LENS)
stage.key_from_view(KEY[0][1], KEY[0][2])
print('[outro-16-5] %d비트 · %.2f초' % (BEATS, DUR))


def draw(fi):
    t = fi / C.FPS
    hero = arms[HERO]
    wt = min(max(t - GO_AT, 0.0), GO_SPAN)
    hx, hy = HOME[HERO][0], HOME[HERO][1]
    ang = math.atan2(FIRE_SPOT[1] - hy, FIRE_SPOT[0] - hx)
    d = min(motions.WALK_SPEED * wt, math.hypot(FIRE_SPOT[0] - hx, FIRE_SPOT[1] - hy))
    hero.location = (hx + d * math.cos(ang), hy + d * math.sin(ang), 0)
    hero.rotation_euler = (0, 0, ang + math.pi)
    if t < GO_AT:
        C.work(hero, C.CAST[HERO][0], t)
    elif wt < GO_SPAN:
        stage.pose(hero, motions.blend(motions.stop(wt), motions.walk(wt),
                                       C.ease(wt / 0.26)))
    else:
        u = t - GO_AT - GO_SPAN
        stage.pose(hero, motions.blend(motions.walk(GO_SPAN), motions.warm(u),
                                       C.ease(u / 0.40)))
    C.all_work(arms, t, HOME, skip=(HERO,))
    village.flicker(FIRE, t)
    C.fly(cam, KEY, t)


stage.bake(OUT, NF, C.FPS, draw, 'outro-16-5')
