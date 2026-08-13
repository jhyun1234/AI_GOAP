"""ep15s-3 본문3 — 5 비트. **닿지를 못한다. 그래서 아무 일도 안 일어난다.**

  ① 눈높이. 집 없는 주민이 목수가 간 쪽을 본다
  ②③ 그쪽으로 몇 걸음 걷다 **멈춰 선다**
  ④ 막대는 끝까지 차 있다(문턱 훨씬 위)
  ⑤ 그 자리에 주저앉는다

🔑 거절당한 것이 아니라 **닿지를 못하는** 것이다 — 그래서 사건이 「멈춤」이다.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import instrument, motions, stage, village

SHOT, SID = 'body3', 'S3'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 5
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

v, arms, fire, _f = C.build()
HOME = [tuple(a.location) for a in arms]
arms[C.CARPENTER].hide_render = True
G = C.dist_gauge()
RIGHT = C.cam_right(C.CLOSE[0], C.CLOSE[1])
cam = stage.light_camera()
stage.key_from_view(*C.CLOSE)
GO_AT, GO_SPAN, SIT_AT = 0.8 * BEAT, 1.15, 3.1 * BEAT
print('[body3-3] %d비트 · %.2f초 · 주저앉음 %.2f초' % (BEATS, DUR, SIT_AT))


def draw(fi):
    t = fi / C.FPS
    for i, arm in enumerate(arms):
        if i in (C.CARPENTER, C.HOMELESS):
            continue
        stage.pose(arm, motions.MOTIONS[C.CAST[i][0]](t))
        arm.location = HOME[i]

    # ②③ 몇 걸음 걷다 멈춘다 → ⑤ 주저앉는다
    tgt = (HOME[C.HOMELESS][0] + 2.6 * math.cos(C.CARP_AWAY),
           HOME[C.HOMELESS][1] + 2.6 * math.sin(C.CARP_AWAY))
    p, wt = C.walk_to(arms[C.HOMELESS], HOME[C.HOMELESS], tgt, t, GO_AT, GO_SPAN)
    if t < SIT_AT:
        C.walk_pose(arms[C.HOMELESS], wt)
    else:
        k = C.ease((t - SIT_AT) / 0.9)
        stage.pose(arms[C.HOMELESS],
                   motions.blend(motions.look_around(t - GO_AT), motions.rest(t - SIT_AT), k))

    # ④ 막대는 끝까지 차 있다 — 목수는 화면 밖 9.9m(38타일)에 있다
    far = (C.CARP_HOME[0] + C.FAR_TILE * C.TILE * math.cos(C.CARP_AWAY),
           C.CARP_HOME[1] + C.FAR_TILE * C.TILE * math.sin(C.CARP_AWAY))
    instrument.gauge_at(G, p[0] + RIGHT[0] * 0.26, p[1] + RIGHT[1] * 0.26, 1.0)
    C.dist_set(G, p, far)
    C.dist_show(G, True)

    u = t / DUR
    (sx, sy, sz), at = C.CLOSE
    stage.aim(cam, (sx + 0.45 * u, sy - 0.30 * u, sz + 0.12 * u), at)
    village.flicker(fire, t)


stage.bake(OUT, NF, C.FPS, draw, 'body3-3')
