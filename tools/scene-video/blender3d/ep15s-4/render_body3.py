"""ep15s-4 본문3 — 5 비트 · 5.51 초. **불은 늘고, 집 자리는 비어 있다.**

자막 두 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~2.55 「모닥불이 부탁보다 급한 일이라,」
     넓게. 불밭이 이미 여섯이다. 목수는 저쪽 끝에서 **아직도 하나를 더 짓고 있다**
  2.68~4.98 「부탁받은 집은 시작도 못 해요」
     카메라가 반대쪽으로 흘러 **빈 집터**를 잡는다. 아무것도 없고, 부탁한 주민이
     그 옆에 서서 목수 쪽을 본다

🔑 크기 판단: 이 줄의 뜻은 **두 자리의 대비**다 — 저쪽은 불이 넘치고 이쪽은 비어 있다.
   컷으로 자르면 장소 둘이 되고 대비가 죽는다. 그래서 **한 번에 훑는다.**
🔴 「시작도 못 한다」를 표시로 그리지 않는다. 빈 땅과 **기다리는 사람**이 그 말이다.
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
LENS = C.PLOT_LENS

N = 9
v, arms, FIRES, SPOTS = C.build(N)
HOME = [tuple(a.location) for a in arms]
carp, asker = arms[C.CARP], arms[C.ASKER]
HAMMER = stage.hold(carp, village.hammer(), 'hand.R', (0.0, 0.02, 0.0))
RING = C.plot_ring()
cam = stage.light_camera()
C.lens(cam, LENS)

BASE = 5
ORDER = [5, 6]
START = (SPOTS[4][0] + 0.62, SPOTS[4][1] - 0.30)
PAN_AT = C.line_at(SID, 1) - 0.35
# 🔑 넓게 시작해 빈 집터로 흐른다. 컷이 아니다 — 같은 마을이라는 것이 이 샷의 뜻이다
CAM_A = ((2.30, -8.00, 3.15), (-3.00, -1.20, 0.90))
CAM_B = C.PLOTCAM
stage.key_from_view(*CAM_A)
PLAN = C.plan_builds(SPOTS, START, ORDER, 0.10)
print('[body3-4] %d비트 · %.2f초 · 흐름 %.2f초부터' % (BEATS, DUR, PAN_AT))


def draw(fi):
    t = fi / C.FPS
    C.others(arms, t, HOME)

    # ── 목수는 저쪽 끝에서 아직도 짓는다 ──
    p, done, growing, grow = C.run_builds(carp, PLAN, t)
    C.fire_all(FIRES, BASE + done, t, growing, grow)

    # ── 부탁한 주민 — 빈 집터 옆에서 **목수 쪽을 보며 기다린다** ──
    asker.location = HOME[C.ASKER]
    C.face(asker, p, HOME[C.ASKER][:2])
    stage.pose(asker, motions.blend(motions.stop(t), motions.look_around(t),
                                    C.ease((t - PAN_AT) / 0.7)))

    # 🔑 집이 설 자리를 땅에 그린다 — 빈 땅만으로는 「시작도 못 했다」가 안 읽힌다
    instrument.ring_at(RING, C.PLOT[0], C.PLOT[1])
    instrument.ring_show(RING, t > PAN_AT - 0.5)

    u = C.ease((t - PAN_AT) / max(DUR - PAN_AT, 0.01))
    loc = tuple(a + (b - a) * u for a, b in zip(CAM_A[0], CAM_B[0]))
    at = tuple(a + (b - a) * u for a, b in zip(CAM_A[1], CAM_B[1]))
    stage.aim(cam, loc, at)
    C.flicker_home(t)


stage.bake(OUT, NF, C.FPS, draw, 'body3-4')
