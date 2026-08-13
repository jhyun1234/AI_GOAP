"""ep15s-5 아웃트로 — 3 비트 · 3.43 초. **아무것도 안 적힌 비석이 선다.**

자막 한 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~3.08 「다음 편에서는, 무덤에 이름이 없었어요」
     쓰러진 두 자리에 낮은 흙더미와 **빈 비석**이 하나씩 솟는다.
     마을은 그 옆에서 계속 제 일을 한다 — 아무도 그쪽을 안 본다

🔑 **글자를 안 새긴다.** 이 세계에는 글자가 없고, 비어 있는 비석이 곧 「이름이 없다」다.
   다음 편이 맡을 사건이라 여기서는 **무대만** 세운다.
🔴 비석은 세계층(흙·돌)이라 유채색 예산을 한 톨도 안 쓴다.
🔴 엔진이 이 샷 위에 아웃트로 카드를 얹는다 — 그림이 조용해야 카드가 읽힌다.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import stage, village

SHOT, SID = 'outro', 'SO'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 3
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

v, arms, fire = C.build()
HOME = [tuple(a.location) for a in arms]
# 🔑 쓰러진 그 자리에 선다 — 자리가 같아야 「저 사람들」이 된다
GRAVES = [village.grave(C.CAST[i][1][0], C.CAST[i][1][1], -14 + 26 * n)
          for n, i in enumerate(C.IDLERS)]
BASE = [[(o, tuple(o.scale), o.location.z) for o in g] for g in GRAVES]
for i in C.IDLERS:
    arms[i].hide_render = True                 # 무덤이 그 자리를 대신한다
    for ob in arms[i].children:
        ob.hide_render = True
UP = [0.45, 1.05]
# 🔴 **무덤 둘이 다 들어와야 한다.** 한쪽만 겨누면 나머지가 덤불 옆 사람 뒤로 숨는다 —
#    자막이 「무덤에」라고 복수로 말하는데 화면에 하나만 있으면 그 줄이 반만 산다.
CAM_A = ((2.90, -7.10, 2.20), (0.30, -3.60, 0.72))
CAM_B = ((2.25, -6.40, 2.00), (0.15, -3.40, 0.70))
cam = stage.light_camera()
C.lens(cam, C.MID_LENS)
stage.key_from_view(*CAM_A)
print('[outro-5] %d비트 · %.2f초 · 무덤 %d' % (BEATS, DUR, len(GRAVES)))


def draw(fi):
    t = fi / C.FPS
    C.all_work(arms, t, HOME, skip=C.IDLERS)
    # 🔴 조각 셋을 **같이** 세운다 — 하나만 만지면 나머지가 먼저 떠 있다(village 함정)
    for n, parts in enumerate(BASE):
        k = C.ease((t - UP[n]) / 0.60)
        for o, sc, z0 in parts:
            o.hide_render = k <= 0.02
            o.scale = (sc[0], sc[1], max(sc[2] * k, 1e-4))
            o.location.z = z0 * k
    u = C.ease(t / DUR)
    stage.aim(cam, tuple(a + (b - a) * u for a, b in zip(CAM_A[0], CAM_B[0])),
              tuple(a + (b - a) * u for a, b in zip(CAM_A[1], CAM_B[1])))
    village.flicker(fire, t)


stage.bake(OUT, NF, C.FPS, draw, 'outro-5')
