"""ep16s-2 아웃트로 — 4 비트 · 4.81 초. **한 색이 격자를 다 채운다.**

자막 한 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~4.81 「다음 편에서는, 기록을 모아 놓으니 한 사람이 다 덮어 버렸어요」
     칸이 하나씩 **같은 색으로** 채워져 결국 격자 전체가 한 색이 된다 —
     다른 사람의 칸이 **안 보인다**

🔑 **다음 편의 무대만 세운다.** 16-3 이 지는 사건(명령 거부 아홉 연발이 명부를 덮는다)은
   여기서 풀지 않는다.
🔴 축을 바꿨다 — 이 편은 칸이 **모자란** 이야기였고, 다음 편은 칸이 **한 사람으로 차는**
   이야기다. 같은 격자를 쓰되 방향이 반대다.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import stage

SHOT, SID = 'outro', 'SO'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 4
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

v, arms, cells = C.build()
HOME = [tuple(a.location) for a in arms]
FILL0, FILL_GAP = 0.55, 0.32
CAM_A = ((0.15, -7.15, 4.45), (-0.45, -0.05, 1.95))
CAM_B = ((-0.35, -6.75, 4.25), (-0.50, 0.15, 2.05))
cam = stage.light_camera()
C.lens(cam, C.MID_LENS)
stage.key_from_view(*CAM_A)
print('[outro-16-2] %d비트 · %.2f초 · 칸 %d' % (BEATS, DUR, len(cells)))


def draw(fi):
    t = fi / C.FPS
    n = max(0, min(len(cells), int((t - FILL0) / FILL_GAP) + 1))
    C.light(cells, set(range(n)))
    C.all_work(arms, t, HOME)
    u = C.ease(t / DUR)
    stage.aim(cam, C.lerp3(CAM_A[0], CAM_B[0], u), C.lerp3(CAM_A[1], CAM_B[1], u))


stage.bake(OUT, NF, C.FPS, draw, 'outro-16-2')
