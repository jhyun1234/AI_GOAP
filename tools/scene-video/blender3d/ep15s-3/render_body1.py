"""ep15s-3 본문1 — 6 비트 · 7.55 초. **부탁은 다가가야 걸린다.**

자막 두 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~3.60 「집이 없는 주민은 목수한테 지어 달라고 부탁해요」
     주민이 목수 앞까지 **걸어가서**, 손을 뻗고 **말한다**(`talk`). 목수가 **끄덕인다**(`nod`)
  3.73~7.02 「그 부탁은 6타일 안에서 마주쳐야 걸리고요」
     목수 발밑에 **6타일 원**이 그려지고, 주민은 그 **안에** 서 있다. 거리 막대는 문턱 아래.
     받아들인 목수가 망치질을 시작하고 **집이 땅에서 자라 오른다**

🔴 **집 지붕이 먼저 떠 있던 버그를 고쳤다**(2026-08-13 사용자 판정). 원인은 이 파일이
   아니라 `village.house()` 였다 — 지붕을 만들고 **반환하지 않아서** 몸통만 켜고 껐다.
   이제 `village.house_grow(집, k)` 하나가 조각 전부를 같이 세운다.
🔴 카메라와 마을을 넓혔다. 「6타일 안」은 **거리 이야기**라 화면에 거리가 있어야 한다.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import instrument, motions, stage, village

SHOT, SID = 'body1', 'S1'
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
RING = instrument.ring(C.REACH_R)
RIGHT = C.cam_right(C.WIDE[0], C.WIDE[1])
LINES = stage.shot_lines(C.EP, SID)
FOCUS = {1: 0}                                   # 「6타일 안에서 마주쳐야」 — 거리 막대
house = village.house(C.HOUSE_AT[0], C.HOUSE_AT[1], -18)
cam = stage.light_camera()
C.lens(cam, C.WIDE_LENS)
stage.key_from_view(*C.WIDE)

# 🔴 자리도 시각도 **재서 낸다.** 「걸어가서 부탁한다」는 걷는 시간이 대본 줄 안에 들어가야
#    성립한다 — 자리를 눈대중으로 적으면 말이 끝난 뒤에 도착하거나 도착 전에 손을 뻗는다.
STOP_GAP = 1.15                                   # 목수 앞 어디까지 다가가나(반경 1.56 안)
_dx, _dy = C.CARP_HOME[0] - HOME[C.HOMELESS][0], C.CARP_HOME[1] - HOME[C.HOMELESS][1]
_d = math.hypot(_dx, _dy)
NEAR = (C.CARP_HOME[0] - _dx / _d * STOP_GAP, C.CARP_HOME[1] - _dy / _d * STOP_GAP)
assert STOP_GAP < C.REACH_R, '부탁하는 자리가 6타일 밖이다'
GO_AT = 0.05
GO_SPAN = (_d - STOP_GAP) / motions.WALK_SPEED    # 걸음이 만드는 시간 — 손으로 적지 않는다
ASK_AT = GO_AT + GO_SPAN                          # 다 걸어간 순간 손을 뻗는다
assert ASK_AT < C.line_at(SID, 1), '「부탁해요」가 끝난 뒤에 손을 뻗는다: %.2f' % ASK_AT
RING_AT = C.line_at(SID, 1)                       # 「6타일 안에서」 하는 그 순간 원이 뜬다
NOD_AT = RING_AT + 0.55
BUILD_AT = RING_AT + 1.35
print('[body1-3] %d비트 · %.2f초 · 부탁 %.2f · 원 %.2f · 집 %.2f'
      % (BEATS, DUR, ASK_AT, RING_AT, BUILD_AT))


def draw(fi):
    t = fi / C.FPS
    C.others(arms, t, HOME)

    # ① 주민이 목수 앞까지 걸어가 ② 손을 뻗고 ③ **말한다**
    p, wt = C.walk_to(homeless, HOME[C.HOMELESS], NEAR, t, GO_AT, GO_SPAN)
    if t < ASK_AT:
        C.walk_pose(homeless, wt)
    else:
        C.face(homeless, C.CARP_HOME, p)
        # 🔴 걷기에서 곧장 뻗기로 넘기면 다리가 한 프레임에 차렷으로 순간이동한다
        ask = motions.reach(t - ASK_AT)
        # 🔴 뻗기만 하면 「가리켰다」다. 부탁은 **말**이라 뻗은 뒤 계속 오가야 한다
        ask = motions.blend(ask, motions.talk(t - ASK_AT),
                            C.ease((t - ASK_AT - motions.REACH_DUR) / 0.35))
        stage.pose(homeless, motions.blend(motions.walk(wt), ask,
                                           C.ease((t - ASK_AT) / 0.26)))

    # ④ 목수 — 듣는다 → **끄덕인다** → 망치질
    carp.location = (C.CARP_HOME[0], C.CARP_HOME[1], 0)
    C.face(carp, NEAR, C.CARP_HOME)               # 🔑 부탁하는 사람을 **본다**
    if t < NOD_AT:
        stage.pose(carp, motions.stop(t))
    elif t < BUILD_AT:
        stage.pose(carp, motions.blend(motions.stop(t), motions.nod(t - NOD_AT),
                                       C.ease((t - NOD_AT) / 0.30)))
    else:
        C.face(carp, C.HOUSE_AT, C.CARP_HOME)     # 지을 자리를 보고 친다
        stage.pose(carp, motions.blend(motions.nod(t - NOD_AT), motions.hammer(t - BUILD_AT),
                                       C.ease((t - BUILD_AT) / 0.30)))

    # ⑤ 6타일 원 — 「어디까지가 안인가」. 막대는 「지금 얼마나 먼가」이고 둘은 다른 말이다
    instrument.ring_at(RING, C.CARP_HOME[0], C.CARP_HOME[1])
    instrument.ring_show(RING, t > RING_AT)

    # ⑥ 거리 막대 — **문턱 아래**다
    em = C.emphasis(LINES, t, FOCUS)
    instrument.gauge_emph(G, em.get(0, 1.0))
    C.gauge_over(G, p, RIGHT)
    C.dist_set(G, p, C.CARP_HOME)
    C.dist_show(G, t > 1.2 * BEAT)

    # ⑦ 집이 **땅에서 자라 오른다** — 지붕까지 같이 (village.house_grow)
    village.house_grow(house, C.ease((t - BUILD_AT) / (1.9 * BEAT)))

    u = t / DUR
    (sx, sy, sz), (ax, ay, az) = C.WIDE
    stage.aim(cam, (sx - 1.05 * u, sy + 1.30 * u, sz - 0.55 * u),
              (ax - 0.35 * u, ay - 1.05 * u, az - 0.10 * u))
    village.flicker(fire, t)


stage.bake(OUT, NF, C.FPS, draw, 'body1-3')
