"""ep16s-1 본문1 — 9 비트 · 11.77 초. **일곱이 전부 똑같이 비어 있다.**

  0.00~2.74  「죽은 자리마다 비석이 하나씩 서요」
     가깝게(0.45). 무덤 하나가 흙더미 위로 **올라선다** — 무엇이 남는지를 크게 한 번
  2.74~6.01  「그런데 일곱 개가 전부 똑같이 비어 있었어요」
     카메라가 무리 A → 무리 B 로 **흐르며** 비석을 지난다(0.30). 구별되는 것이 없다
  6.01~9.79  「아침까지 지켜보던 그 사람이 어느 무덤인지,」
     살아남은 사람이 밭을 두고 **무덤 사이로 걸어 들어온다**(0.38 · 거리에서 역산)
  9.79~11.77 「저도 도무지 못 찾겠더라고요」
     그 자리에 서서 **둘러본다**(0.48). 화면에 단서가 없다

🔴 「없다」를 빈 땅으로 그리지 않는다(런북 §7) — **사람이 찾는 행동**이 그 말을 진다.
🔴 인물 크기는 `stage.cam_for(frac=…)` 가 정한다. 좌표를 손으로 적지 마라.
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

NEAR = 1                                   # 크게 잡는 무덤 — 무리 A 의 가운데
STAND = (-1.05, -2.35)                     # 무덤 사이. 🔴 비석을 밟고 서지 않는다
GO_AT, GO_SPAN = L[2] + 0.05, 3.35

# 🔑 크기: 0.45(무덤 하나) → 0.30(무덤 여럿) → 0.38(걸어오는 사람) → 0.48(둘러봄)
K0 = C.cam_at(NEAR, frac=0.45, lens_mm=C.LENS_CLOSE, yaw=-72, elev=17, dz=0.45)
K1 = C.cam_at(C.mid_of(C.GROUP_A), frac=0.30, lens_mm=C.LENS_MID, yaw=-86, elev=20, dz=0.50)
K2 = C.cam_at(C.mid_of(C.GROUP_A + C.GROUP_B), frac=0.30, lens_mm=C.LENS_MID,
              yaw=-104, elev=21, dz=0.55)
K3 = C.cam_at(STAND, frac=0.38, lens_mm=C.LENS_MID, yaw=-118, elev=16, dz=0.62)
K4 = C.cam_at(STAND, frac=0.48, lens_mm=C.LENS_CLOSE, yaw=-124, elev=14, dz=0.66)
KEY = [(0.00, K0[0], K0[1]),
       (L[1] + 0.10, K1[0], K1[1]),
       (L[2] - 0.10, K2[0], K2[1]),
       (L[3] - 0.20, K3[0], K3[1]),
       (DUR, K4[0], K4[1])]

cam = stage.light_camera()
C.lens(cam, C.LENS_MID)
stage.key_from_view(*K1)
C.report('body1-16-1', KEY, C.LENS_MID)
print('[body1-16-1] %d비트 · %.2f초 · 줄 %s' % (BEATS, DUR, [round(x, 2) for x in L]))


def draw(fi):
    t = fi / C.FPS
    for i, g in enumerate(graves):
        # 🔴 크게 잡은 하나만 올라온다 — 일곱이 같이 솟으면 「하나씩」이 거짓말이 된다
        C.grave_rise(g, C.ease((t - 0.25) / 0.80) if i == NEAR else 1.0)
    live = arms[C.LIVE_I]
    p, wt = C.walk_to(live, HOME[C.LIVE_I], STAND, t, GO_AT, GO_SPAN)
    if t < GO_AT:
        C.work(live, C.CAST[C.LIVE_I][0], t)            # 아직 밭을 간다
    elif wt < GO_SPAN:
        C.walk_pose(live, wt)
    else:
        u = t - GO_AT - GO_SPAN
        stage.pose(live, motions.blend(motions.stop(u), motions.look_around(u),
                                       C.ease(u / 0.35)))
    C.fly(cam, KEY, t)


stage.bake(OUT, NF, C.FPS, draw, 'body1-16-1')
