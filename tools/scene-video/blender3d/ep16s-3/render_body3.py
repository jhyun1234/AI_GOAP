"""ep16s-3 본문3 — 12 비트 · 17.12 초. **접고, 갈라 세우고, 목차로 되돌린다.**

  0.00~2.95   「그래서 같은 일은 한 줄로 묶었어요」
     아홉 칸이 **한 칸으로 접힌다**
  2.95~6.05   「묶을 땐 무엇을 했는지까지 봤고요」
     접힌 칸이 **둘로 갈라선다**(보라 = 그 사람 · 연두 = 한 일)
  6.05~10.30  「밭 완공 열한 번이 그 사이에 낀 집 완공을 삼켰거든요」
     밭 옆 사람은 **물을 주고**, 집 옆 사람은 **망치질한다** — 둘이 따로 선다
  10.30~13.60 「명부는 목차로 두고, 누르면 이야기가 펼쳐져요」
     칸 하나에서 **실**이 내려오고 아래 칸들이 **펼쳐진다**
  13.60~17.12 「기록은 장부가 아니라 한 사람의 이야기니까요」
     그 실이 **무덤으로 이어진다**

🔴 실은 만들 때 두 점이 굳는다 — 그래서 실이 가리키는 무덤은 처음부터 그 자리에 있다.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import instrument
import stage

SHOT, SID = 'body3', 'S3'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 12
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

L = [C.line_at(SID, i) for i in range(5)]
v, arms, cells, grave = C.build(grave=True)
HOME = [tuple(a.location) for a in arms]
C.hide_person(arms[C.IDLER], False)          # 그 사람은 이미 무덤이다
VIOLET, GREEN, CYAN = C.mats()

KEEP = 5                                     # 접힌 뒤 남는 칸
SPLIT = 6                                    # 갈라서는 짝 칸
FOLD_AT, FOLD_SPAN = 0.55, 1.05
OPEN_AT = L[3] + 0.35                        # 목차에서 펼쳐지는 시각
GX, GY = C.CAST[C.IDLER][1]
kx, ky, kz = cells[KEEP].location
THREAD = instrument.thread((kx, ky, kz - 0.25), (GX, GY, 0.55), CYAN, w=0.016)
THREAD.hide_render = True

KEY = [(0.00, C.CLOSE[0], C.CLOSE[1]),
       (L[1] + 0.30, (-0.15, -5.35, 4.15), (-0.45, 0.35, 2.30)),
       (L[2] + 0.40, C.EYE[0], C.EYE[1]),
       (L[3] + 0.30, (-0.45, -7.55, 4.05), (-0.85, -0.75, 1.75)),
       (DUR, (-0.55, -6.55, 2.75), (-1.10, -1.85, 1.15))]

cam = stage.light_camera()
C.lens(cam, C.MID_LENS)
stage.key_from_view(*C.CLOSE)
print('[body3-16-3] %d비트 · %.2f초 · 줄 %s' % (BEATS, DUR, [round(x, 2) for x in L]))


def draw(fi):
    t = fi / C.FPS
    C.show(cells)
    k = C.ease((t - FOLD_AT) / FOLD_SPAN)
    if t < OPEN_AT:
        C.fold(cells, KEEP, k)                       # 아홉이 하나로
        split = t >= L[1] + 0.35
        if split:
            # 갈라섬 — 접혔던 칸 하나가 둘이 된다(그 사람 · 그가 한 일)
            cells[SPLIT].scale = (1.0, 1.0, 1.0)
            instrument.show(cells[SPLIT], True)
        C.paint(cells, violet_idx={KEEP}, green_idx={SPLIT} if split else ())
    else:
        # 목차 → 펼침. 접혀 있던 칸들이 다시 선다
        o = C.ease((t - OPEN_AT) / 0.90)
        for n, cell in enumerate(cells):
            s = 1.0 if n in (KEEP, SPLIT) else max(o, 1e-3)
            cell.scale = (s, s, s)
            instrument.show(cell, s > 0.06)
        C.paint(cells, violet_idx={KEEP}, green_idx={SPLIT})
    THREAD.hide_render = t < OPEN_AT
    C.grave_rise(grave, 1.0)
    C.all_work(arms, t, HOME, skip=(C.IDLER,))
    C.fly(cam, KEY, t)


stage.bake(OUT, NF, C.FPS, draw, 'body3-16-3')
