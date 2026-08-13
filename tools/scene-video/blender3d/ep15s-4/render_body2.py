"""ep15s-4 본문2 — 7 비트 · 8.77 초. **불은 느는데 막대는 안 켜진다.**

자막 세 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~2.62 「다 지어도 그 불에 주인이 안 붙어요」
     목수가 불 하나를 **다 세운다.** 옆에 「내 불 있음」 막대가 뜨는데 **바닥 그대로**다
  2.75~5.33 「그래서 자기 불이 없는 걸로 남아요」
     막대가 커졌다 작아진다(대본이 그 값을 부른다). 여전히 바닥이다
  5.46~8.24 「없다고 세니까, 돌아서서 또 짓고요」
     그가 **돌아서서 옆으로 걸어가 또 짓는다.** 한 번, 두 번, 세 번 —
     불은 하나씩 느는데 **막대는 한 번도 안 올라간다**

🔑 **이 샷이 이 편의 심장이다.** 지은 개수와 「내 불 있음」이 한 프레임에 나란히 있어야
   무한 건축의 원인이 우선순위가 아니라 **셈**이라는 것이 보인다. 2D 판에는 이 자리가 없었다.
🔑 크기 판단: 막대가 읽히면서 그가 **옮겨 다니는 것**도 보여야 한다 — 중간 크기이고,
   그가 멀어지는 만큼 카메라가 아주 조금 물러난다.
🔴 같은 자리에서 같은 동작을 반복하면 정적 게이트에 걸리고 사람 눈에도 루프로 보인다.
   **옮겨 가며** 짓는 것이 되풀이를 되풀이로 보이게 한다.
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
LENS = C.MID_LENS

N = 9
v, arms, FIRES, SPOTS = C.build(N)
HOME = [tuple(a.location) for a in arms]
carp, asker = arms[C.CARP], arms[C.ASKER]
HAMMER = stage.hold(carp, village.hammer(), 'hand.R', (0.0, 0.02, 0.0))
G = C.own_gauge()
CAM = ((-1.95, -6.45, 2.05), (-4.95, -2.35, 0.88))
RIGHT = C.cam_right(CAM[0], CAM[1])
LINES = stage.shot_lines(C.EP, SID)
cam = stage.light_camera()
C.lens(cam, LENS)
stage.key_from_view(*CAM)

# 🔴 시작 개수를 1 로 뒀더니 샷이 끝나도 불이 둘뿐이라 「또 짓고, 또 지어요」가 안 읽혔다.
#    2 로 시작해 넷까지 간다 — 되풀이는 **늘어난 개수**로 보인다.
BASE = 2
DONE0 = SPOTS[1]                       # 앞서 세운 불 — 그 앞에서 시작한다
STAND0 = (DONE0[0] + 0.62, DONE0[1] - 0.30)
AGAIN = C.line_at(SID, 2) - 0.25       # 「돌아서서 또 짓고요」 직전에 몸이 돌아선다
ORDER = [2, 3, 4, 5]
GAUGE_IN = 0.55
PLAN = C.plan_builds(SPOTS, STAND0, ORDER, AGAIN)
print('[body2-4] %d비트 · %.2f초 · 되풀이 %.2f초부터 · 마디 %s'
      % (BEATS, DUR, AGAIN, ' '.join('%.2f' % (p[5] + C.STEP_HAMMER) for p in PLAN)))


def draw(fi):
    t = fi / C.FPS
    C.others(arms, t, HOME)
    asker.location = HOME[C.ASKER]
    asker.rotation_euler = (0, 0, C.home_face(C.ASKER))
    stage.pose(asker, motions.stop(t))

    if t < AGAIN:
        # ① 다 세운 불 앞에 서 있다 — 막대는 바닥이다
        carp.location = (STAND0[0], STAND0[1], 0)
        C.face(carp, DONE0, STAND0)
        stage.pose(carp, motions.stop(t))
        C.fire_all(FIRES, BASE, t)
        p = STAND0
    else:
        # ③ 돌아서서 또 짓는다 — 옮겨 가며
        p, done, growing, grow = C.run_builds(carp, PLAN, t)
        C.fire_all(FIRES, BASE + done, t, growing, grow)

    # 🔴 **막대는 한 번도 안 올라간다.** 이 샷에서 값이 바뀌지 않는 것이 사건이다
    # 🔴 **여기서는 강조를 안 건다.** 값이 한 번도 안 바뀌는데 강조를 걸면 빈 홈이
    #    커졌다 작아져서 **값이 오르내리는 것처럼** 보인다(프레임 40·180 에서 잡았다).
    instrument.gauge_emph(G, 1.0)
    C.gauge_at(G, carp, RIGHT, z=0.62, side=0.62)
    instrument.gauge_set(G, 0.0)
    C.gauge_show(G, t > GAUGE_IN)

    u = C.ease(t / DUR)
    (sx, sy, sz), (ax, ay, az) = CAM
    # 🔴 많이 흐르면 앞서 지은 불이 화면 밖으로 나가서 「늘었다」가 안 보인다
    stage.aim(cam, (sx - 0.25 * u, sy - 0.55 * u, sz + 0.35 * u),
              (ax - 0.15 * u, ay - 0.05 * u, az))
    C.flicker_home(t)


stage.bake(OUT, NF, C.FPS, draw, 'body2-4')
