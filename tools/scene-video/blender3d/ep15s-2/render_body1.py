"""ep15s-2 본문1 — 9 비트 · 12.04 초. **무엇을 하려 하든 밥으로 되돌아간다.**

  0.00~1.34  ① 개발자 시점으로 컷. 위에서 마을을 들여다본다
  1.34~4.01  ②③ 그가 밭 쪽으로 몇 걸음 가다 **멈추고 먹는다**
  4.01~6.69  ④⑤ 이번엔 우물 쪽으로 가다 **또 멈추고 먹는다**
  6.69~9.36  ⑥⑦ 불 쪽으로 가다 **또**
  9.36~12.04 ⑧⑨ 계기층이 그 위에 뜬다 — 내가 이 사람을 들여다보고 있다는 표시다

🔑 자막이 말하는 것은 「숫자로 고른다 · 안전장치를 걸었다」이고, 화면이 하는 것은
   **그 안전장치가 실제로 하는 일**이다 — 무엇을 시작하든 밥으로 되돌린다.
🔴 값을 그리지 않는다(runbook §3-0). 계기층은 **들여다보는 순간의 표시**로만 쓴다.
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
import village

SHOT, SID = 'body1', 'S1'
OUT = C.out_dir(SHOT)
DUR = C.seconds(SID)
NF = round(DUR * C.FPS) + 1
BEATS = 9
BEAT = DUR / BEATS
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

# 🔑 세 번 되돌아간다. 가려던 쪽은 **소품이 있는 쪽**이라야 「무엇을 하려 했는지」가 읽힌다.
TRIES = [('field', 1 * BEAT, 2 * BEAT), ('well', 3 * BEAT, 2 * BEAT), ('fire', 5 * BEAT, 2 * BEAT)]
GO_SPAN = 0.95                     # 한 번에 걷는 시간(초) — 나머지는 멈춰서 먹는다
HOME_H = C.CAST[C.HUNGRY][1]

v, arms, fire, flakes = C.build()
HOME = [tuple(a.location) for a in arms]
cam = stage.light_camera()
stage.key_from_view(*C.DEV)

# 계기 — 🔴 **수치를 띄운다**(2026-08-13 지시). 막대 왼쪽이 배고픔, 오른쪽이 도망이다.
#    글자가 없으니 **높이가 곧 수**이고, 이 샷에서는 배고픔이 늘 위에 있다(안전장치).
G = C.gauges()
RIGHT = C.cam_right(C.DEV[0], C.DEV[1])
# 🔴 **강조는 대본이 정한다.** 줄 번호 → 그때 부르는 막대(0 배고픔 · 1 도망).
#    ① 「할 일마다 급한 정도가 숫자로」 둘 다 · ②③ 「배고픔보다 앞설 수 없다」 배고픔
LINES = stage.shot_lines(C.EP, SID)
FOCUS = {0: (0, 1), 1: 0, 2: 0}

print('[body1-2] %d비트 · %.2f초 · 비트 %.2f · 되돌아감 %d회' % (BEATS, DUR, BEAT, len(TRIES)))


def drift(t):
    """그가 지금 어디 있나. 🔑 나아간 거리는 **걸음이 만든다**(WALK_SPEED × 걸은 시간)."""
    x, y = HOME_H
    walked = 0.0
    for name, t0, _span in TRIES:
        u = min(max(t - t0, 0.0), GO_SPAN)
        if u <= 0:
            break
        s = village.SPOTS[name]
        ang = math.atan2(s.y - y, s.x - x)
        d = motions.WALK_SPEED * u
        x, y = x + d * math.cos(ang), y + d * math.sin(ang)
        walked += u
    return x, y, walked


def draw(fi):
    t = fi / C.FPS

    for i, arm in enumerate(arms):
        if i == C.HUNGRY:
            continue
        C.work(arm, C.CAST[i][0], t)
        arm.location = HOME[i]

    # ── ②~⑦ 가려다 만다 · 세 번 ──
    hero = arms[C.HUNGRY]
    x, y, walked = drift(t)
    going = any(t0 <= t < t0 + GO_SPAN for _n, t0, _s in TRIES)
    # 🔴 걷기↔밥 이음새를 안 이으면 관절이 순간이동한다
    k = 0.0
    for name, t0, _span in TRIES:
        if t0 <= t < t0 + GO_SPAN + 0.3:
            k = C.ease((t - t0) / 0.22) * (1.0 - C.ease((t - t0 - GO_SPAN) / 0.28))
    stage.pose(hero, motions.blend(motions.eat(t), motions.walk(walked), k))
    hero.location = (x, y, 0)
    if going:
        s = village.SPOTS[[n for n, t0, _s in TRIES if t0 <= t < t0 + GO_SPAN][0]]
        hero.rotation_euler = (0, 0, math.atan2(y - s.y, x - s.x))

    # ── 계기 — 막대 둘이 자라 오른다. 🔑 **배고픔이 도망보다 위**인 것이 안전장치다 ──
    on = C.ease((t - 0.55 * BEAT) / (1.1 * BEAT))
    C.gauge_show(G, on > 0.02)
    C.gauge_place(G, hero, RIGHT, z=0.95, emph=C.emphasis(LINES, t, FOCUS))
    instrument.gauge_set(G[0], instrument.gauge_k(C.HUNGER) * on)
    instrument.gauge_set(G[1], instrument.gauge_k(C.FLEE_BEFORE) * on)

    # 카메라 — 위에서 천천히 돈다. 🔴 안 돌리면 12 초짜리 홀드가 된다
    (sx, sy, sz), at = C.DEV
    a = math.atan2(sy - at[1], sx - at[0]) + math.radians(13) * (t / DUR)
    r = math.hypot(sx - at[0], sy - at[1])
    stage.aim(cam, (at[0] + r * math.cos(a), at[1] + r * math.sin(a), sz - 0.75 * (t / DUR)), at)

    village.flicker(fire, t)
    village.snowfall(flakes, t)


stage.bake(OUT, NF, C.FPS, draw, 'body1-2')
