"""ep16s-3 아웃트로 — 4 비트 · 4.09 초. **두 사람을 잇던 실이 끊긴다.**

  0.00~4.09 「다음 편에서는, 죽으면 그 사람 관계까지 지워졌어요」
     마주 선 두 사람 사이에 실이 걸려 있다가 **툭 끊어져 사라진다**

🔑 다음 편(16-4)의 무대만 세운다 — 왜 끊기는지, 무엇으로 고쳤는지는 그 편의 몫이다.
🔴 축을 바꿨다: 이 편은 **격자**의 이야기였고 다음 편은 **실**의 이야기다.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import instrument
import stage

SHOT, SID = 'outro', 'SO'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 4
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

v, arms, cells, _g = C.build()
HOME = [tuple(a.location) for a in arms]
C.show(cells, set())                       # 격자는 이 샷에서 쉰다 — 실만 남긴다
_V, _G, CYAN = C.mats()
AX, AY = C.CAST[0][1]
BX, BY = C.CAST[2][1]                      # 밭 옆 사람 ↔ 나무꾼
THREAD = instrument.thread((AX, AY, 0.95), (BX, BY, 0.95), CYAN, w=0.018)
CUT = 2.15

CAM_A = ((-1.35, -6.15, 2.35), (-3.35, -0.85, 0.95))
CAM_B = ((-1.05, -5.65, 2.15), (-3.45, -0.65, 0.90))
cam = stage.light_camera()
C.lens(cam, C.MID_LENS)
stage.key_from_view(*CAM_A)
print('[outro-16-3] %d비트 · %.2f초' % (BEATS, DUR))


def draw(fi):
    t = fi / C.FPS
    THREAD.hide_render = t >= CUT
    C.all_work(arms, t, HOME)
    u = C.ease(t / DUR)
    stage.aim(cam, C.lerp3(CAM_A[0], CAM_B[0], u), C.lerp3(CAM_A[1], CAM_B[1], u))


stage.bake(OUT, NF, C.FPS, draw, 'outro-16-3')
