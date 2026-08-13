"""ep16s-2 본문2 — 7 비트 · 9.28 초. **안 사라지는 줄, 그리고 빈 자리.**

자막 세 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~3.05 「그래서 안 사라지는 줄을 하나 얹었어요」
     칸 하나가 **켜진 채 유지된다.** 앞 샷의 깜빡임과 정확히 반대다
  3.05~6.10 「그 사람이 밥을 먹으면 그때 사라지고요」
     굶던 사람이 **밥을 먹고**(`eat`) 그 순간 칸이 **꺼진다**
  6.10~9.28 「아무 일도 없으면 그 자리는 완전히 비어요」
     넓게. 격자가 **텅 빈 채** 떠 있고 아래에서는 밭 갈고 나무 팬다

🔴 **빈 격자만 잡으면 아무 말도 안 한다**(런북 §7). 그래서 마지막 줄은 빈 화면이 아니라
   **일하는 마을**을 함께 잡는다 — 「아무 일 없음」이 곧 「사람들이 제 일을 함」이다.
🔑 원문의 중립 불변식이 그 문장이다: '평시에는 이 자리가 완전히 빕니다.'
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import stage

SHOT, SID = 'body2', 'S2'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 7
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

L = [C.line_at(SID, i) for i in range(3)]
v, arms, cells = C.build()
HOME = [tuple(a.location) for a in arms]
LIT = 2
OFF_AT = L[1] + 1.30                       # 먹는 동작이 한 번 돈 뒤에 꺼진다

KEY = [(0.00, C.MID[0], C.MID[1]),
       (L[1] - 0.10, (-0.20, -6.75, 3.75), (-0.85, -1.55, 1.35)),
       (L[2] - 0.05, (-0.25, -7.85, 4.35), (-0.55, -0.65, 1.75)),
       (DUR, C.EYE[0], C.EYE[1])]

cam = stage.light_camera()
C.lens(cam, C.MID_LENS)
stage.key_from_view(*C.MID)
print('[body2-16-2] %d비트 · %.2f초 · 줄 %s' % (BEATS, DUR, [round(x, 2) for x in L]))


def draw(fi):
    t = fi / C.FPS
    C.light(cells, (LIT,) if t < OFF_AT else ())
    C.all_work(arms, t, HOME)
    C.fly(cam, KEY, t)


stage.bake(OUT, NF, C.FPS, draw, 'body2-16-2')
