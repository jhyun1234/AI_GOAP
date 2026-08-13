"""ep15s-3 본문2 — 6 비트 · 7.34 초. **목수가 저 멀리서 혼자 집을 짓는다.**

자막 두 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~4.13 「떠돌이 성격인 목수는 38타일 밖에 집을 지었어요」
     한 프레임에 **둘 다** 있다 — 왼쪽 아래 모여 일하는 다섯, 오른쪽 위 **혼자** 있는 목수.
     목수가 망치질을 하고 **그 자리에 제 집이 자라 오른다**
  4.26~6.82 「그때부터 부탁이 한 번도 안 걸렸죠」
     집 없는 주민이 그쪽으로 몇 걸음 가다 **멈춘다.** 목수 발밑 6타일 원이 저 멀리 떠 있고
     주민은 그 **밖**이다. 거리 막대는 문턱을 한참 넘어 끝까지 차 있다

🔴 앞 판은 목수를 화면 밖으로 걸려 보내고 `hide_render` 로 지웠다. 그러면 「멀리서 혼자
   지었다」가 아니라 **아무 일도 안 일어난 빈 마을**이 된다(2026-08-13 사용자 판정).
   38 타일까지 실제로 갈 필요는 없다 — **무리와 확실히 떨어져 혼자 짓는 것**이면 된다.
🔴 그래서 카메라가 이 회차에서 가장 넓다(`C.FAR`). 둘을 한 프레임에 못 넣으면 「떨어졌다」는
   컷 두 개로 나뉘고, 나뉘는 순간 그건 거리가 아니라 장소 둘이 된다.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import instrument, motions, stage, village

SHOT, SID = 'body2', 'S2'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 6
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

v, arms, fire, _out = C.build()
HOME = [tuple(a.location) for a in arms]
carp, homeless = arms[C.CARPENTER], arms[C.HOMELESS]
HAMMER = stage.hold(carp, village.hammer(), 'hand.R', (0.0, 0.02, 0.0))
G = C.dist_gauge()
RIGHT = C.cam_right(C.FAR[0], C.FAR[1])
LINES = stage.shot_lines(C.EP, SID)
FOCUS = {0: 0, 1: 0}                      # 두 줄 다 거리를 부른다(38타일 · 그때부터)

# 🔑 목수의 새 집은 그가 선 자리에서 **한 걸음 더 바깥**이다(common 이 잰다) — 사람과 집이
#    겹치면 실루엣이 한 덩어리가 되어 「혼자」가 안 읽힌다.
FAR_HOUSE = C.FAR_HOUSE
house = village.house(FAR_HOUSE[0], FAR_HOUSE[1], 152)
cam = stage.light_camera()
C.lens(cam, C.FAR_LENS)
stage.key_from_view(*C.FAR)

BUILD_AT = 0.55                                   # 첫 줄이 「집을 지었어요」로 가는 동안 짓는다
STOP_LINE = C.line_at(SID, 1)                     # 「그때부터 … 안 걸렸죠」
GO_AT, GO_SPAN = STOP_LINE - 0.35, 1.30           # 몇 걸음 나섰다 **멈춘다**
print('[body2-3] %d비트 · %.2f초 · 목수 (%.2f, %.2f) · 집 %.2f초'
      % (BEATS, DUR, C.FAR_SETTLE[0], C.FAR_SETTLE[1], BUILD_AT))


def draw(fi):
    t = fi / C.FPS
    C.others(arms, t, HOME)

    # ① 목수는 저 멀리 혼자 있다 — **망치질을 하고 집이 자라 오른다**
    carp.location = (C.FAR_SETTLE[0], C.FAR_SETTLE[1], 0)
    C.face(carp, FAR_HOUSE, C.FAR_SETTLE)
    stage.pose(carp, motions.hammer(t - BUILD_AT) if t > BUILD_AT else motions.stop(t))
    village.house_grow(house, C.ease((t - BUILD_AT) / (2.6 * BEAT)))

    # ② 집 없는 주민 — 그쪽으로 몇 걸음 나섰다 **멈춰 선다.** 닿지를 못한다
    tgt = (HOME[C.HOMELESS][0] + 2.2 * math.cos(C.CARP_AWAY),
           HOME[C.HOMELESS][1] + 2.2 * math.sin(C.CARP_AWAY))
    p, wt = C.walk_to(homeless, HOME[C.HOMELESS], tgt, t, GO_AT, GO_SPAN)
    if t < GO_AT:
        C.face(homeless, C.FAR_SETTLE, HOME[C.HOMELESS])
        stage.pose(homeless, motions.stop(t))
    elif t < GO_AT + GO_SPAN:
        C.walk_pose(homeless, wt)
    else:
        # 🔑 멈춰 서서 **그쪽을 본다.** 이 샷의 사건은 「갔다」가 아니라 「못 간다」다
        C.face(homeless, C.FAR_SETTLE, p)
        stage.pose(homeless, motions.blend(motions.walk(wt),
                                           motions.look_around(t - GO_AT - GO_SPAN),
                                           C.ease((t - GO_AT - GO_SPAN) / 0.30)))

    # ❌ **6타일 원을 여기서는 안 그린다.** 그려 봤는데 목수가 15m 밖이라 원이 지름
    #    30 픽셀짜리 점선 조각이 되고, 게다가 마을 집 뒤로 반이 가려서 「땅에 그린 원」이
    #    아니라 **공중에 뜬 점선**으로 읽혔다(프레임 130 에서 잡았다). 이 샷에서 「멀다」는
    #    막대와 문턱 표시가 지고, 원은 가까운 샷(본문1·4)에서만 쓴다.

    # ④ 거리 막대 — 문턱을 한참 넘어 끝까지 찬다
    em = C.emphasis(LINES, t, FOCUS)
    instrument.gauge_emph(G, em.get(0, 1.0))
    C.gauge_over(G, p, RIGHT)
    C.dist_set(G, p, C.FAR_SETTLE)
    C.dist_show(G, True)

    u = t / DUR
    (sx, sy, sz), (ax, ay, az) = C.FAR
    stage.aim(cam, (sx + 0.85 * u, sy - 0.70 * u, sz + 0.55 * u),
              (ax + 0.30 * u, ay + 0.45 * u, az))
    village.flicker(fire, t)


stage.bake(OUT, NF, C.FPS, draw, 'body2-3')
