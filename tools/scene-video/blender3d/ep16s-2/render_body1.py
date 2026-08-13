"""ep16s-2 본문1 — 12 비트 · 16.12 초. **한 칸이 일곱 건을 지웠다.**

자막 네 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~3.72  「일곱 건이 났는데 알림 자리는 한 칸이었어요」
     가깝게. **같은 칸 하나**가 켜졌다 꺼지기를 일곱 번 반복한다
  3.72~7.90  「새 소식이 앞엣것을 덮어서 마지막 하나만 남았고요」
     마지막 한 번만 **켜진 채** 멈춘다
  7.90~12.30 「알림이 뜨는 자리를 전부 세어 봤어요. 열네 군데였는데,」
     물러나며 칸 여덟이 **하나씩 깜빡인다** — 자리는 많은데 전부 순간이다
  12.30~16.12 「위험이 풀릴 때까지 남는 줄은 한 개도 없었어요」
     깜빡이던 칸이 **전부 꺼진다.** 마을은 그대로 일한다

🔴 「없다」를 빈 화면으로 그리지 않는다(런북 §7) — 꺼진 격자 아래에서 **사람들이 일한다.**
🔑 일곱 번은 자막이 세고, 화면은 **같은 자리가 반복해서 깜빡이는 것**만 진다.
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
BEATS = 12
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

L = [C.line_at(SID, i) for i in range(4)]
v, arms, cells = C.build()
HOME = [tuple(a.location) for a in arms]
LIT = 2
BLINK0, BLINK_T = 0.45, 0.44          # 일곱 번 · 한 번에 0.44초(켬 0.24 · 끔 0.20)
SWEEP0, SWEEP_GAP = L[2] + 0.25, 0.34  # 열네 곳 — 칸 여덟이 차례로 깜빡인다

KEY = [(0.00, C.CLOSE[0], C.CLOSE[1]),
       (L[1] + 0.60, (-0.15, -4.65, 4.45), (-0.35, 0.75, 2.35)),
       (L[2] + 0.40, C.MID[0], C.MID[1]),
       (DUR, C.EYE[0], C.EYE[1])]

cam = stage.light_camera()
C.lens(cam, C.CLOSE_LENS)
stage.key_from_view(*C.CLOSE)
print('[body1-16-2] %d비트 · %.2f초 · 줄 %s' % (BEATS, DUR, [round(x, 2) for x in L]))


def draw(fi):
    t = fi / C.FPS
    on = set()
    if t < L[1]:
        n = int((t - BLINK0) / BLINK_T)
        if 0 <= n < 7 and (t - BLINK0) % BLINK_T < 0.24:
            on.add(LIT)
    elif t < L[2]:
        on.add(LIT)                                   # 마지막 하나만 남는다
    elif t < L[3]:
        for n in range(len(cells)):
            t0 = SWEEP0 + n * SWEEP_GAP
            if t0 <= t < t0 + 0.30:
                on.add(n)
    C.light(cells, on)                                # L[3] 뒤에는 전부 꺼진다
    C.all_work(arms, t, HOME)
    C.fly(cam, KEY, t)


stage.bake(OUT, NF, C.FPS, draw, 'body1-16-2')
