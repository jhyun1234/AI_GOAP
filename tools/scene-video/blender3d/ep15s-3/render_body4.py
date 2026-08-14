"""ep15s-3 본문4 — 10 비트 · 14.37 초. **판정이 아니라 자리를 고쳤다.**

자막 세 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~3.96 「반경을 늘리면, 맵 반대편끼리 허공에 대고 말해요」
     ① **주민 클로즈업** — 손짓을 하며 **말한다**(`talk`). 앞은 비어 있다
     ② 컷. **저 멀리 목수 클로즈업** — 그 말을 받아 **고개를 끄덕인다**(`nod`)
     🔑 둘이 같은 프레임에 없다는 것이 이 줄의 뜻이다. 서로 안 보이는데 대화가 성립한다 —
        그게 「허공에 대고 말한다」이고, 그래서 여기만 컷 둘로 갈라 놓았다
  4.09~9.08 「그래서 판정이 아니라 자리를 고쳤어요. 목수 집을 8타일 안으로」
     컷. 넓게. 목수가 **마을 안으로 뛰어 들어와** 주민들 곁에 서고, 망치질을 해
     **제 집이 그 자리에 자라 오른다.** 거리 막대가 문턱 아래로 내려온다
  9.21~13.84 「그래도 정주형 목수는 더 가까워서, 성격 차이는 살아 있고요」
     **더 가까운 자리**에 집이 하나 더 앉는다. 주민이 목수에게 다가가 손을 뻗고 —
     이번엔 6타일 원 **안**이라 부탁이 걸린다

🔴 앞 판은 첫 줄을 아예 안 그렸다(「안 한 안이라 화면에 안 그린다」). 그런데 자막이 그
   장면을 묘사하고 있어서, 화면에는 대신 **자막과 무관한 되돌아옴**이 3.96 초 동안 돌았다.
   말하는 사람이 있어야 「허공에 대고 말한다」가 보인다(2026-08-13 사용자 판정).
🔴 컷마다 키라이트를 그 컷의 시선에 맞춘다 — 다만 **컷 안에서는 안 움직인다**(해가 돈다).
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

v, arms, fire, _out = C.build()
HOME = [tuple(a.location) for a in arms]
carp, homeless = arms[C.CARPENTER], arms[C.HOMELESS]
HAMMER = stage.hold(carp, village.hammer(), 'hand.R', (0.0, 0.02, 0.0))
G = C.dist_gauge()
RING = instrument.ring(C.REACH_R)
LINES = stage.shot_lines(C.EP, SID)
FOCUS = {1: 0, 2: 0}                    # 「8타일 안으로」·「성격 차이는 살아 있고요」
near_house = village.house(C.NEAR_HOUSE[0], C.NEAR_HOUSE[1], -18)
home_house = village.house(C.HOMEBODY_HOUSE[0], C.HOMEBODY_HOUSE[1], C.HOMEBODY_ROT)
cam = stage.light_camera()

# ── 컷 셋 ─────────────────────────────────────────────
# 🔴 첫 줄만 컷 둘로 갈린다. 나머지는 한 컷이고 그 안에서만 천천히 움직인다.
# 🔴 클로즈업은 **말하는 쪽 앞에 선다.** 첫 판은 둘 다 뒤에서 잡아 뒤통수만 나왔고,
#    그러면 손짓도 끄덕임도 안 보인다 — 클로즈업으로 잡은 뜻이 통째로 사라진다.
#    그래서 카메라를 각자가 **보고 있는 방향 쪽**에 세웠다(주민은 북을 보므로 북에서,
#    목수는 남을 보므로 남에서). 두 컷의 시선이 서로 맞물려 「주고받는다」가 된다.
# 🔴 거리는 **재서 정했다** — 42mm 로 2.0m 면 화면 폭 1.7m 라 사람이 세로의 70% 를 채운다.
#    2.4m 판은 24% 라 클로즈업이 아니라 그냥 먼 컷이었다.
# 🔴 목수 쪽 카메라를 그가 **정면으로 보는 자리**에 두면 집 (0.2, 4.0) 안이 된다 —
#    첫 판이 그랬고 화면이 통째로 지붕이었다. 45° 옆으로 비켜 세워 소품을 피한다.
# 🔴 높이도 **하늘 넓이로** 정했다 — 눈높이에 두면 위 28% 가 빈 하늘이다(프레임 95).
TALK_CAM = ((0.03, -0.75, 1.20), (-0.35, -2.05, 0.75))           # 주민 클로즈업(앞에서)
NOD_CAM = ((C.FAR_SETTLE[0] - 1.73, C.FAR_SETTLE[1] - 1.00, 1.25),       # 목수 클로즈업(4분의 3 앞)
           (C.FAR_SETTLE[0], C.FAR_SETTLE[1], 0.72))
CUT_B = 2.05                                     # 주민 → 목수
CUT_C = C.line_at(SID, 1) - 0.10                 # 목수 → 넓게 (둘째 줄 직전)
LINE3 = C.line_at(SID, 2)

RUN_AT = CUT_B + 1.15                            # 끄덕인 뒤 **바로** 돌아서서 온다
RUN_SPAN = math.hypot(C.PULLED[0] - C.FAR_SETTLE[0],
                      C.PULLED[1] - C.FAR_SETTLE[1]) / motions.RUN_SPEED
ARRIVE = RUN_AT + RUN_SPAN
BUILD_AT = ARRIVE + 0.15
NEAR_STEP = 1.10                                 # 목수 앞 1.10m — 6타일(1.56) **안**이다
assert NEAR_STEP < C.REACH_R, '들어서도 반경 밖이다'
_dx, _dy = C.PULLED[0] - HOME[C.HOMELESS][0], C.PULLED[1] - HOME[C.HOMELESS][1]
_d = math.hypot(_dx, _dy)
STEP_TO = (C.PULLED[0] - _dx / _d * NEAR_STEP, C.PULLED[1] - _dy / _d * NEAR_STEP)
STEP_AT = LINE3 + 0.35                           # 주민이 반경 안으로 들어서는 걸음
STEP_SPAN = (_d - NEAR_STEP) / motions.WALK_SPEED   # 🔴 걸음이 시간을 정한다(손으로 안 적는다)
ASK_AT = STEP_AT + STEP_SPAN
assert ASK_AT + motions.REACH_DUR < DUR, '부탁이 샷 밖으로 나간다: %.2f' % ASK_AT
RIGHT = C.cam_right(C.WIDE[0], C.WIDE[1])       # 🔴 막대가 눕지 않으려면 **그 컷의** 오른쪽
print('[body4-3] %d비트 · %.2f초 · 컷 %.2f/%.2f · 도착 %.2f · 부탁 %.2f'
      % (BEATS, DUR, CUT_B, CUT_C, ARRIVE, ASK_AT))
assert ARRIVE < LINE3, '「8타일 안으로」가 끝나도 목수가 안 왔다: %.2f' % ARRIVE


def view(t):
    """이 시각의 컷. 반환 (기준 loc, 기준 at, 지금 loc, 지금 at).
    🔑 키라이트는 **기준** 시선으로 맞춘다 — 컷 안에서 카메라가 움직여도 해가 안 돈다."""
    if t < CUT_B:
        (sx, sy, sz), at = TALK_CAM
        u = C.ease(t / CUT_B)
        return TALK_CAM[0], at, (sx + 0.22 * u, sy + 0.30 * u, sz - 0.05 * u), at
    if t < CUT_C:
        (sx, sy, sz), at = NOD_CAM
        u = C.ease((t - CUT_B) / (CUT_C - CUT_B))
        return NOD_CAM[0], at, (sx - 0.25 * u, sy - 0.35 * u, sz + 0.10 * u), at
    (sx, sy, sz), (ax, ay, az) = C.WIDE
    u = C.ease((t - CUT_C) / (DUR - CUT_C))
    return C.WIDE[0], C.WIDE[1], \
        (sx - 0.85 * u, sy + 0.95 * u, sz - 0.45 * u), (ax - 0.35 * u, ay - 0.60 * u, az)


def draw(fi):
    t = fi / C.FPS
    C.others(arms, t, HOME, watch=C.PULLED, watch_at=ARRIVE - 0.5)

    # ── 목수 — 저 밖에서 끄덕이고, 마을 안으로 뛰어 들어와, 짓는다
    p_c, cwt = C.walk_to(carp, C.FAR_SETTLE, C.PULLED, t, RUN_AT, RUN_SPAN, run=True)
    if t < RUN_AT:
        carp.location = (C.FAR_SETTLE[0], C.FAR_SETTLE[1], 0)
        C.face(carp, HOME[C.HOMELESS], C.FAR_SETTLE)          # 🔑 말하는 쪽을 **본다**
        stage.pose(carp, motions.stop(t) if t < CUT_B else
                   motions.blend(motions.stop(t), motions.nod(t - CUT_B),
                                 C.ease((t - CUT_B) / 0.30)))
    elif t < BUILD_AT:
        C.walk_pose(carp, cwt, run=True)
    else:
        C.face(carp, C.NEAR_HOUSE, C.PULLED)
        stage.pose(carp, motions.blend(motions.run(cwt), motions.hammer(t - BUILD_AT),
                                       C.ease((t - BUILD_AT) / 0.28)))

    # ── 집 없는 주민 — 말한다 → 지켜본다 → 반경 안으로 들어서서 **부탁한다**
    p_h, hwt = C.walk_to(homeless, HOME[C.HOMELESS], STEP_TO, t, STEP_AT, STEP_SPAN)
    if t < CUT_B:
        homeless.location = HOME[C.HOMELESS]
        C.face(homeless, C.FAR_SETTLE, HOME[C.HOMELESS])      # 🔑 저 멀리 대고 말한다
        stage.pose(homeless, motions.talk(t))
    elif t < STEP_AT:
        homeless.location = HOME[C.HOMELESS]
        C.face(homeless, C.PULLED if t > ARRIVE - 1.0 else C.FAR_SETTLE, HOME[C.HOMELESS])
        stage.pose(homeless, motions.blend(motions.talk(t), motions.stop(t),
                                           C.ease((t - CUT_B) / 0.40)))
    elif t < ASK_AT:
        C.walk_pose(homeless, hwt)
    else:
        C.face(homeless, C.PULLED, p_h)
        ask = motions.blend(motions.reach(t - ASK_AT), motions.talk(t - ASK_AT),
                            C.ease((t - ASK_AT - motions.REACH_DUR) / 0.35))
        stage.pose(homeless, motions.blend(motions.walk(hwt), ask,
                                           C.ease((t - ASK_AT) / 0.26)))

    # ── 6타일 원은 목수를 따라온다. 이번엔 주민이 그 **안**으로 들어선다
    instrument.ring_at(RING, p_c[0] if t > RUN_AT else C.FAR_SETTLE[0],
                       p_c[1] if t > RUN_AT else C.FAR_SETTLE[1])
    instrument.ring_show(RING, t > CUT_C)

    # ── 집 둘 — 떠돌이 목수의 집, 그리고 **더 가까운** 눌러사는 목수의 집
    village.house_grow(near_house, C.ease((t - BUILD_AT) / 1.60))
    village.house_grow(home_house, C.ease((t - LINE3 - 1.40) / 1.60))

    # ── 거리 막대
    em = C.emphasis(LINES, t, FOCUS)
    instrument.gauge_emph(G, em.get(0, 1.0))
    C.gauge_over(G, p_h, RIGHT)
    C.dist_set(G, p_h, p_c if t > RUN_AT else C.FAR_SETTLE)
    C.dist_show(G, t > CUT_C)

    base_loc, base_at, loc, at = view(t)
    stage.key_from_view(base_loc, base_at)
    C.lens(cam, C.CLOSE_LENS if t < CUT_C else C.WIDE_LENS)
    stage.aim(cam, loc, at)
    village.flicker(fire, t)


stage.bake(OUT, NF, C.FPS, draw, 'body4-3')
