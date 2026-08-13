"""ep15s-3 훅 — 5 비트 · 6.4 초. **목수를 찾아 나섰는데 그 자리가 비어 있다.**

  ① 마을. 집 없는 주민이 서 있고 넷은 제 일을 한다
  ②③ 그가 목수 자리 쪽으로 **걸어간다**
  ④ 그 자리에는 아무도 없다 — 카메라가 빈자리를 본다
  ⑤ 그가 멈춰 선다

🔴 자막을 가려도 무슨 일인지 보여야 한다: **찾아갔는데 없다.**
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import motions, stage, village

SHOT, SID = 'hook', 'SH'
OUT, DUR = C.out_dir(SHOT), C.seconds('SH')
NF = round(DUR * C.FPS) + 1
BEATS = 5
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

v, arms, fire, _f = C.build()
HOME = [tuple(a.location) for a in arms]
arms[C.CARPENTER].hide_render = True            # 🔑 **목수는 이미 없다** — 그게 훅이다
cam = stage.light_camera()
stage.key_from_view(*C.EYE)
GO_AT, GO_SPAN = 0.9 * BEAT, 2.2
print('[hook-3] %d비트 · %.2f초 · 비트 %.2f' % (BEATS, DUR, BEAT))


def draw(fi):
    t = fi / C.FPS
    for i, arm in enumerate(arms):
        if i in (C.CARPENTER, C.HOMELESS):
            continue
        stage.pose(arm, motions.MOTIONS[C.CAST[i][0]](t))
        arm.location = HOME[i]
    # ②③⑤ 집 없는 주민이 목수 자리로 걸어가 멈춘다
    p, wt = C.walk_to(arms[C.HOMELESS], HOME[C.HOMELESS], C.CARP_HOME, t, GO_AT, GO_SPAN)
    C.walk_pose(arms[C.HOMELESS], wt)
    if t > GO_AT + GO_SPAN:
        stage.pose(arms[C.HOMELESS], motions.look_around(t - GO_AT - GO_SPAN))
    # ④ 카메라가 빈자리 쪽으로 흐른다
    u = C.ease((t - 2.2 * BEAT) / (2.4 * BEAT))
    (sx, sy, sz), (ax, ay, az) = C.EYE
    stage.aim(cam, (sx - 1.15 * u, sy + 0.85 * u, sz - 0.22 * u),
              (ax + (C.CARP_HOME[0] - ax) * u, ay + (C.CARP_HOME[1] - ay) * u, az))
    village.flicker(fire, t)


stage.bake(OUT, NF, C.FPS, draw, 'hook-3')
