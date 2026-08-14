"""ep15s-4 본문1 — 5 비트 · 6.23 초. **부탁을 받아 놓고, 돌아서서 딴 걸 짓는다.**

자막 두 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~3.34 「목수는 집을 지어 달라는 부탁을 받아 놨어요」
     주민이 목수 앞에서 손을 뻗고 **말한다**. 목수가 그를 보고 **고개를 끄덕인다** — 수락이다
  3.47~5.70 「그런데 자기 모닥불부터 짓죠」
     목수가 **돌아선다.** 주민을 등지고 옆 빈 땅으로 걸어가 모닥불을 짓기 시작한다.
     주민은 그 자리에 그대로 서 있다

🔑 크기 판단: 두 사람의 **주고받음**이 사건이라 둘이 같이 들어오는 중간 크기다.
   넓히면 끄덕임이 안 보이고, 붙이면 「돌아서서 간다」가 프레임 밖으로 나간다.
🔴 여기가 사슬의 시작이다 — 이 편은 **불이 하나도 없는 상태에서** 시작한다.
   훅에서 본 밭은 이 마디가 여러 번 돈 뒤의 모습이다.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import motions, stage, village

SHOT, SID = 'body1', 'S1'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 5
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT
LENS = C.MID_LENS

N = 9
v, arms, FIRES, SPOTS = C.build(N)
HOME = [tuple(a.location) for a in arms]
carp, asker = arms[C.CARP], arms[C.ASKER]
HAMMER = stage.hold(carp, village.hammer(), 'hand.R', (0.0, 0.02, 0.0))
cam = stage.light_camera()
C.lens(cam, LENS)

# 🔑 이 샷에서만 주민이 **목수 앞에 와 있다.** 부탁은 상태이지 사건이 아니라, 걸어오는 데
#    3.7초를 쓰면 정작 「받아 놨다」를 말할 자리가 없다(재 보고 뺐다).
ASK_AT_POS = (-3.45, -3.95)
NOD_AT = 1.55
TURN_AT = C.line_at(SID, 1) - 0.20        # 「그런데」에 몸이 먼저 돌아선다
ORDER = [0]
CAM = ((-2.05, -6.55, 1.95), (-3.85, -3.55, 0.85))
stage.key_from_view(*CAM)
PLAN = C.plan_builds(SPOTS, HOME[C.CARP][:2], ORDER, TURN_AT)
print('[body1-4] %d비트 · %.2f초 · 끄덕임 %.2f · 돌아섬 %.2f' % (BEATS, DUR, NOD_AT, TURN_AT))


def draw(fi):
    t = fi / C.FPS
    C.others(arms, t, HOME)
    C.fire_all(FIRES, 0, t)                # 아직 하나도 없다

    # ── 주민 — 손을 뻗고 말한다. 목수가 돌아선 뒤에도 그 자리에 있다 ──
    asker.location = (ASK_AT_POS[0], ASK_AT_POS[1], 0)
    C.face(asker, HOME[C.CARP], ASK_AT_POS)
    ask = motions.blend(motions.reach(t), motions.talk(t),
                        C.ease((t - motions.REACH_DUR) / 0.35))
    stage.pose(asker, motions.blend(ask, motions.stop(t), C.ease((t - TURN_AT) / 0.6)))

    # ── 목수 — 듣는다 → 끄덕인다 → **돌아서서 불을 지으러 간다** ──
    if t < TURN_AT:
        carp.location = HOME[C.CARP]
        C.face(carp, ASK_AT_POS, HOME[C.CARP][:2])
        stage.pose(carp, motions.stop(t) if t < NOD_AT else
                   motions.blend(motions.stop(t), motions.nod(t - NOD_AT),
                                 C.ease((t - NOD_AT) / 0.30)))
        C.fire_all(FIRES, 0, t)
    else:
        p, done, growing, grow = C.run_builds(carp, PLAN, t)
        C.fire_all(FIRES, done, t, growing, grow)

    u = t / DUR
    (sx, sy, sz), (ax, ay, az) = CAM
    stage.aim(cam, (sx - 0.35 * u, sy - 0.30 * u, sz + 0.18 * u),
              (ax - 0.45 * u, ay + 0.30 * u, az))
    C.flicker_home(t)


stage.bake(OUT, NF, C.FPS, draw, 'body1-4')
