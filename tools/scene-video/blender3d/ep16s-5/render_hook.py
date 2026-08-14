"""ep16s-5 훅 — 6 비트 · 7.24 초. **아무도 안 돌아보는 죽음, 그리고 빈 비석 둘.**

  0.00~3.55 「전에 이 판을 보고 제가 한 말은 이거였어요」
     넓게. 마을이 돌아가고 구석에서 노는 둘이 보인다
  3.55~7.24 「게으름뱅이 둘 다 그냥 죽었어, 별 이야기가 없었다」
     둘이 **그냥 무너지고**(예비 동작 없음) 그 자리에 **빈 비석 둘**이 선다.
     아무도 안 돌아본다 — 곁에 아무것도 안 선다

🔴 이 샷은 **밀스톤 이전의 판**이다. 도구도 계기도 켜지 않는다 — 켜는 순간 이 편의
   대조가 죽는다(뒤에서 같은 자리에 켜는 것이 결론이다).
🔴 반응을 일부러 뺐다(ep15s-5 와 같은 자리). 「아무도 안 돌아봤다」가 사건이다.
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
BEATS = 6
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

L = [C.line_at(SID, i) for i in range(2)]
v, arms, graves = C.build(graves=True)
HOME = [tuple(a.location) for a in arms]
FALL = [L[1] + 0.35, L[1] + 0.85]
RISE = [L[1] + 1.95, L[1] + 2.35]

KEY = [(0.00, C.EYE[0], C.EYE[1]),
       (L[1] - 0.10, (0.15, -7.75, 2.85), (-0.55, -1.85, 0.95)),
       (DUR, C.MID[0], C.MID[1])]

cam = stage.light_camera()
C.lens(cam, C.WIDE_LENS)
stage.key_from_view(*C.EYE)
print('[hook-16-5] %d비트 · %.2f초' % (BEATS, DUR))


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


stage.bake(OUT, NF, C.FPS, draw, 'hook-16-5')
