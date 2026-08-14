"""ep16s-2 훅 — 4 비트 · 4.53 초. **칸은 켜졌는데 누구인지가 없다.**

자막 두 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~2.16 「굶고 있다는 알림이 떴는데,」
     넓게. 마을 위 공중 격자에서 칸 하나가 **빨갛게 켜진다**
  2.16~4.53 「누가 굶는지가 없었어요」
     카메라가 마을을 훑는다 — 여섯이 다 제 일을 하고 **누가 그 사람인지 표시가 없다**

🔴 굶는 사람을 겉으로 갈라 놓지 않았다. 갈라 놓는 순간 이 줄이 거짓말이 된다.
🔴 뜻층은 빨강 하나. 격자 테두리(시안)는 계기층이라 예산 밖이다.
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

v, arms, cells = C.build()
HOME = [tuple(a.location) for a in arms]
LIT = 2
ON = 1.05
cam = stage.light_camera()
C.lens(cam, C.WIDE_LENS)
stage.key_from_view(*C.EYE)
print('[hook-16-2] %d비트 · %.2f초 · 칸 %d' % (BEATS, DUR, len(cells)))


def draw(fi):
    t = fi / C.FPS
    C.all_work(arms, t, HOME)
    C.light(cells, (LIT,) if t >= ON else ())
    u = C.ease(t / DUR)
    (sx, sy, sz), (ax, ay, az) = C.EYE
    stage.aim(cam, (sx + 0.55 * u, sy + 0.70 * u, sz - 0.35 * u),
              (ax + 0.15 * u, ay - 0.50 * u, az - 0.35 * u))


stage.bake(OUT, NF, C.FPS, draw, 'hook-16-2')
