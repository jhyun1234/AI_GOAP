"""ep15s-3 본문4 — 8 비트. **판정이 아니라 자리를 고쳤다.**

  ①② 목수가 마을 안쪽으로 **되걸어 들어온다**
  ③④ 거리 막대가 내려와 **문턱 아래**로 들어간다
  ⑤⑥ 집 없는 주민이 다가가 손을 뻗고, 목수가 망치질을 한다
  ⑦⑧ 집이 선다

🔑 「반경을 늘리면 허공에 대고 말한다」는 **안 한 것**이라 화면에 안 그린다 — 대신 이 샷이
   하는 일이 그 대안이다: 사람을 **가까이 오게** 한다.
🔑 마지막 줄(「정주형 목수는 더 가까워서 성격 차이는 살아 있다」)은 막대가 문턱 **한참
   아래**까지 내려가는 것으로 받는다 — 8타일과 최근접의 차이가 그 여백이다.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import instrument, motions, stage, village

SHOT, SID = 'body4', 'S4'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 10          # 🔴 8 이면 비트가 1.80초로 홀드 상한(1.5)을 넘는다
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

v, arms, fire, _f = C.build()
HOME = [tuple(a.location) for a in arms]
G = C.dist_gauge()
RIGHT = C.cam_right(C.EYE[0], C.EYE[1])
LINES = stage.shot_lines(C.EP, SID)
FOCUS = {1: 0, 2: 0}                    # 「8타일 안으로」·「성격 차이는 살아 있고요」
house = village.house(C.HOUSE_AT[0], C.HOUSE_AT[1], -18)
BASE_SZ, BASE_Z = house.scale.z, house.location.z
cam = stage.light_camera()
stage.key_from_view(*C.EYE)
# 목수는 8타일(2.1m) 자리에서 시작해 마을 안으로 들어온다
FAR0 = (C.CARP_HOME[0] + 5.4 * math.cos(C.CARP_AWAY), C.CARP_HOME[1] + 5.4 * math.sin(C.CARP_AWAY))
PULLED = (C.CARP_HOME[0] - 0.30, C.CARP_HOME[1] - 0.55)
BACK_AT, BACK_SPAN, ASK_AT, BUILD_AT = 0.75 * BEAT, 3.0, 5.25 * BEAT, 6.5 * BEAT
print('[body4-3] %d비트 · %.2f초 · 되돌아옴 %.2f초 · 집 %.2f초' % (BEATS, DUR, BACK_AT, BUILD_AT))


def draw(fi):
    t = fi / C.FPS
    for i, arm in enumerate(arms):
        if i in (C.CARPENTER, C.HOMELESS):
            continue
        stage.pose(arm, motions.MOTIONS[C.CAST[i][0]](t))
        arm.location = HOME[i]

    # ①② 목수가 되돌아온다
    carp = arms[C.CARPENTER]
    cp, cwt = C.walk_to(carp, FAR0, PULLED, t, BACK_AT, BACK_SPAN)
    carp.hide_render = False
    if t < BUILD_AT:
        C.walk_pose(carp, cwt)
    else:
        stage.pose(carp, motions.hammer(t - BUILD_AT))

    # ⑤ 집 없는 주민이 다가가 손을 뻗는다
    near = (PULLED[0] - 0.66, PULLED[1] - 0.70)
    hp, hwt = C.walk_to(arms[C.HOMELESS], HOME[C.HOMELESS], near, t, 2.75 * BEAT, 1.9)
    if t < ASK_AT:
        C.walk_pose(arms[C.HOMELESS], hwt)
    else:
        stage.pose(arms[C.HOMELESS], motions.reach(t - ASK_AT))

    # ③④ 막대가 문턱 아래로 내려온다
    em = C.emphasis(LINES, t, FOCUS)
    instrument.gauge_emph(G, em.get(0, 1.0))
    instrument.gauge_at(G, hp[0] + RIGHT[0] * 0.26, hp[1] + RIGHT[1] * 0.26, 1.0)
    C.dist_set(G, hp, cp)
    C.dist_show(G, True)

    # ⑦⑧ 집이 선다
    k = C.ease((t - BUILD_AT) / (2.75 * BEAT))
    house.scale.z = max(BASE_SZ * k, 1e-4)
    house.location.z = BASE_Z * k
    house.hide_render = k <= 0.001

    u = t / DUR
    (sx, sy, sz), at = C.EYE
    stage.aim(cam, (sx - 1.1 * u, sy + 0.7 * u, sz - 0.15 * u), at)
    village.flicker(fire, t)


stage.bake(OUT, NF, C.FPS, draw, 'body4-3')
