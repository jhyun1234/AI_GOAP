"""ep15s-4 훅 — 4 비트 · 4.99 초. **이미 뒤덮였는데 아직도 짓는다.**

자막 두 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~2.34 「집 주변이 모닥불로 뒤덮였어요」
     넓게. 마을 옆 땅이 모닥불로 빼곡하다. 여섯 개가 이미 타고 있다
  2.47~4.49 「목수가 짓고 또 짓거든요」
     카메라가 그 밭 쪽으로 들어간다. 목수가 하나를 **다 짓고, 옆으로 걸어가 또 짓는다**

🔑 크기 판단: 첫 줄은 「뒤덮였다」라 밭 전체가 한 프레임에 있어야 하고, 둘째 줄은
   「짓고 또 짓는다」라 그 한 사람의 마디가 보여야 한다 — 컷 없이 밀고 들어간다.
🔴 자막을 가려도 읽혀야 하는 것: **이미 저렇게 많은데 아직도 하나 더 짓는다.**
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import motions, stage, village

SHOT, SID = 'hook', 'SH'
OUT, DUR = C.out_dir(SHOT), C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 4
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT
LENS = 34

N = 9
v, arms, FIRES, SPOTS = C.build(N)
HOME = [tuple(a.location) for a in arms]
carp = arms[C.CARP]
HAMMER = stage.hold(carp, village.hammer(), 'hand.R', (0.0, 0.02, 0.0))
cam = stage.light_camera()
C.lens(cam, LENS)
stage.key_from_view(*C.EYE)

BASE = 6                                  # 이미 타고 있는 개수
ORDER = [6, 7, 8]
GO_AT = 0.15
PUSH_AT = C.line_at(SID, 1) - 0.30
PLAN = C.plan_builds(SPOTS, HOME[C.CARP][:2], ORDER, GO_AT)
print('[hook-4] %d비트 · %.2f초 · 불 %d개(이미 %d)' % (BEATS, DUR, N, BASE))


def draw(fi):
    t = fi / C.FPS
    C.others(arms, t, HOME)
    arms[C.ASKER].location = HOME[C.ASKER]
    arms[C.ASKER].rotation_euler = (0, 0, C.home_face(C.ASKER))
    stage.pose(arms[C.ASKER], motions.stop(t))

    p, done, growing, grow = C.run_builds(carp, PLAN, t)
    C.fire_all(FIRES, BASE + done, t, growing, grow)

    u = C.ease((t - PUSH_AT) / max(DUR - PUSH_AT, 0.01))
    (sx, sy, sz), (ax, ay, az) = C.EYE
    (cx, cy, cz), (bx, by, bz) = C.MID
    stage.aim(cam, (sx + (cx - sx) * u, sy + (cy - sy) * u, sz + (cz - sz) * u),
              (ax + (bx - ax) * u, ay + (by - ay) * u, az + (bz - az) * u))
    C.flicker_home(t)


stage.bake(OUT, NF, C.FPS, draw, 'hook-4')
