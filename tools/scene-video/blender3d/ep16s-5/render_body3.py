"""ep16s-5 본문3 — 12 비트 · 16.22 초. **계기가 이미 다 켜져 있다.**

  0.00~3.05   「그런데 이번에 새로 만든 건 거의 없어요」
     실 · 막대 · 격자가 **이미 켜진 채** 마을 위에 떠 있다
  3.05~6.45   「사이가 바뀐 이유도, 성격 보정도, 며칠치 남았는지도,」
     카메라가 실 → 막대 → 격자를 **차례로 지난다**
  6.45~9.25   「전부 이미 계산돼 있었거든요」
     셋이 **한 화면에** 함께 선다
  9.25~12.35  「재료는 다 있었고 전달만 없었어요」
     넓게 — 마을 위로 계기들이 떠 있고 사람들은 그 아래에서 일한다
  12.35~16.22 「그래서 이번 판은 짓기가 아니라 옮기기였어요」
     사람이 무덤 앞에서 **끄덕인다**(`nod`)

🔴 계기 셋(실·막대·격자)은 **형제 넷이 쓴 것 그대로**다. 새로 만들지 않는 것이 이 편의
   논지이고, 그림도 같은 규칙을 지킨다.
🔴 계기층은 뜻층 예산 밖이라, 계기가 셋이나 떠도 이 샷의 뜻층은 **보라 하나**다.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import instrument
import motions
import stage

SHOT, SID = 'body3', 'S3'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 12
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

L = [C.line_at(SID, i) for i in range(5)]
v, arms, graves = C.build(graves=True)
HOME = [tuple(a.location) for a in arms]
for i in C.IDLERS:
    C.hide_person(arms[i], False)
VIOLET, CYAN = C.mats()
TOOLS = [C.tool_at(k, C.CAST[i][1][0], C.CAST[i][1][1], face=C.home_face(i))
         for k, i in zip(C.TOOLS, C.IDLERS)]

# 형제 넷이 쓴 계기 셋 — 실(16-4) · 막대(16-1) · 격자(16-2·16-3)
AX, AY = C.CAST[0][1]
BX, BY = C.CAST[1][1]
BOND = instrument.thread((AX, AY, 0.92), (BX, BY, 0.92), CYAN, w=0.016)
BARS = [instrument.gauge((0, 0, 0), color='need_body'),
        instrument.gauge((0, 0, 0), color='need_work')]
for g, k in zip(BARS, (0.28, 0.74)):
    instrument.gauge_set(g, k)
CELLS = instrument.build(4, 2)
NOD_AT = L[4] + 0.35
WATCHER = 1

KEY = [(0.00, C.HIGH[0], C.HIGH[1]),
       (L[1] + 0.30, (-1.85, -6.85, 2.35), (-1.95, -1.65, 1.05)),
       (L[2] + 0.20, (0.55, -7.35, 3.15), (-0.45, -1.35, 1.45)),
       (L[3] + 0.30, C.HIGH[0], C.HIGH[1]),
       (DUR, (-0.55, -6.55, 2.05), (-0.95, -2.55, 0.95))]

cam = stage.light_camera()
C.lens(cam, C.MID_LENS)
stage.key_from_view(*C.HIGH)
print('[body3-16-5] %d비트 · %.2f초 · 줄 %s' % (BEATS, DUR, [round(x, 2) for x in L]))


def draw(fi):
    t = fi / C.FPS
    for n, g in enumerate(graves):
        C.grave_rise(g, 1.0)
        C.tool_show(TOOLS[n], True)
    instrument.gauge_at(BARS[0], AX + 0.32, AY - 0.12, 1.02)
    instrument.gauge_at(BARS[1], BX + 0.32, BY - 0.12, 1.02)
    w = arms[WATCHER]
    w.location = HOME[WATCHER]
    w.rotation_euler = (0, 0, C.home_face(WATCHER))
    if t < NOD_AT:
        C.work(w, C.CAST[WATCHER][0], t)
    else:
        stage.pose(w, motions.blend(motions.stop(t - NOD_AT), motions.nod(t - NOD_AT),
                                    C.ease((t - NOD_AT) / 0.30)))
    C.all_work(arms, t, HOME, skip=tuple(C.IDLERS) + (WATCHER,))
    C.fly(cam, KEY, t)


stage.bake(OUT, NF, C.FPS, draw, 'body3-16-5')
