"""ep16s-1 훅 — 4 비트 · 4.81 초. **한 사람이 무너지고, 그 자리에 빈 비석이 선다.**

  0.00~2.23 「하루에 일곱 명이 죽었어요」
     **가깝게(인물 0.50)** 물 주던 사람이 배를 움켜쥐고 무너진다. 카메라가 옆으로
     흐르며 곧바로 둘이 더 무너진다 — 일곱은 자막이 세고 화면은 **무너짐 자체**를 진다
  2.23~4.81 「그런데 누가 죽었는지를 몰랐어요」
     쓰러진 자리마다 흙더미와 **빈 비석**이 솟는다. 밭 옆 한 사람은 계속 일한다 —
     구별할 단서가 화면에 하나도 없다

🔴 **2026-08-14 재조정**: 옛 판은 일곱을 한 프레임에 넣으려고 8m 밖에서 잡아 사람이
   화면의 4% 였다. 지금은 **한 무리씩 크게** 잡고 일곱은 카메라가 옮겨 가며 센다.
🔴 계기도 유채색도 없다. 남은 것이 다 똑같다는 것이 이 편의 사건이고, 색을 켜면 그 색이
   구별 단서가 되어 사건을 지운다.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import stage

SHOT, SID = 'hook', 'SH'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 4
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

L = [C.line_at(SID, i) for i in range(2)]
v, arms, graves = C.build()
HOME = [tuple(a.location) for a in arms]
for g in graves:
    C.grave_rise(g, 0.0)

# 카메라: 무너지는 사람 → 무리 A → 무리 A·B 사이(비석이 솟는 것을 본다)
# 🔴 방위는 **그 사람의 앞쪽**에서 딴다(`face_yaw`) — 남쪽 고정이면 뒤통수가 찬다.
# 🔑 첫 컷의 주인공은 **바깥을 보고 선 사람**(곡괭이)이다 — 마을 안쪽을 보는 사람을
#    앞에서 잡으면 카메라가 집·밭 사이로 들어가 다른 사람 뒤통수가 화면을 막는다.
A0 = C.cam_at(1, frac=0.46, lens_mm=C.LENS_CLOSE, yaw=C.face_yaw(1, 22), elev=18,
              dz=0.70)
A1 = C.cam_at(C.mid_of(C.GROUP_A), frac=C.FIG_MID, lens_mm=C.LENS_MID,
              yaw=C.face_yaw(1, 26), elev=19, dz=0.66)
A2 = C.cam_at(C.mid_of(C.GROUP_A + C.GROUP_B), frac=C.FIG_WIDE, lens_mm=C.LENS_WIDE,
              yaw=-100, elev=22, dz=0.60)
KEY = [(0.00, A0[0], A0[1]),
       (L[1] - 0.15, A1[0], A1[1]),
       (DUR, A2[0], A2[1])]

cam = stage.light_camera()
C.lens(cam, C.LENS_MID)
stage.key_from_view(*A1)
C.report('hook-16-1', KEY, C.LENS_MID)
print('[hook-16-1] %d비트 · %.2f초 · 무덤 %d' % (BEATS, DUR, len(graves)))


def draw(fi):
    t = fi / C.FPS
    for i in range(len(C.DEAD)):
        rise = L[1] + C.RISE0 + i * C.RISE_GAP
        k = C.ease((t - rise) / 0.50)
        C.grave_rise(graves[i], k)
        hide = k > 0.35
        C.hide_person(arms[i], not hide)
        if not hide:
            C.fall(arms[i], i, HOME[i], t, C.FALL0 + i * C.FALL_GAP)
    C.work(arms[C.LIVE_I], C.CAST[C.LIVE_I][0], t)      # 남은 사람은 계속 밭을 간다
    C.fly(cam, KEY, t)


stage.bake(OUT, NF, C.FPS, draw, 'hook-16-1')
