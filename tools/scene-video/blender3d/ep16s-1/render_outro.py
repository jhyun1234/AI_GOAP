"""ep16s-1 아웃트로 — 4 비트 · 4.07 초. **알림 칸이 켜졌다 곧 꺼진다.**

자막 한 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~4.07 「그런데 굶고 있다는 알림엔 아직 누구인지가 없었어요」
     마을 위 공중 격자에서 칸 하나가 **켜졌다 곧 꺼진다.** 아래 마을에서는 누가
     그 사람인지 아무 표시가 없다 — 사람들은 그냥 제 일을 한다

🔑 **다음 편의 무대만 세운다.** 16-2 가 지는 사건(알림 칸이 한 개뿐이라 일곱 건이 서로를
   지운다)은 여기서 풀지 않는다 — 한 번 켜졌다 꺼지는 것까지가 미리보기다.
🔴 **축을 바꿨다.** 이 편의 판(무덤·도구)을 끌고 오면 같은 이야기로 읽힌다. 그래서
   무덤 옆 도구를 끄고 격자만 켠다 — 뜻층은 빨강 하나(위험)뿐이다.
🔴 엔진이 이 샷 위에 아웃트로 카드를 얹는다 — 그림이 조용해야 카드가 읽힌다.
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

v, arms, graves = C.build()
HOME = [tuple(a.location) for a in arms]
C.dead_hide(arms)
for g in graves:
    C.grave_rise(g, 1.0)

CELLS = instrument.build(4, 2)
RED = stage.meaning_mat('red', strength=0.95, albedo_scale=0.14)
CYAN = stage.instrument_mat(strength=1.0)
LIT = 2                                    # 켜지는 칸 — 왼쪽 위 줄의 셋째
ON, OFF = 0.85, 2.35                       # 켜졌다 **곧** 꺼진다. 그게 다음 편의 사건이다

CAM_A = ((2.70, -7.80, 4.15), (-0.50, -0.25, 1.80))
CAM_B = ((2.10, -7.20, 3.90), (-0.55, -0.05, 1.90))
cam = stage.light_camera()
C.lens(cam, C.MID_LENS)
stage.key_from_view(*CAM_A)
print('[outro-16-1] %d비트 · %.2f초 · 칸 %d' % (BEATS, DUR, len(CELLS)))


def draw(fi):
    t = fi / C.FPS
    for n, cell in enumerate(CELLS):
        instrument.show(cell, True)
        instrument.paint(cell, RED if (n == LIT and ON <= t < OFF) else CYAN)
    C.work(arms[C.LIVE_I], C.CAST[C.LIVE_I][0], t)
    u = C.ease(t / DUR)
    stage.aim(cam, C.lerp3(CAM_A[0], CAM_B[0], u), C.lerp3(CAM_A[1], CAM_B[1], u))


stage.bake(OUT, NF, C.FPS, draw, 'outro-16-1')
