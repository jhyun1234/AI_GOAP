"""ep16s-4 아웃트로 — 4 비트 · 4.25 초. **마을이 다시 돌아간다.**

  0.00~4.25 「다음 편에서는, 같은 판을 다시 켰더니 회상이 달라졌어요」
     넓게. 밭 갈고 나무 패고 물 긷고 — **같은 판이 처음부터 다시 도는** 그림이다

🔑 다음 편(16-5)의 무대만 세운다. 무엇이 달라졌는지는 그 편의 몫이다.
🔴 축을 바꿨다 — 이 편은 **실**의 이야기였고 다음 편은 **같은 판의 회상**이다.
   그래서 굳은 실도 무덤도 이 샷에서는 안 켠다.
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

v, arms, _g = C.build()
HOME = [tuple(a.location) for a in arms]
CAM_A = ((0.35, -8.35, 3.15), (-0.55, -1.15, 1.05))
CAM_B = ((-0.55, -7.85, 2.95), (-0.65, -0.95, 1.10))
cam = stage.light_camera()
C.lens(cam, C.WIDE_LENS)
stage.key_from_view(*CAM_A)
print('[outro-16-4] %d비트 · %.2f초' % (BEATS, DUR))


def draw(fi):
    t = fi / C.FPS
    C.all_work(arms, t, HOME)
    u = C.ease(t / DUR)
    stage.aim(cam, C.lerp3(CAM_A[0], CAM_B[0], u), C.lerp3(CAM_A[1], CAM_B[1], u))


stage.bake(OUT, NF, C.FPS, draw, 'outro-16-4')
