"""ep16s-1 훅 — 4 비트 · 4.81 초. **일곱이 무너지고, 남은 것은 똑같은 무덤 일곱이다.**

자막 두 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~2.23 「하루에 일곱 명이 죽었어요」
     넓게. 마을 곳곳에서 일곱이 **차례로 배를 움켜쥐고 무너진다**(0.22초 간격).
     밭 옆 한 사람만 계속 밭을 간다
  2.23~4.81 「그런데 누가 죽었는지를 몰랐어요」
     쓰러진 자리마다 흙더미와 **빈 비석**이 솟는다. 일곱이 **서로 구별이 안 된다** —
     그게 이 줄이다. 살아남은 사람은 그쪽을 안 보고 계속 일한다

🔑 크기 판단: 「일곱」이 한 프레임에 다 들어와야 수가 자막의 거짓말이 안 된다 — 넓게.
🔴 계기도 유채색도 없다. 이 편의 사건은 **남은 것이 다 똑같다**이고, 색을 켜는 순간
   그 색이 구별 단서가 되어 사건을 지운다.
🔴 죽음에 예비 동작을 안 넣었다(`C.fall`). 굶어 죽는 것은 예고가 없다.
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

v, arms, graves = C.build()
HOME = [tuple(a.location) for a in arms]
for g in graves:
    C.grave_rise(g, 0.0)
cam = stage.light_camera()
C.lens(cam, C.WIDE_LENS)
stage.key_from_view(*C.EYE)
print('[hook-16-1] %d비트 · %.2f초 · 무덤 %d' % (BEATS, DUR, len(graves)))


def draw(fi):
    t = fi / C.FPS
    for i in range(len(C.DEAD)):
        k = C.ease((t - (C.RISE0 + i * C.RISE_GAP)) / 0.55)
        C.grave_rise(graves[i], k)
        # 🔴 무덤이 반쯤 올라오면 몸을 뺀다 — 둘이 같은 자리에 겹쳐 있으면 한 덩이가 된다
        hide = k > 0.35
        arms[i].hide_render = hide
        for ob in arms[i].children:
            ob.hide_render = hide
        if not hide:
            C.fall(arms[i], i, HOME[i], t, C.FALL0 + i * C.FALL_GAP)
    C.work(arms[C.LIVE_I], C.CAST[C.LIVE_I][0], t)      # 남은 사람은 계속 밭을 간다
    u = C.ease(t / DUR)
    (sx, sy, sz), (ax, ay, az) = C.EYE
    stage.aim(cam, (sx + 0.25 * u, sy + 0.80 * u, sz - 0.30 * u),
              (ax + 0.10 * u, ay - 0.45 * u, az - 0.08 * u))


stage.bake(OUT, NF, C.FPS, draw, 'hook-16-1')
