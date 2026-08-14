"""ep16s-3 본문2 — 7 비트 · 9.12 초. **아홉 연발이 판을 덮는다.**

  0.00~3.15 「그런데 게으름뱅이 하나가 명령을 아홉 번 거부한 게,」
     한 사람이 일하러 안 가고 **선 채로 멈춰** 있고, 그 색이 칸을 하나씩 켠다
  3.15~6.30 「명부를 통째로 덮어 버렸어요」
     그 색이 판을 **다 채운다** — 다른 사람 칸이 안 보인다
  6.30~9.12 「다 보여주는 게 답이 아니더라고요」
     격자가 그대로 멈춰 있다. **읽을 것이 없다** — 카메라만 천천히 흐른다

🔴 「명령 거부」는 이 세계에 몸짓이 없다. **일하러 안 가고 선 채로 멈추는 것**(`stop`)이
   그 말이고, 아홉 번은 자막과 **칸 아홉 개**가 함께 진다.
🔴 마지막 줄은 홀드처럼 보이지만 홀드가 아니다 — 판이 멈춰 있는 동안 카메라가 움직인다.
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
v, arms, cells, _g = C.build()
HOME = [tuple(a.location) for a in arms]
NINE0, NINE_GAP = 0.55, 0.31

KEY = [(0.00, (-0.35, -6.55, 3.85), (-0.95, -1.15, 1.65)),
       (L[1] - 0.10, C.MID[0], C.MID[1]),
       (L[2] + 0.20, C.CLOSE[0], C.CLOSE[1]),
       (DUR, (0.55, -4.75, 4.25), (-0.15, 0.55, 2.40))]

cam = stage.light_camera()
C.lens(cam, C.MID_LENS)
stage.key_from_view(KEY[0][1], KEY[0][2])
print('[body2-16-3] %d비트 · %.2f초 · 줄 %s' % (BEATS, DUR, [round(x, 2) for x in L]))


def draw(fi):
    t = fi / C.FPS
    n = max(0, min(len(C.NINE), int((t - NINE0) / NINE_GAP) + 1))
    C.show(cells)
    C.paint(cells, violet_idx=set(C.NINE[:n]))
    C.all_work(arms, t, HOME)      # 게으름뱅이는 CAST 가 이미 `stop` 이다
    C.fly(cam, KEY, t)


stage.bake(OUT, NF, C.FPS, draw, 'body2-16-3')
