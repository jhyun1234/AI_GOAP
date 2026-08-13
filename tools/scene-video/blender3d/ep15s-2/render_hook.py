"""ep15s-2 훅 — 5 비트 · 6.65 초. **위험이 오는데 한 명이 밥을 먹고 있다.**

자막 두 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~2.85 「늑대가 다가오는데 주민이 밥만 먹어요」
     넓게. 여섯이 제각기 제 일을 한다 → 오른쪽에서 붉은 덩이가 들어온다 →
     다섯이 **손을 멈추고 그쪽을 돌아본 뒤**(`startle` 의 멈칫) 뿔뿔이 **뛴다**.
     한 명은 **아무 반응도 안 한다** — 계속 먹는다
  2.98~6.15 「제가 안전장치라고 믿은 문장 때문이죠」
     카메라가 그 한 명에게 다가간다(컷이 아니라 **다가감**). 덩이가 그의 등 뒤까지 오는데
     그는 고개 한 번 안 든다. 화면에 남는 것은 **반응하지 않는 몸** 하나다

🔑 크기 판단: 첫 줄은 「여섯이 흩어진다」라 **넓어야** 하고, 둘째 줄은 「이 사람이 반응을
   안 한다」라 **가까워야** 한다. 그래서 컷 없이 넓은 데서 중간 크기로 밀고 들어간다 —
   렌즈는 샷 안에서 안 바꾸고(설계 §4-1) 카메라가 실제로 다가간다.
🔴 자막을 가려도 무슨 일인지 보여야 한다. 대비 하나로 선다: **다섯은 돌아보고 뛰는데
   하나는 고개도 안 든다.**
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import motions
import stage
import village

SHOT, SID = 'hook', 'SH'
OUT = C.out_dir(SHOT)
DUR = C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 5
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초 — 비트를 늘려라' % BEAT
LENS = 34

v, arms, fire, flakes, OBST = C.build()
HOME = [tuple(a.location) for a in arms]
d = C.danger()
cam = stage.light_camera()
C.lens(cam, LENS)
stage.key_from_view(*C.WIDE)           # 🔴 안 부르면 여섯이 통째로 역광이 된다

# 🔑 시각은 대본에서 읽는다. 다섯이 **차례로** 알아차린다 — 동시에 돌면 그건 군무다.
RUN_AT = [C.line_at(SID, 0) + 1.62 + 0.15 * i for i in range(5)]
NEAR = C.behind(C.HOME_H, C.WIDE[0], 1.05)
PUSH_AT = C.line_at(SID, 1) - 0.35     # 둘째 줄 직전부터 그 한 명에게 다가간다
DANGER_IN = 0.75
# 🔴 각을 손으로 정하지 않는다 — 소품을 피하는 각을 **재서** 고른다(common.flee_angles).
ANG = list(C.flee_angles([HOME[i][:2] for i in range(6)], OBST))
print('[hook-2] %d비트 · %.2f초 · 멈칫 %.2f · 첫 뛰기 %.2f · 다가감 %.2f'
      % (BEATS, DUR, RUN_AT[0] - C.NOTICE_LEAD, RUN_AT[0], PUSH_AT))


def draw(fi):
    t = fi / C.FPS

    # 덩이가 먼저 자리를 잡아야 「그쪽을 돌아본다」가 그쪽을 가리킨다
    dxy = C.danger_at(d, (t - DANGER_IN) / (DUR - DANGER_IN - 0.2), NEAR, t)
    d.hide_render = t < DANGER_IN * 0.9

    for i, arm in enumerate(arms):
        job = C.CAST[i][0]
        if i == C.HUNGRY:
            # 🔑 **이 사람만 아무것도 안 바뀐다.** 위험이 와도 하던 것을 계속한다
            C.work(arm, job, t)
            arm.location = HOME[i]
            arm.rotation_euler = (0, 0, C.home_face(i))
            continue
        C.startle(arm, i, HOME[i], job, t, RUN_AT[i], ANG[i], dxy)

    # 카메라 — 조망에서 그 한 명에게 **다가간다**(컷이 아니다)
    u = C.ease((t - PUSH_AT) / (DUR - PUSH_AT))
    (sx, sy, sz), (ax, ay, az) = C.WIDE
    (cx, cy, cz), (bx, by, bz) = C.MID
    stage.aim(cam, (sx + (cx - sx) * u, sy + (cy - sy) * u, sz + (cz - sz) * u),
              (ax + (bx - ax) * u, ay + (by - ay) * u, az + (bz - az) * u))

    village.flicker(fire, t)
    village.snowfall(flakes, t)


stage.bake(OUT, NF, C.FPS, draw, 'hook-2')
