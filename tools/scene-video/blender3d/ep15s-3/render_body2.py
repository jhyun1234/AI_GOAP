"""ep15s-3 본문2 — 7 비트. **목수가 걸어 나가고, 막대가 문턱을 넘는다.**

  ①② 목수가 마을을 등지고 걸어 나간다
  ③④ 거리 막대가 차오른다 — **6타일 선을 넘는 순간**이 이 편의 사건이다
  ⑤⑥ 목수가 프레임 밖으로 사라진다
  ⑦ 남은 것은 빈자리와 꽉 찬 막대

🔴 38타일은 화면에 못 담는다(마을이 10m 대다). 그래서 「멀다」를 **화면에서 사라지는 것**과
   **막대가 끝까지 차는 것** 둘로 말한다 — 수는 자막이 부른다.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import instrument, motions, stage, village

SHOT, SID = 'body2', 'S2'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 7
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

v, arms, fire, _f = C.build()
HOME = [tuple(a.location) for a in arms]
G = C.dist_gauge()
RIGHT = C.cam_right(C.DEV[0], C.DEV[1])
LINES = stage.shot_lines(C.EP, SID)
FOCUS = {0: 0, 1: 0}                      # 두 줄 다 거리를 부른다(38타일 · 그때부터)
cam = stage.light_camera()
stage.key_from_view(*C.DEV)
GO_AT = 0.7 * BEAT
print('[body2-3] %d비트 · %.2f초 · 목수가 %.2f초에 떠난다' % (BEATS, DUR, GO_AT))


def draw(fi):
    t = fi / C.FPS
    for i, arm in enumerate(arms):
        if i == C.CARPENTER:
            continue
        stage.pose(arm, motions.MOTIONS['stop' if i == C.HOMELESS else C.CAST[i][0]](t))
        arm.location = HOME[i]

    # ①② 목수가 걸어 나간다 — 나아가는 거리는 걸음이 만든다
    carp = arms[C.CARPENTER]
    wt = max(0.0, t - GO_AT)
    d = motions.WALK_SPEED * wt
    cx = C.CARP_HOME[0] + d * math.cos(C.CARP_AWAY)
    cy = C.CARP_HOME[1] + d * math.sin(C.CARP_AWAY)
    carp.location = (cx, cy, 0)
    carp.rotation_euler = (0, 0, C.CARP_AWAY + math.pi)
    C.walk_pose(carp, wt)

    # ③④ 거리 막대가 문턱을 넘는다
    em = C.emphasis(LINES, t, FOCUS)
    instrument.gauge_emph(G, em.get(0, 1.0))
    hp = HOME[C.HOMELESS]
    instrument.gauge_at(G, hp[0] + RIGHT[0] * 0.26, hp[1] + RIGHT[1] * 0.26, 1.0)
    tiles = C.dist_set(G, hp, (cx, cy))
    C.dist_show(G, True)

    # ⑤⑥ 화면 밖으로. 🔑 사라지는 것 자체가 「38타일」이다
    carp.hide_render = math.hypot(cx, cy) > 6.4

    u = t / DUR
    (sx, sy, sz), at = C.DEV
    stage.aim(cam, (sx + 0.9 * u, sy - 0.6 * u, sz + 0.8 * u), at)
    village.flicker(fire, t)


stage.bake(OUT, NF, C.FPS, draw, 'body2-3')
