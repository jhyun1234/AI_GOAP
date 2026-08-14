"""ep16s-3 본문1 — 14 비트 · 20.10 초. **기록을 사람에게서 떼어 마을이 든다.**

  0.00~3.30   「전에는 주민이 사라지면 기록도 같이 사라졌어요」
     사람이 무너지고, 그 위 칸도 **같이 꺼진다**
  3.30~7.35   「죽은 뒤에 남기려고 만드는 건데 죽으면 없어지니까요」
     빈 비석이 서는데 그 곁에 **아무것도 없다**
  7.35~11.60  「그래서 기록을 사람한테서 떼어 마을이 들고 있게 했어요」
     사람이 무너져도 격자는 **공중에 그대로 남는다**
  11.60~15.80 「이제 성격이랑 직업, 들어온 날과 떠난 날이 남아요」
     칸 **넷이 차례로** 채워진다
  15.80~20.10 「마을이 전멸하면 숫자 대신 사람 목록이 뜨고요」
     물러나며 마을 위로 칸이 **사람 수만큼** 선다

🔴 같은 죽음을 두 번 보여 준다 — 앞은 **기록이 같이 꺼지는 판**, 뒤는 **남는 판**이다.
   되받아 뒤집는 것이 이 샷의 문법이고, 그래서 카메라 자리도 같다.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import stage

SHOT, SID = 'body1', 'S1'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 14
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

L = [C.line_at(SID, i) for i in range(5)]
v, arms, cells, grave = C.build(grave=True)
HOME = [tuple(a.location) for a in arms]
FALL_A = 0.85                     # 첫 번째 죽음(기록도 꺼진다)
FALL_B = L[2] + 1.30              # 두 번째 죽음(격자가 남는다)
MINE = 5                          # 게으름뱅이의 칸

KEY = [(0.00, C.MID[0], C.MID[1]),
       (L[1] + 0.20, C.NEAR[0], C.NEAR[1]),
       (L[2] + 0.30, (-0.35, -7.05, 3.95), (-0.85, -1.35, 1.75)),
       (L[3] + 0.30, C.CLOSE[0], C.CLOSE[1]),
       (DUR, C.EYE[0], C.EYE[1])]

cam = stage.light_camera()
C.lens(cam, C.MID_LENS)
stage.key_from_view(*C.MID)
print('[body1-16-3] %d비트 · %.2f초 · 줄 %s' % (BEATS, DUR, [round(x, 2) for x in L]))


def draw(fi):
    t = fi / C.FPS
    first = t < L[2]                       # 옛 판 — 기록이 사람에게 매달려 있다
    idler = arms[C.IDLER]
    if first:
        C.show(cells, {MINE} if t < FALL_A + 0.45 else set())
        C.paint(cells, violet_idx={MINE})
        C.fall(idler, C.IDLER, HOME[C.IDLER], t, FALL_A)
        C.hide_person(idler, t < L[1] + 0.35)
        C.grave_rise(grave, C.ease((t - L[1] - 0.30) / 0.60))
    else:
        # 새 판 — 사람이 무너져도 격자는 남고, 칸 넷이 채워진 뒤 사람 수만큼 선다
        C.grave_rise(grave, 0.0)
        C.hide_person(idler, True)
        C.fall(idler, C.IDLER, HOME[C.IDLER], t, FALL_B)
        if t < L[3]:
            on = {MINE}
        elif t < L[4]:
            n = max(0, min(4, int((t - L[3] - 0.20) / 0.85) + 1))
            on = set(C.FOUR[:n]) | {MINE}
        else:
            on = set(range(len(C.CAST)))
        C.show(cells, on)
        C.paint(cells, violet_idx={MINE})
    C.all_work(arms, t, HOME, skip=(C.IDLER,))
    C.fly(cam, KEY, t)


stage.bake(OUT, NF, C.FPS, draw, 'body1-16-3')
