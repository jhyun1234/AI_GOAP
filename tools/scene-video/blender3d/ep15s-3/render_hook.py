"""ep15s-3 훅 — 5 비트 · 6.92 초. **성격이 목수를 마을 밖으로 내보낸다.**

자막 두 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~3.24 「목수가 이사 가자 아무도 집을 못 얻었어요」
     마을이 산다. 목수가 **망치를 들고** 제 자리에서 망치질하고, 집 없는 주민이 그를 본다
  3.37~6.43 「성격이 목수를 마을 밖으로 내보냈거든요」
     목수가 망치질을 멈추고 **바깥으로 돌아서서 뛰어 나간다**(망치를 든 채).
     남은 다섯이 하던 일을 멈추고 **그쪽으로 돌아선다.** 카메라가 그 길을 따라 넓어진다

🔴 앞 판은 목수를 `hide_render` 로 **지워 놓고** 「그 자리가 비었다」를 말했다. 없는 사람은
   아무 몸짓도 못 하고, 자막이 말하는 「내보냈다」는 **나가는 것**이지 「없다」가 아니다.
🔴 카메라와 마을을 **둘 다** 넓혔다(2026-08-13 사용자 지시). 나가는 길이 화면에 있어야
   「밖으로」가 보인다.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common as C
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import motions, stage, village

SHOT, SID = 'hook', 'SH'
OUT, DUR = C.out_dir(SHOT), C.seconds('SH')
NF = round(DUR * C.FPS) + 1
BEATS = 5
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

v, arms, fire, _out = C.build()
HOME = [tuple(a.location) for a in arms]
carp = arms[C.CARPENTER]
HAMMER = stage.hold(carp, village.hammer(), 'hand.R', (0.0, 0.02, 0.0))
cam = stage.light_camera()
C.lens(cam, C.WIDE_LENS)
stage.key_from_view(*C.AXIS)

# 🔴 시각은 **대본에서 읽는다.** 「내보냈거든요」 줄이 시작할 때 뛰기 시작해야 한다.
TURN_AT = C.line_at(SID, 1) - 0.25      # 예비 — 말보다 살짝 먼저 몸이 돌아선다
RUN_AT = C.line_at(SID, 1)
RUN_SPAN = DUR - RUN_AT
# 집 없는 주민은 처음부터 목수를 본다 — 그가 나가는 것을 **지켜보는 사람**이 있어야 사건이다
C.face(arms[C.HOMELESS], C.CARP_HOME, HOME[C.HOMELESS])
print('[hook-3] %d비트 · %.2f초 · 돌아섬 %.2f · 뛰기 %.2f' % (BEATS, DUR, TURN_AT, RUN_AT))


def draw(fi):
    t = fi / C.FPS
    C.others(arms, t, HOME, watch=C.FAR_AT, watch_at=RUN_AT + 0.2)

    # 집 없는 주민 — 목수를 보다가, 나가는 것을 보고 한 걸음 따라 나섰다 멈춘다
    hp = arms[C.HOMELESS]
    hp.location = HOME[C.HOMELESS]
    C.face(hp, C.CARP_HOME if t < RUN_AT else C.FAR_AT, HOME[C.HOMELESS])
    stage.pose(hp, motions.stop(t) if t < RUN_AT + 0.3
               else motions.blend(motions.stop(t), motions.reach(t - RUN_AT - 0.3),
                                  C.ease((t - RUN_AT - 0.3) / 0.5)))

    # 목수 — 망치질 → **돌아선다**(예비) → 뛰어 나간다
    # 🔴 돌아서는 것을 빼면 망치질에서 곧장 뛰기로 튄다. 방향도 한 프레임에 뒤집힌다.
    p, wt = C.walk_to(carp, C.CARP_HOME, C.FAR_AT, t, RUN_AT, RUN_SPAN, run=True)
    a_run = carp.rotation_euler[2]                      # walk_to 가 넣어 준 뛰는 쪽
    if wt > 0.0:
        C.walk_pose(carp, wt, run=True)
    else:
        a_work = C.face(carp, village.SPOTS['fire'], C.CARP_HOME)
        k = C.ease((t - TURN_AT) / (RUN_AT - TURN_AT))
        d = (a_run - a_work + math.pi) % (2 * math.pi) - math.pi
        carp.rotation_euler = (0, 0, a_work + d * k)
        stage.pose(carp, motions.blend(motions.hammer(t), motions.stop(t), k))

    # 카메라 — **나가는 사람을 따라간다.** 안 따라가면 그가 프레임 밖으로 사라지는데,
    # 그러면 화면이 말하는 것은 「나갔다」가 아니라 「없어졌다」가 된다.
    # 🔑 목수를 화면 한가운데 두지 않는다 — 마을과 그 사이를 겨눠야 **떨어지는 것**이 보인다.
    u = C.ease((t - RUN_AT + 0.45) / (DUR - RUN_AT))
    (sx, sy, sz), (ax, ay, az) = C.AXIS
    stage.aim(cam, (sx - 0.35 * u, sy - 2.10 * u, sz + 1.05 * u),
              (ax + (p[0] - ax) * 0.40 * u, ay + (p[1] - ay) * 0.40 * u, az))
    village.flicker(fire, t)


stage.bake(OUT, NF, C.FPS, draw, 'hook-3')
