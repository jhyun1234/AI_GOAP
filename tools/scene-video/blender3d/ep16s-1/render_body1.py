"""ep16s-1 본문1 — 9 비트 · 11.77 초. **일곱이 전부 똑같이 비어 있다.**

자막 네 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~2.74 「죽은 자리마다 비석이 하나씩 서요」
     가깝게. 무덤 하나가 흙더미 위로 **올라선다.** 무엇이 남는지를 먼저 한 번 크게 본다
  2.74~6.01 「그런데 일곱 개가 전부 똑같이 비어 있었어요」
     카메라가 물러나며 일곱을 **훑는다.** 구별되는 것이 하나도 없다
  6.01~9.79 「아침까지 지켜보던 그 사람이 어느 무덤인지,」
     살아남은 사람이 밭을 두고 **무덤 사이로 걸어 들어간다**(3.6초 · 거리에서 역산)
  9.79~11.77 「저도 못 찾겠더라고요」
     그 자리에 서서 **둘러본다**(`look_around`). 화면에 단서가 없다

🔴 「빈 땅으로 없음을 그리지 마라」(런북 §7)를 여기서 지킨다 — 없음을 **사람이 찾는
   행동**으로 그린다. 걷다 서서 둘러보는 몸이 「단서가 없다」를 진다.
🔴 여전히 계기도 유채색도 없다. 이 샷은 **아직 아무것도 안 고친 상태**다.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import motions
import stage

SHOT, SID = 'body1', 'S1'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 9
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

L = [C.line_at(SID, i) for i in range(4)]
v, arms, graves = C.build()
HOME = [tuple(a.location) for a in arms]
C.dead_hide(arms)
NEAR = C.DEAD[2][1]                       # 크게 보는 무덤 — 남쪽이라 카메라가 가깝다
GO_AT, GO_SPAN = L[2] + 0.04, 3.60        # 걷는 시간은 거리 ÷ WALK_SPEED 로 잡았다
STAND = (-1.95, -2.75)                    # 무덤 셋 사이. 🔴 비석을 밟고 서지 않는다

KEY = [(0.00, C.CLOSE[0], C.CLOSE[1]),
       (L[1] - 0.10, C.CLOSE[0], C.CLOSE[1]),
       (L[1] + 1.60, (1.55, -7.05, 2.55), (-0.95, -1.55, 0.78)),
       (L[2], C.EYE[0], C.EYE[1]),
       (DUR, C.MID[0], (STAND[0], STAND[1] + 0.35, 0.75))]

cam = stage.light_camera()
C.lens(cam, C.MID_LENS)
stage.key_from_view(*C.CLOSE)
print('[body1-16-1] %d비트 · %.2f초 · 줄 %s' % (BEATS, DUR, [round(x, 2) for x in L]))


def draw(fi):
    t = fi / C.FPS
    for i, g in enumerate(graves):
        # 🔴 가까이 잡은 하나만 올라온다. 나머지는 이미 서 있다 — 일곱이 같이 솟으면
        #    「하나씩」이 거짓말이 되고, 훅에서 이미 선 것들이 다시 서는 꼴이 된다
        C.grave_rise(g, C.ease((t - 0.30) / 0.85) if i == 2 else 1.0)
    live = arms[C.LIVE_I]
    p, wt = C.walk_to(live, HOME[C.LIVE_I], STAND, t, GO_AT, GO_SPAN)
    if t < GO_AT:
        C.work(live, C.CAST[C.LIVE_I][0], t)            # 아직 밭을 간다
    elif t < GO_AT + GO_SPAN:
        C.walk_pose(live, wt)
    else:
        # 🔑 선 자리에서 **둘러본다.** 자리·방향은 `walk_to` 가 이미 끝값으로 고정해 뒀다
        stage.pose(live, motions.blend(motions.stop(t - GO_AT - GO_SPAN),
                                       motions.look_around(t - GO_AT - GO_SPAN),
                                       C.ease((t - GO_AT - GO_SPAN) / 0.35)))
    C.fly(cam, KEY, t)


stage.bake(OUT, NF, C.FPS, draw, 'body1-16-1')
