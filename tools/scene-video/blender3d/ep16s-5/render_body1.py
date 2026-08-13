"""ep16s-5 본문1 — 7 비트 · 9.50 초. **같은 판이 다시 돈다.**

  0.00~3.30 「똑같은 판을 다시 켜 두고 또 물어봤어요」
     마을이 처음부터 다시 돌아간다 — 밭 갈고, 물 주고, 망치질한다
  3.30~5.55 「이번 대답은 이랬어요」
     같은 자리에서 둘이 **다시 무너진다**
  5.55~9.50 「순둥이와 고집쟁이가 굶어 죽었네, 집과 밭이 있었는데」
     쓰러진 자리 **바로 옆에 밭이 있고** 그 너머에 집이 선다 — 있었는데 굶었다

🔴 훅과 **같은 자리·같은 죽음**이다. 다른 것은 카메라가 옆의 밭과 집을 함께 잡는다는 것뿐 —
   대조가 이 편의 문법이라 그림을 새로 만들면 안 된다.
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
BEATS = 7
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

L = [C.line_at(SID, i) for i in range(3)]
v, arms, graves = C.build(graves=True)
HOME = [tuple(a.location) for a in arms]
FALL = [L[1] + 0.25, L[1] + 0.70]
RISE = [L[2] + 0.35, L[2] + 0.70]

KEY = [(0.00, C.EYE[0], C.EYE[1]),
       (L[1] - 0.05, (0.45, -7.35, 2.55), (-0.75, -2.15, 0.95)),
       (L[2] + 0.30, (-1.15, -6.55, 2.25), (-1.55, -1.95, 1.05)),
       (DUR, (-1.75, -7.15, 2.65), (-1.85, -1.35, 1.15))]

cam = stage.light_camera()
C.lens(cam, C.MID_LENS)
stage.key_from_view(*C.EYE)
print('[body1-16-5] %d비트 · %.2f초 · 줄 %s' % (BEATS, DUR, [round(x, 2) for x in L]))


def draw(fi):
    t = fi / C.FPS
    for n, i in enumerate(C.IDLERS):
        k = C.ease((t - RISE[n]) / 0.55)
        C.grave_rise(graves[n], k)
        hide = k > 0.35
        C.hide_person(arms[i], not hide)
        if not hide:
            C.fall(arms[i], i, HOME[i], t, FALL[n])
    C.all_work(arms, t, HOME, skip=C.IDLERS)
    C.fly(cam, KEY, t)


stage.bake(OUT, NF, C.FPS, draw, 'body1-16-5')
