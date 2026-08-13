"""ep15s-3 본문1 — 7 비트. **부탁은 다가가야 걸린다.**

  ① 되짚기: 예전엔 목수가 마을 안에 있었다
  ②③ 집 없는 주민이 목수 **앞까지 걸어간다**
  ④ 손을 뻗는다(부탁) — 거리 막대가 뜨고 **문턱 아래**다
  ⑤⑥ 목수가 망치질을 시작하고 집이 선다
  ⑦ 막대는 계속 문턱 아래

🔑 수치는 **거리**다(계기 시안 막대 · 문턱 표시 = 6타일).
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import instrument, motions, stage, village

SHOT, SID = 'body1', 'S1'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 7
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

v, arms, fire, _f = C.build()
HOME = [tuple(a.location) for a in arms]
G = C.dist_gauge()
RIGHT = C.cam_right(C.EYE[0], C.EYE[1])
LINES = stage.shot_lines(C.EP, SID)
FOCUS = {1: 0}                                   # 「6타일 안에서 마주쳐야」 — 거리 막대
house = village.house(C.HOUSE_AT[0], C.HOUSE_AT[1], -18)
BASE_SZ, BASE_Z = house.scale.z, house.location.z
cam = stage.light_camera()
stage.key_from_view(*C.EYE)
GO_AT, GO_SPAN, ASK_AT, BUILD_AT = 0.8 * BEAT, 2.0, 3.2 * BEAT, 4.3 * BEAT
print('[body1-3] %d비트 · %.2f초 · 부탁 %.2f초 · 집 %.2f초' % (BEATS, DUR, ASK_AT, BUILD_AT))


def draw(fi):
    t = fi / C.FPS
    for i, arm in enumerate(arms):
        if i in (C.CARPENTER, C.HOMELESS):
            continue
        stage.pose(arm, motions.MOTIONS[C.CAST[i][0]](t))
        arm.location = HOME[i]

    # ②③ 다가간다 → ④ 손을 뻗는다
    near = (C.CARP_HOME[0] - 0.62, C.CARP_HOME[1] - 0.72)
    p, wt = C.walk_to(arms[C.HOMELESS], HOME[C.HOMELESS], near, t, GO_AT, GO_SPAN)
    if t < ASK_AT:
        C.walk_pose(arms[C.HOMELESS], wt)
    else:
        stage.pose(arms[C.HOMELESS], motions.reach(t - ASK_AT))

    # ⑤⑥ 목수가 짓는다
    carp = arms[C.CARPENTER]
    carp.location = HOME[C.CARPENTER]
    stage.pose(carp, motions.hammer(t) if t > BUILD_AT else motions.stop(t))
    k = C.ease((t - BUILD_AT) / (2.0 * BEAT))
    house.scale.z = max(BASE_SZ * k, 1e-4)
    house.location.z = BASE_Z * k
    house.hide_render = k <= 0.001

    # 거리 막대 — **문턱 아래**다
    em = C.emphasis(LINES, t, FOCUS)
    instrument.gauge_emph(G, em.get(0, 1.0))
    instrument.gauge_at(G, p[0] + RIGHT[0] * 0.24, p[1] + RIGHT[1] * 0.24, 1.0)
    C.dist_set(G, p, tuple(carp.location)[:2])
    C.dist_show(G, t > 1.6 * BEAT)

    u = t / DUR
    (sx, sy, sz), at = C.EYE
    stage.aim(cam, (sx - 0.9 * u, sy + 0.5 * u, sz), at)
    village.flicker(fire, t)


stage.bake(OUT, NF, C.FPS, draw, 'body1-3')
