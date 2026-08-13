"""ep15s-4 아웃트로 — 4 비트 · 4.36 초. **다음 편의 무대: 수는 다 통과인데 아무 일도 없다.**

자막 한 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~4.01 「다음 편에서는, 숫자는 다 성공인데 볼 게 없었어요」
     마을이 조용히 돌아간다 — 넷이 제 일을 하고 아무 사고도 안 난다.
     그 위로 막대가 하나씩 켜지며 **전부 끝까지 찬다.** 그런데 화면에서는 아무 일도 안 일어난다

🔑 이 편의 사건(무한 건축)은 이미 닫혔다. 여기서 새로 여는 것은 **다음 편의 물음** 하나다 —
   「다 통과인데 왜 볼 게 없나」. 그래서 막대를 다 채우고 **아무 사건도 안 준다.**
   비어 있는 것이 이 샷의 내용이다.
🔴 엔진이 이 샷 위에 아웃트로 카드를 얹는다 — 그림이 조용해야 카드가 읽힌다.
🔴 불밭은 **안 보여 준다.** 앞 샷에서 고쳤으므로 여기 남아 있으면 「안 고쳐졌다」가 된다.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import instrument, motions, stage, village

SHOT, SID = 'outro', 'SO'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 4
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT
LENS = 36

N = 9
v, arms, FIRES, SPOTS = C.build(N)
HOME = [tuple(a.location) for a in arms]
house = village.house(C.PLOT[0], C.PLOT[1], C.PLOT_ROT)     # 앞 샷에서 선 그 집
CAM_A = ((2.60, -7.40, 2.70), (-1.60, -0.90, 0.95))
CAM_B = ((1.70, -6.50, 2.45), (-1.90, -0.40, 0.95))
RIGHT = C.cam_right(CAM_A[0], CAM_A[1])
# 🔑 막대를 다는 넷 — 목수와 부탁한 주민을 포함해 **넷이면 「다 통과」가 읽힌다**
SHOWN = [C.CARP, C.ASKER, 2, 4]
G = {i: C.own_gauge() for i in SHOWN}
FILL_AT = [0.45 + 0.55 * n for n in range(len(SHOWN))]
cam = stage.light_camera()
C.lens(cam, LENS)
stage.key_from_view(*CAM_A)
print('[outro-4] %d비트 · %.2f초 · 막대 %d개' % (BEATS, DUR, len(SHOWN)))


def draw(fi):
    t = fi / C.FPS
    C.fire_all(FIRES, 0, t)                # 불밭은 없다 — 고쳐졌다
    village.house_grow(house, 1.0)

    # 🔑 목수도 이제 제 일을 한다. 여섯이 다 평범하게 돌아가는 것이 이 샷의 내용이다
    for i, arm in enumerate(arms):
        arm.location = HOME[i]
        arm.rotation_euler = (0, 0, C.home_face(i))
        job = 'hammer' if i == C.CARP else ('warm' if i == C.ASKER else C.CAST[i][0])
        stage.pose(arm, motions.MOTIONS[job](t))

    for n, i in enumerate(SHOWN):
        g = G[i]
        k = C.ease((t - FILL_AT[n]) / 0.55)
        instrument.gauge_emph(g, 1.0)
        C.gauge_at(g, arms[i], RIGHT, z=0.95, side=0.30)
        instrument.gauge_set(g, k)
        C.gauge_show(g, k > 0.02)

    u = C.ease(t / DUR)
    loc = tuple(a + (b - a) * u for a, b in zip(CAM_A[0], CAM_B[0]))
    at = tuple(a + (b - a) * u for a, b in zip(CAM_A[1], CAM_B[1]))
    stage.aim(cam, loc, at)
    C.flicker_home(t)


stage.bake(OUT, NF, C.FPS, draw, 'outro-4')
