"""ep16s-4 훅 — 5 비트 · 5.25 초. **두 가닥이 한꺼번에 사라진다.**

  0.00~1.85 「단짝이 죽었는데,」
     마주 선 두 사람 사이에 **실 두 가닥**이 걸려 있다
  1.85~5.25 「남은 사람 기억에서도 그 사이가 사라졌어요」
     한 사람이 **무너지고** 두 가닥이 **다 사라진다.** 남은 사람은 그대로 서 있다

🔴 남은 사람이 아무 반응도 안 하는 것이 이 편의 사건이다 — 상실이 도착할 길이 없다.
🔴 살아 있는 실은 계기 시안이라 뜻층 예산을 안 먹는다. 이 샷의 뜻층은 0색이다.
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
BEATS = 5
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

L = [C.line_at(SID, i) for i in range(2)]
v, arms, grave = C.build(grave=True)
HOME = [tuple(a.location) for a in arms]
VIOLET, CYAN = C.mats()
BONDS = [C.bond(C.A, C.B, CYAN, dz=(0.10, 0.10)), C.bond(C.A, C.B, CYAN, dz=(-0.16, -0.16))]
FALL_AT = L[1] + 0.35
CUT_AT = FALL_AT + 0.55

cam = stage.light_camera()
C.lens(cam, C.MID_LENS)
stage.key_from_view(*C.CLOSE)
print('[hook-16-4] %d비트 · %.2f초' % (BEATS, DUR))


def draw(fi):
    t = fi / C.FPS
    for th in BONDS:
        th.hide_render = t >= CUT_AT
    C.fall(arms[C.A], C.A, HOME[C.A], t, FALL_AT)
    C.hide_person(arms[C.A], t < CUT_AT + 0.35)
    C.grave_rise(grave, C.ease((t - CUT_AT - 0.30) / 0.55))
    C.all_work(arms, t, HOME, skip=(C.A,))
    u = C.ease(t / DUR)
    stage.aim(cam, C.lerp3(C.CLOSE[0], C.MID[0], u), C.lerp3(C.CLOSE[1], C.MID[1], u))


stage.bake(OUT, NF, C.FPS, draw, 'hook-16-4')
