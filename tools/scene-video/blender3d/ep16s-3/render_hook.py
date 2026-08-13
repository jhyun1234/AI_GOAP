"""ep16s-3 훅 — 4 비트 · 5.32 초. **한 색이 판을 다 덮는다.**

  0.00~2.55 「죽은 사람들 기록을 모아 놨어요」
     공중 격자에 칸이 **하나씩 채워진다**(계기 시안)
  2.55~5.32 「그런데 한 사람이 다 덮어 버렸어요」
     한 색(보라)이 **아홉 칸을 연달아** 칠해 판이 그 색이 된다

🔴 판은 열두 칸이고 덮는 것은 아홉이다 — 딱 맞는 판이면 「덮었다」가 아니라 「채웠다」다.
🔴 뜻층은 보라 하나. 마을은 아래에서 계속 제 일을 한다(사건은 격자에서 난다).
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
v, arms, cells, _g = C.build()
HOME = [tuple(a.location) for a in arms]
FILL0, FILL_GAP = 0.35, 0.22          # 칸이 하나씩 (계기 시안 테두리는 늘 있다)
COVER0, COVER_GAP = L[1] + 0.20, 0.20  # 보라가 아홉 칸을 연달아
cam = stage.light_camera()
C.lens(cam, C.MID_LENS)
stage.key_from_view(*C.MID)
print('[hook-16-3] %d비트 · %.2f초 · 칸 %d' % (BEATS, DUR, len(cells)))


def draw(fi):
    t = fi / C.FPS
    n = max(0, min(len(cells), int((t - FILL0) / FILL_GAP) + 1))
    C.show(cells, set(range(n)))
    m = max(0, min(len(C.NINE), int((t - COVER0) / COVER_GAP) + 1)) if t >= COVER0 else 0
    C.paint(cells, violet_idx=set(C.NINE[:m]))
    C.all_work(arms, t, HOME)
    u = C.ease(t / DUR)
    stage.aim(cam, C.lerp3(C.MID[0], C.EYE[0], u), C.lerp3(C.MID[1], C.EYE[1], u))


stage.bake(OUT, NF, C.FPS, draw, 'hook-16-3')
