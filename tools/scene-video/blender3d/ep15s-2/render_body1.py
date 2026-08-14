"""ep15s-2 본문1 — 9 비트 · 12.04 초. **내가 걸어 둔 안전장치가 실제로 하는 일.**

자막 세 줄과 몸짓의 대응(🔴 이게 이 샷의 계약이다):

  0.00~3.35 「주민은 할 일마다 급한 정도가 숫자로 있어요」
     개발자 시점. 주민 **넷** 머리 위에 막대 둘(배고픔·도망)이 **차례로** 떠오른다.
     넷 다 하던 일을 계속한다 — 여기서는 아직 아무 사건도 없다
  3.48~8.01 「그래서 걸어 뒀어요. 어떤 성격도 배고픔보다 앞설 수 없다」
     도망 막대에 **문턱 선**이 그어진다(= 배고픔 높이). 도망이 차오르다 그 선에 **부딪혀
     되밀린다** — 두 번. 같은 순간 배고픈 사람이 자리를 뜨려고 몸을 돌렸다가
     **도로 끌려와 다시 먹는다.** 막대가 하는 말과 몸이 하는 말이 같다
  8.14~11.56 「성격 때문에 굶어 죽으면 불공정하니까요」
     카메라가 그 사람 쪽으로 내려온다. 배고픔 막대가 **바닥까지 빠졌다가**, 그가 먹자
     다시 찬다 — 이 규칙이 막으려던 것이 바로 그 바닥이다

🔑 크기 판단: 이 샷은 처음부터 끝까지 **수를 견주는 줄**이다. 넓히면 막대가 안 읽히고,
   너무 붙으면 「넷 다 숫자가 있다」가 안 보인다. 그래서 넷이 들어오는 개발자 시점 하나로
   가되 **25° 로 눌러** 막대가 안 눕게 했다.
🔴 값을 그리는 것이 아니라 **값이 사람을 어떻게 붙잡는가**를 그린다(runbook §3-0).
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

# 🔑 막대를 다는 넷. 🔴 여섯 다 달면 **막대 숲**이 되고(ep15s-3 에서 재서 배웠다), 하나만
#    달면 「할 일마다 숫자가 있다」가 아니라 「저 사람만 특별하다」가 된다.
SHOWN = [0, 1, 3, C.HUNGRY]
BARS_IN = [0.55 + 0.42 * i for i in range(len(SHOWN))]

v, arms, fire, flakes, _obst = C.build()
HOME = [tuple(a.location) for a in arms]
hero = arms[C.HUNGRY]
G = {i: C.gauges(ceiling=True) for i in SHOWN}
RIGHT = C.cam_right(C.DEV[0], C.DEV[1])
LINES = stage.shot_lines(C.EP, SID)
# 🔴 **강조는 대본이 정한다.** ① 「숫자로」 둘 다 · ② 「배고픔보다 앞설 수 없다」 배고픔
#    ③ 「굶어 죽으면」 배고픔
FOCUS = {0: (0, 1), 1: 0, 2: 0}
cam = stage.light_camera()
C.lens(cam, C.DEV_LENS)
stage.key_from_view(*C.DEV)

CEIL_AT = C.line_at(SID, 1)            # 문턱 선이 그어지는 순간 = 「걸어 뒀어요」
BUMP = [CEIL_AT + 0.75, CEIL_AT + 2.55]   # 도망이 선에 부딪히는 두 번
BUMP_SPAN = 1.45
STARVE_AT = C.line_at(SID, 2)          # 「굶어 죽으면」 — 배고픔이 바닥까지 빠진다
print('[body1-2] %d비트 · %.2f초 · 문턱 %.2f · 부딪힘 %s · 바닥 %.2f'
      % (BEATS, DUR, CEIL_AT, ' '.join('%.2f' % b for b in BUMP), STARVE_AT))


def bump_k(t):
    """도망이 문턱을 향해 차올랐다 **되밀리는** 정도(0~1). 두 번 친다."""
    for b in BUMP:
        if b <= t < b + BUMP_SPAN:
            return math.sin(math.pi * (t - b) / BUMP_SPAN) ** 1.4
    return 0.0


def hero_pose(t):
    """배고픈 사람 — **자리를 뜨려다 도로 끌려와 먹는다.** 막대의 되밀림과 같은 시각이다.
    🔑 이게 「어떤 성격도 배고픔보다 앞설 수 없다」의 몸짓 판이다 — 규칙은 값이 아니라
       **행동에서** 보여야 한다."""
    k = bump_k(t)
    a0 = C.home_face(C.HUNGRY)
    # 몸이 마을 밖(도망 쪽)으로 40° 돌았다 돌아온다
    C.turn(hero, a0, a0 + math.radians(-42), k)
    hero.location = HOME[C.HUNGRY]
    return motions.blend(motions.eat(t), motions.stop(t), k)


def draw(fi):
    t = fi / C.FPS

    for i, arm in enumerate(arms):
        if i == C.HUNGRY:
            continue
        C.work(arm, C.CAST[i][0], t)
        arm.location = HOME[i]
        arm.rotation_euler = (0, 0, C.home_face(i))

    stage.pose(hero, hero_pose(t))

    # ── 막대 — 넷 위에 차례로 떠오르고, 문턱은 「걸어 뒀어요」에서 그어진다 ──
    em = C.emphasis(LINES, t, FOCUS)
    hunger_k = instrument.gauge_k(C.HUNGER)
    if t > STARVE_AT:
        # ③ 바닥까지 빠졌다가 **다시 찬다** — 규칙이 막으려던 것이 그 바닥이다
        u = (t - STARVE_AT) / max(DUR - STARVE_AT, 0.01)
        hunger_k *= 1.0 - 0.92 * math.sin(math.pi * min(u / 0.78, 1.0)) ** 1.2
    for n, i in enumerate(SHOWN):
        g = G[i]
        on = C.ease((t - BARS_IN[n]) / 0.55)
        C.gauge_show(g, on > 0.02, ceiling=(on > 0.02 and t > CEIL_AT))
        C.gauge_place(g, arms[i], RIGHT, z=0.95, emph=em)
        instrument.gauge_set(g[0], hunger_k * on if i == C.HUNGRY
                             else instrument.gauge_k(C.HUNGER) * on)
        # 🔑 도망은 문턱 **바로 아래**까지만 간다. 못 넘는 것이 이 샷의 사건이다
        base = instrument.gauge_k(C.FLEE_BEFORE)
        top = instrument.gauge_k(C.HUNGER) - 0.012
        instrument.gauge_set(g[1], (base + (top - base) * bump_k(t)) * on)

    # 카메라 — 위에서 천천히 돌다가, 셋째 줄에서 **그 사람 쪽으로 내려온다**
    (sx, sy, sz), at = C.DEV
    a = math.atan2(sy - at[1], sx - at[0]) + math.radians(11) * (t / DUR)
    r = math.hypot(sx - at[0], sy - at[1])
    dn = C.ease((t - STARVE_AT) / max(DUR - STARVE_AT, 0.01))
    hx, hy = HOME[C.HUNGRY][0], HOME[C.HUNGRY][1]
    stage.aim(cam,
              (at[0] + r * math.cos(a) - 0.55 * dn, at[1] + r * math.sin(a) + 1.15 * dn,
               sz - 1.35 * dn),
              (at[0] + (hx - at[0]) * dn, at[1] + (hy - at[1]) * dn, at[2] + 0.10 * dn))

    village.flicker(fire, t)
    village.snowfall(flakes, t)


stage.bake(OUT, NF, C.FPS, draw, 'body1-2')
