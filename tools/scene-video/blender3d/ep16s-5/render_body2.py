"""ep16s-5 본문2 — 8 비트 · 10.61 초. **비석 옆에 도구가 서고, 둘이 갈린다.**

  0.00~1.85  「달라진 게 셋이에요」
     비석 둘 옆에 **도구가 하나씩** 선다(뜻층 보라)
  1.85~4.55  「누가 죽었는지를 구별했고,」
     가깝게 — 두 도구가 **서로 다르다**(물뿌리개 · 망치)
  4.55~7.65  「예상했던 죽음과 이상한 죽음을 갈라서 봤고,」
     카메라가 두 무덤을 지난다 — 한쪽은 **밭과 집 옆**, 다른 쪽은 **빈 땅**이다
  7.65~10.61 「재미없다가 신기하네로 바뀌었어요」
     산 사람이 무덤 앞에 서서 **올려다본다**(`look_up`)

🔑 16-1 이 만든 어휘(비석 옆 도구)를 착지 편이 **되받는다.** 새 그림이 아니라 같은 그림이
   다른 자리에서 뜻을 바꾸는 것이 이 편의 문법이다.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import motions
import stage

SHOT, SID = 'body2', 'S2'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 8
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

L = [C.line_at(SID, i) for i in range(4)]
v, arms, graves = C.build(graves=True)
HOME = [tuple(a.location) for a in arms]
for i in C.IDLERS:
    C.hide_person(arms[i], False)
TOOLS = [C.tool_at(k, C.CAST[i][1][0], C.CAST[i][1][1], face=C.home_face(i))
         for k, i in zip(C.TOOLS, C.IDLERS)]
UP = [0.45, 0.85]
WATCHER = 1                                   # 무덤 앞에 서서 올려다보는 사람
LOOK_AT = L[3] + 0.25

KEY = [(0.00, C.MID[0], C.MID[1]),
       (L[1] + 0.20, C.CLOSE[0], C.CLOSE[1]),
       (L[2] + 0.30, (-1.65, -6.75, 2.15), (-1.35, -2.05, 0.95)),
       (DUR, (-0.85, -6.15, 1.95), (-1.15, -2.45, 0.95))]

cam = stage.light_camera()
C.lens(cam, C.MID_LENS)
stage.key_from_view(*C.MID)
print('[body2-16-5] %d비트 · %.2f초 · 줄 %s' % (BEATS, DUR, [round(x, 2) for x in L]))


def draw(fi):
    t = fi / C.FPS
    for n, g in enumerate(graves):
        C.grave_rise(g, 1.0)
        C.tool_show(TOOLS[n], t >= UP[n])
    w = arms[WATCHER]
    w.location = HOME[WATCHER]
    w.rotation_euler = (0, 0, C.home_face(WATCHER))
    if t < LOOK_AT:
        C.work(w, C.CAST[WATCHER][0], t)
    else:
        stage.pose(w, motions.blend(motions.stop(t - LOOK_AT),
                                    motions.look_up(t - LOOK_AT),
                                    C.ease((t - LOOK_AT) / 0.35)))
    C.all_work(arms, t, HOME, skip=tuple(C.IDLERS) + (WATCHER,))
    C.fly(cam, KEY, t)


stage.bake(OUT, NF, C.FPS, draw, 'body2-16-5')
