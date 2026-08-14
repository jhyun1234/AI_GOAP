"""ep16s-2 본문3 — 14 비트 · 19.79 초. **칸을 사람 수만큼 세우고, 그 사람에게 간다.**

자막 여섯 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~2.85   「그런데 켜 보니 또 걸렸어요」        칸 하나가 켜진다
  2.85~6.35   「굶는 사람이 있다고만 뜨지, 누군지를 몰랐거든요」
      넷이 **하던 일을 멈추고 똑같이 선다** — 겉으로 아무도 안 갈린다
  6.35~9.95   「그래서 마을 합계를 버리고 한 사람씩 세웠어요」
      칸이 **넷으로 갈라지고** 칸마다 **실**이 한 사람에게 내려간다
  9.95~12.80  「급한 사람이 맨 위고요」
      맨 위 칸의 실 끝에 선 사람의 **막대가 바닥**이다
  12.80~16.30 「그 줄을 누르면 카메라가 그 사람한테 날아가요」
      카메라가 **그 사람에게 붙고** 발치에 **원**이 그려진다
  16.30~19.79 「이제 누가 위험한지 보고, 바로 그 사람한테 갈 수 있어요」
      다른 사람이 **다가와**(`walk`) 손을 **뻗는다**(`reach`)

🔴 실은 만들 때 두 점이 굳는다 — 그래서 이 샷에서 실이 걸린 넷은 **제자리에 선다.**
   움직이는 사람은 마지막에 다가오는 한 명뿐이고, 그 사람에게는 실이 없다.
"""
import math
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
BEATS = 14
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

L = [C.line_at(SID, i) for i in range(6)]
v, arms, cells = C.build()
HOME = [tuple(a.location) for a in arms]
RED, CYAN = C.mats()

# 🔑 위 줄 넷이 **사람 넷의 줄**이다. 급한 사람(굶는 사람)이 맨 앞 = 화면 왼쪽.
ROW = [0, 1, 2, 3]
THREADS = []
for n, who in enumerate(C.LISTED):
    cx, cy, cz = cells[ROW[n]].location
    px, py = C.CAST[who][1]
    THREADS.append(instrument.thread((cx, cy, cz - 0.28), (px, py, 1.05), CYAN, w=0.014))
for th in THREADS:
    th.hide_render = True

# 🔴 막대는 **급한 사람만** 바닥이다. 넷 다 달면 「누가 급한가」가 아니라 계기판이 된다
BARS = [instrument.gauge((0, 0, 0), color='need_body') for _ in C.LISTED]
LEVEL = [0.06, 0.72, 0.55, 0.63]
for g, k in zip(BARS, LEVEL):
    instrument.gauge_set(g, k)
RING = instrument.ring(0.85)
instrument.ring_at(RING, *C.CAST[C.HUNGRY][1])
instrument.ring_show(RING, False)

HELPER = 1                                   # 다가오는 사람 — 나무꾼
HX, HY = C.CAST[C.HUNGRY][1]
COME_AT, COME_SPAN = L[5] + 0.15, 2.30
COME_TO = (HX - 1.05, HY + 0.35)

KEY = [(0.00, C.MID[0], C.MID[1]),
       (L[2] - 0.10, (-0.15, -8.35, 4.75), (-0.50, -0.35, 1.85)),
       (L[3] - 0.05, (-0.20, -7.35, 4.15), (-0.85, -0.85, 1.65)),
       (L[4] + 0.90, C.NEAR[0], C.NEAR[1]),
       (DUR, (-0.05, -5.95, 1.75), (-1.15, -2.40, 0.80))]

cam = stage.light_camera()
C.lens(cam, C.MID_LENS)
stage.key_from_view(*C.MID)
print('[body3-16-2] %d비트 · %.2f초 · 줄 %s' % (BEATS, DUR, [round(x, 2) for x in L]))


def draw(fi):
    t = fi / C.FPS
    split = t >= L[2] + 0.35                       # 칸이 사람 수만큼 서는 시각
    on = set(ROW) if split else {2}
    C.light(cells, on)
    for n, th in enumerate(THREADS):
        th.hide_render = not (split and t >= L[2] + 0.35 + 0.18 * n)
    # 막대는 「급한 사람이 맨 위」를 말하는 줄에서만 켠다
    show_bars = t >= L[3] - 0.10
    for n, (g, who) in enumerate(zip(BARS, C.LISTED)):
        px, py = C.CAST[who][1]
        instrument.gauge_at(g, px + 0.30, py - 0.10, 1.02)
        for key in ('track', 'fill'):
            g[key].hide_render = not show_bars
    instrument.ring_show(RING, t >= L[4] + 0.55)
    for i, arm in enumerate(arms):
        if i == HELPER and t >= COME_AT:
            p, wt = C.walk_to(arm, HOME[HELPER], COME_TO, t, COME_AT, COME_SPAN)
            if wt < COME_SPAN:
                C.walk_pose(arm, wt)
            else:
                stage.pose(arm, motions.blend(motions.stop(t - COME_AT - COME_SPAN),
                                              motions.reach(t - COME_AT - COME_SPAN),
                                              C.ease((t - COME_AT - COME_SPAN) / 0.35)))
        elif i in C.LISTED and t >= L[1] + 0.20:
            C.stand(arm, i, HOME[i], t)            # 넷이 똑같이 선다
        else:
            arm.location = HOME[i]
            arm.rotation_euler = (0, 0, C.home_face(i))
            C.work(arm, C.CAST[i][0], t)
    C.fly(cam, KEY, t)


stage.bake(OUT, NF, C.FPS, draw, 'body3-16-2')
