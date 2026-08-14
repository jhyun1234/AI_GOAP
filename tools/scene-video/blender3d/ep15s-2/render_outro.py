"""ep15s-2 아웃트로 — 4 비트 · 4.55 초. **다음 편의 무대를 남긴다.**

자막 한 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~4.20 「다음 편에서는, 목수가 떠나자 아무도 집을 못 얻어요」
     한 사람이 집 앞으로 걸어와 **옆의 빈자리를 향해 말한다**(`talk` — 부탁하는 몸짓).
     아무도 없다. 손이 내려가고 그는 그 자리에 선다

🔑 크기 판단: 여기서 보여 줄 것은 「사람 하나와 **비어 있는 자리**」뿐이다. 넓히면 마을
   구경이 되고, 붙이면 빈자리가 화면 밖으로 나간다 — 둘이 같이 들어오는 중간 크기다.
🔴 사건은 다음 편의 것이라 여기서 미리 쓰면 그 편이 반복으로 읽힌다. **무대만** 세운다.
🔴 엔진이 이 샷 위에 아웃트로 카드를 얹는다 — 그림이 **조용해야** 카드가 읽힌다.
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

SHOT, SID = 'outro', 'SO'
OUT = C.out_dir(SHOT)
DUR = C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 4
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT
# 🔴 38mm 는 화각 반각이 25.3° 라 **덤불 옆 사람(22.8°)이 화면 가장자리에 걸린다.**
#    44mm 면 22.2° 라 그가 빠지고, 부르는 사람이 화면의 3할을 채운다.
LENS = 44

# 집 앞에서 기다리는 자리와, **비어 있는 옆자리**(목수가 없는 자리)
# 🔴 걸음은 **샷 길이 안에서 끝나야** 한다. 집 앞(0.35, 2.45)까지는 5.1m 라 7.1 초 —
#    4.55 초짜리 샷에서 그는 영영 도착을 못 했고, 카메라만 집 쪽으로 가서 그는 화면
#    오른쪽 아래 모서리에 잘려 있었다. 1.5m 만 나서고, **빈자리 쪽을 향해** 부른다.
WAIT_AT = (-0.34, -1.11)
EMPTY = (0.90, 1.60)          # 아무도 없는 자리 — 다음 편에서 목수가 떠난 그 자리다

# 🔴 **카메라를 마을 북쪽에 세운다.** 그가 부르는 쪽(빈자리)이 북이라, 남쪽에서 잡으면
#    부르는 내내 뒤통수만 나온다 — 손짓이 이 샷의 유일한 몸짓인데 그게 안 보인다.
#    남쪽에서 잡던 판은 덤불 옆 사람 머리가 화면 오른쪽 아래 모서리를 막기도 했다.
CAM_A = ((3.10, 1.60, 2.05), (-0.10, -1.30, 0.85))
CAM_B = ((2.30, 0.90, 1.85), (-0.30, -1.15, 0.82))

v, arms, fire, flakes, _obst = C.build()
HOME = [tuple(a.location) for a in arms]
hero = arms[C.HUNGRY]
cam = stage.light_camera()
C.lens(cam, LENS)
stage.key_from_view(*CAM_B)

WALK_SPAN = math.hypot(WAIT_AT[0] - C.HOME_H[0],
                       WAIT_AT[1] - C.HOME_H[1]) / motions.WALK_SPEED
ASK_AT = min(WALK_SPAN + 0.15, DUR - 1.9)      # 도착하면 곧 부른다
DROP_AT = ASK_AT + 1.15                        # 아무도 없다 — 손이 내려간다
print('[outro-2] %d비트 · %.2f초 · 걸음 %.2f초 · 부름 %.2f' % (BEATS, DUR, WALK_SPAN, ASK_AT))


def draw(fi):
    t = fi / C.FPS
    u = t / DUR

    for i, arm in enumerate(arms):
        if i == C.HUNGRY:
            continue
        C.work(arm, C.CAST[i][0], t)
        arm.location = HOME[i]
        arm.rotation_euler = (0, 0, C.home_face(i))

    # ── 걸어와서 → 빈자리를 향해 **말한다** → 손이 내려간다 ──
    wt = min(max(t, 0.0), WALK_SPAN)
    ang = math.atan2(WAIT_AT[1] - C.HOME_H[1], WAIT_AT[0] - C.HOME_H[0])
    dd = motions.WALK_SPEED * wt
    x, y = C.HOME_H[0] + dd * math.cos(ang), C.HOME_H[1] + dd * math.sin(ang)
    hero.location = (x, y, 0)
    if t < ASK_AT:
        hero.rotation_euler = (0, 0, ang + math.pi)
        stage.pose(hero, motions.blend(motions.stop(t), motions.walk(wt),
                                       C.ease(min(wt / 0.26, (WALK_SPAN - wt) / 0.30))))
    else:
        C.face(hero, EMPTY, (x, y))            # 🔑 **빈자리 쪽을 본다**
        ask = motions.blend(motions.reach(t - ASK_AT), motions.talk(t - ASK_AT),
                            C.ease((t - ASK_AT - motions.REACH_DUR) / 0.30))
        # 아무도 없다 — 손이 내려가고 그대로 선다
        ask = motions.blend(ask, motions.stop(t), C.ease((t - DROP_AT) / 0.55))
        stage.pose(hero, motions.blend(motions.walk(wt), ask,
                                       C.ease((t - ASK_AT) / 0.26)))

    k = C.ease(u)
    stage.aim(cam,
              tuple(a + (b - a) * k for a, b in zip(CAM_A[0], CAM_B[0])),
              tuple(a + (b - a) * k for a, b in zip(CAM_A[1], CAM_B[1])))

    village.flicker(fire, t)
    village.snowfall(flakes, t)


stage.bake(OUT, NF, C.FPS, draw, 'outro-2')
