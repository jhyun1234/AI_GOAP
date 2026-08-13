"""ep15s-2 훅 — 5 비트 · 6.87 초. **위험이 오는데 한 명이 밥을 먹고 있다.**

  0.00~1.37  ① 마을. 여섯이 제각기 제 일을 한다(밭·불·나무·우물·덤불, 그리고 **밥**)
  1.37~2.75  ② 화면 오른쪽에서 붉은 덩이가 들어온다
  2.75~4.12  ③ 다섯이 하던 일을 놓고 **뛴다**
  4.12~5.50  ④ 카메라가 남은 한 명에게 붙는다 — 그는 계속 먹고 있다
  5.50~6.87  ⑤ 덩이가 그 뒤까지 온다. 그는 여전히 먹는다

🔴 자막을 가려도 무슨 일인지 보여야 한다 — 그게 이 편 그림의 판정 기준이다.
   대비 하나로 선다: **다섯은 움직이고 하나는 안 움직인다.**
🔴 뜻층은 **빨강 하나**다(설계 §2 상한 둘). 성격 표식은 안 띄운다 — 이 편의 사건은
   「누가 어떤 성격인가」가 아니라 「무엇을 먼저 고르나」다.
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

SHOT, SID = 'hook', 'SH'
OUT = C.out_dir(SHOT)
DUR = C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 5
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초 — 비트를 늘려라' % BEAT

RUN_AT = 2 * BEAT                      # ③ 다섯이 뛰기 시작하는 시각
NEAR = C.behind(C.CAST[C.HUNGRY][1], C.CLOSE[0], 1.05)

v, arms, fire, flakes = C.build()
HOME = [tuple(a.location) for a in arms]
d = C.danger()
cam = stage.light_camera()
stage.key_from_view(*C.EYE)            # 🔴 안 부르면 여섯이 통째로 역광이 된다

# 🔴 각을 손으로 정하지 않는다 — 소품을 피하는 각을 **재서** 고른다(common.flee_angles).
import bpy
bpy.context.view_layer.update()
OBST = village.obstacles(exclude=(v['ground'],))
ANG = list(C.flee_angles([HOME[i][:2] for i in range(6)], OBST))
print('[hook-2] %d비트 · %.2f초 · 비트 %.2f · 뛰는 각 %s'
      % (BEATS, DUR, BEAT, ' '.join('%.0f°' % math.degrees(a) for a in ANG)))


def draw(fi):
    t = fi / C.FPS

    for i, arm in enumerate(arms):
        job = C.CAST[i][0]
        if i == C.HUNGRY:
            # 🔑 **이 사람만 아무것도 안 바뀐다.** 위험이 와도 하던 것을 계속한다
            C.work(arm, job, t)
            arm.location = HOME[i]
            continue
        if t < RUN_AT:
            C.work(arm, job, t)
            arm.location = HOME[i]
        else:
            C.flee(arm, HOME[i], ANG[i], t, RUN_AT)
            C.flee_pose(arm, t, RUN_AT, job)

    # ② 덩이가 들어와 배고픈 사람 옆까지 온다
    C.danger_at(d, (t - BEAT) / (DUR - BEAT), NEAR, t)
    d.hide_render = t < BEAT * 0.9

    # ④ 카메라가 조망에서 그 한 명에게 붙는다 — **컷이 아니라 다가감**이다
    u = C.ease((t - 3 * BEAT) / (2 * BEAT))
    (sx, sy, sz), (ax, ay, az) = C.EYE
    (cx, cy, cz), (bx, by, bz) = C.CLOSE
    stage.aim(cam, (sx + (cx - sx) * u, sy + (cy - sy) * u, sz + (cz - sz) * u),
              (ax + (bx - ax) * u, ay + (by - ay) * u, az + (bz - az) * u))

    village.flicker(fire, t)
    village.snowfall(flakes, t)


stage.bake(OUT, NF, C.FPS, draw, 'hook-2')
