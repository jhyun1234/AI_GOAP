"""ep16s-4 본문1 — 12 비트 · 16.90 초. **양쪽에서 끊었다.**

  0.00~3.05   「둘이 친해지면 사이에 줄이 하나 이어져요」
     마주 선 둘이 **이야기하고**(`talk`) 실이 **생긴다**
  3.05~5.90   「사이가 나빠져도 줄은 남고요」
     다른 둘은 **등지고 선 채**인데 그 사이에도 실이 걸린다
  5.90~9.85   「그런데 한 사람이 죽으면 뒷정리가 그 줄을 끊었어요」
     무너지는 몸에서 실이 **툭 끊어진다**
  9.85~13.15  「죽은 쪽만이 아니라 산 사람 쪽에서도요」
     넓게 — 멀리 선 사람 쪽 실 끝도 **같이 사라진다**
  13.15~16.90 「그러면 상실이 남은 사람한테 도착할 길이 없어요」
     남은 사람이 **둘러보고**(`look_around`) 그냥 **밭을 간다**(`farm`)

🔴 등지고 선 둘은 `home_face` 가 각을 뒤집는다 — 「사이가 나쁘다」를 자리가 아니라
   **방향**이 진다. 자리를 벌리면 실이 길어져서 「멀다」로 읽힌다.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import motions
import stage

SHOT, SID = 'body1', 'S1'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 12
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

L = [C.line_at(SID, i) for i in range(5)]
v, arms, grave = C.build(grave=True)
HOME = [tuple(a.location) for a in arms]
VIOLET, CYAN = C.mats()
PAIR_AB = [C.bond(C.A, C.B, CYAN, dz=(0.10, 0.10)), C.bond(C.A, C.B, CYAN, dz=(-0.16, -0.16))]
PAIR_CD = [C.bond(C.C, C.D, CYAN)]
FALL_AT = L[2] + 1.10
CUT_AT = FALL_AT + 0.50

KEY = [(0.00, C.CLOSE[0], C.CLOSE[1]),
       (L[1] - 0.10, (0.75, -5.55, 1.65), (1.95, -0.95, 0.85)),
       (L[2] - 0.05, C.MID[0], C.MID[1]),
       (L[3] + 0.20, C.EYE[0], C.EYE[1]),
       (DUR, (-1.55, -6.95, 1.95), (-2.25, -3.05, 0.85))]

cam = stage.light_camera()
C.lens(cam, C.MID_LENS)
stage.key_from_view(*C.CLOSE)
print('[body1-16-4] %d비트 · %.2f초 · 줄 %s' % (BEATS, DUR, [round(x, 2) for x in L]))


def draw(fi):
    t = fi / C.FPS
    for th in PAIR_AB:
        th.hide_render = not (0.55 <= t < CUT_AT)
    for th in PAIR_CD:
        th.hide_render = t < L[1] + 0.35
    C.fall(arms[C.A], C.A, HOME[C.A], t, FALL_AT)
    C.hide_person(arms[C.A], t < CUT_AT + 0.30)
    C.grave_rise(grave, C.ease((t - CUT_AT - 0.25) / 0.55))
    # 남은 사람 — 마지막 줄에서 둘러보다 밭으로 돌아선다
    b = arms[C.B]
    b.location = HOME[C.B]
    b.rotation_euler = (0, 0, C.home_face(C.B))
    if t < L[4]:
        C.work(b, 'talk' if t < CUT_AT else 'stop', t)
    else:
        u = t - L[4]
        if u < 1.60:
            stage.pose(b, motions.blend(motions.stop(u), motions.look_around(u),
                                        C.ease(u / 0.35)))
        else:
            stage.pose(b, motions.blend(motions.look_around(u), motions.farm(u - 1.60),
                                        C.ease((u - 1.60) / 0.40)))
    C.all_work(arms, t, HOME, skip=(C.A, C.B))
    C.fly(cam, KEY, t)


stage.bake(OUT, NF, C.FPS, draw, 'body1-16-4')
