"""ep16s-1 다섯 샷이 공유하는 것 — 마을·죽는 일곱·무덤·도구.

원문(M13 「사건과 흔적」)에서 이 편이 맡은 사건은 하나다: **일곱이 죽었는데 남은 것이
서로 구별이 안 됐다.** 대본은 `../../notes/ep16s-대본-3D.md` §1 이고, 그림 계약은 같은
문서 §0 이 다섯 편 공통으로 못박았다:

  이름          = **그 사람이 쓰던 도구**(도끼·곡괭이·망치·물뿌리개)
  이름이 없다   = 비석이 비어 있고 그 곁에 아무것도 안 선다
  이름을 새겼다 = 비석 옆에 그 도구가 선다(뜻층 보라 = 그 사람에게 닿은 자리)
  수            = 자막이 진다. 화면은 **개수·크기의 차이**만 진다

🔴 **글자를 안 쓴다.** 원문의 비석 표기 '{shortName} · Day {day}' 는 이 세계에 폰트가
   없어서 화면에 못 오른다. 도구가 그 자리를 진다 — 게임의 `AnimKind` 넷과 짝이라
   지어낸 것이 아니다.
🔴 **도구 넷 < 죽은 일곱.** 그래서 「일곱이 갈린다」를 일곱 종류로 그리지 않는다.
   S2 의 마지막 줄이 잡는 프레임 안에 **네 종류가 다 들어오게** 자리를 골랐고, 뒤쪽 셋은
   같은 종류가 겹친다(마을에 나무꾼이 둘인 것과 같다). 자막이 부르는 것도 「도끼면
   나무꾼, 물뿌리개면 밭 보던 사람」이지 「일곱이 다 다르다」가 아니다.
🔴 **불을 안 켠다.** 이 편의 뜻층은 보라 하나(비석 옆 도구)뿐이라 앰버를 켤 이유가 없다 —
   한 프레임 2색 예산을 절반만 쓴다.
"""
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import instrument       # noqa: E402
import motions          # noqa: E402
import stage            # noqa: E402
import village          # noqa: E402

EP = 'ep16s-1'
FPS = 30

# ── 죽는 일곱 — 무엇을 하다 어디서 쓰러지나 ─────────────────
# 🔑 **일하다 쓰러진다.** 그래야 손에서 굴러 나온 도구가 「이 사람이 하던 일」이 된다.
# 🔴 자리는 **소품을 피해서** 골랐다(밭 −2.7,−1.5 · 우물 3.0,−0.4 · 덤불 1.1,−2.9 ·
#    모닥불 −0.2,1.0 · 나무 −4.8,0.6 / 5.0,1.1 · 집 셋). 겹치면 카메라에서 한 물건이 된다.
# 🔴 남쪽(−Y)으로 몰지 마라 — 카메라가 전부 남쪽에 있어서 앞줄이 뒷줄을 가린다.
# 🔴 **첫 판은 8m 로 벌려 놨다가 버렸다.** 일곱이 다 들어오는 카메라가 그만큼 물러나면
#    주민이 화면 높이의 12분의 1 로 줄어 「사람이 무너진다」가 안 읽힌다(접점 시트로 잡음).
#    ep15s-5 훅의 실물이 5분의 1 이고, 그 크기를 기준으로 폭을 7.7m 안으로 접었다.
DEAD = [
    ('chop',   (-3.85,  0.30), (-4.8, 0.6),  'axe'),
    ('water',  (-2.85, -2.60), 'field',      'wateringcan'),
    ('mine',   (-1.15, -3.55), None,         'pickaxe'),
    ('hammer', ( 0.95,  1.35), (0.2, 4.0),   'hammer'),
    ('water',  (-1.45, -1.55), 'field',      'wateringcan'),
    ('mine',   ( 2.05, -2.30), None,         'pickaxe'),
    ('chop',   ( 3.20,  0.55), (5.0, 1.1),   'axe'),
]
# 🔴 **살아남은 한 사람이 있어야 한다.** 「남은 사람은 계속 밭을 간다」가 대본의 훅2 이고,
#    무덤 사이를 걷다 서서 둘러보는 것도(본문1) 이 사람이다. 죽음만 있는 화면은
#    「일곱이 죽었다」가 아니라 「아무도 없다」로 읽힌다.
ALIVE = ('farm', (-3.35, -1.05), 'field')
CAST = DEAD + [ALIVE]
LIVE_I = len(DEAD)

# 죽는 차례 — 훅 첫 줄(2.23초) 안에서 일곱이 다 무너진다. 0.22초 간격.
FALL0, FALL_GAP = 0.42, 0.22
RISE0, RISE_GAP = 2.70, 0.14      # 비석이 솟는 차례(훅 둘째 줄)

# S2 마지막 줄이 잡는 프레임 — 네 종류가 다 들어오는 자리다(도끼·물뿌리개·곡괭이·망치).
TOOL_SHOWCASE = (0, 1, 2, 4)

# 🔴 거리는 **재서 정했다** — ep15s-5 훅(거리 7.9m · 렌즈 32 · 주민이 화면 높이의 1/5)이
#    기준이다. 더 물러나면 마을이 작아지는 게 아니라 **빈 바닥이 커진다**(village.py 주석).
EYE = ((-0.10, -8.10, 3.15), (-0.30, -0.95, 1.00))     # 마을 + 일곱 자리가 한 프레임에
MID = ((1.85, -6.60, 2.25), (-0.85, -2.30, 0.75))     # 남쪽 무덤 넷 쪽으로
# 🔴 첫 판(2.6m · 46mm)은 **머리가 화면을 덮어 흰 공 하나**가 됐다(런북 §7). 3.4m · 40mm.
CLOSE = ((0.30, -6.30, 1.60), (-1.15, -3.50, 0.58))   # 무덤 하나 + 그 옆 도구
WIDE_LENS, MID_LENS, CLOSE_LENS = 30, 36, 40


def lens(cam, mm):
    cam.data.lens = mm


def ease(u):
    u = max(0.0, min(1.0, u))
    return u * u * (3 - 2 * u)


def out_dir(shot):
    d = os.path.join(stage.OUT_ROOT, EP, shot, 'frames')
    os.makedirs(d, exist_ok=True)
    return d


def seconds(shot_id):
    return stage.shot_seconds(EP, shot_id)


def line_at(shot_id, i):
    """그 샷 i 번째 자막 줄이 언제 시작하나(초). 🔴 몸짓 시각을 손으로 적지 마라."""
    return stage.shot_lines(EP, shot_id)[i][0]


def home_face(i):
    _job, (x, y), spot, *_ = CAST[i]
    if not spot:
        # 🔴 **누운 몸을 정면에서 잡으면 흰 공 하나가 된다**(런북 §7). 카메라가 전부
        #    남쪽에 있으므로 시선축과 어긋나는 각으로 세운다.
        return math.radians(58)
    s = village.SPOTS[spot] if isinstance(spot, str) else spot
    return math.atan2(y - s[1], x - s[0])


def build(graves=True):
    """마을 + 여덟 + (무덤 일곱). 반환 (village, arms, graves).

    🔴 무덤은 **조각 목록**으로 돌려받는다(`village.grave`). 하나만 만지면 나머지가
       화면에 남는 함정을 이 파이프라인이 두 번 밟았다."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    v = village.build()
    arms = []
    for i, (_job, (x, y), _spot, *_) in enumerate(CAST):
        _mesh, arm = stage.rigged(loc=(x, y, 0), rot_z=math.degrees(home_face(i)))
        arms.append(arm)
    village.outskirts()
    gs = []
    if graves:
        for i, (_job, (x, y), _spot, _tool) in enumerate(DEAD):
            parts = village.grave(x, y, -18 + 12 * i)
            gs.append([(o, tuple(o.scale), o.location.z) for o in parts])
    return v, arms, gs


# ── 무덤 · 도구 ────────────────────────────────────────
def grave_rise(parts, k):
    """무덤이 솟는다(k 0→1). 조각 **셋을 같이** 움직인다."""
    for o, sc, z0 in parts:
        o.hide_render = k <= 0.02
        o.scale = (sc[0], sc[1], max(sc[2] * k, 1e-4))
        o.location.z = z0 * k


def grave_scale(parts, s):
    """무덤 전체 크기(원문의 「루트 0.5배 축소」를 그리는 자리). 바닥에 붙인 채 커진다."""
    for o, sc, z0 in parts:
        o.hide_render = False
        o.scale = (sc[0] * s, sc[1] * s, sc[2] * s)
        o.location.z = z0 * s


def grave_show(parts, on):
    for o, _sc, _z0 in parts:
        o.hide_render = not on


TOOL_OF = {'axe': village.axe, 'pickaxe': village.pickaxe,
           'hammer': village.hammer, 'wateringcan': village.wateringcan}


def tool(kind, violet=False):
    """도구 하나. `violet=True` 면 뜻층 보라 — **그 사람에게 닿은 자리**라는 뜻이다."""
    root = TOOL_OF[kind]()
    if violet:
        mat = stage.meaning_mat('violet', strength=0.85, albedo_scale=0.16)
        for ch in root.children:
            ch.data.materials.clear()
            ch.data.materials.append(mat)
    return root


def tool_stand(root, x, y, s=1.0, face=0.0):
    """비석 **옆에** 세운다. 자루가 +Y 라 X 로 90° 세우면 머리가 위로 간다."""
    root.location = (x + 0.42, y - 0.20, 0.0)
    root.rotation_mode = 'XYZ'
    root.rotation_euler = (math.radians(90), 0.0, face)
    root.scale = (s, s, s)


def tool_show(root, on):
    for ch in root.children:
        ch.hide_render = not on


def bar(color='need_body'):
    return instrument.gauge((0, 0, 0), color=color)


# ── 사람 ──────────────────────────────────────────────
def work(arm, job, t):
    stage.pose(arm, motions.MOTIONS[job](t))


def all_work(arms, t, home, skip=()):
    for i, arm in enumerate(arms):
        if i in skip:
            continue
        arm.location = home[i]
        arm.rotation_euler = (0, 0, home_face(i))
        work(arm, CAST[i][0], t)


COLLAPSE_RATE = 1.15


def fall(arm, i, home, t, t0):
    """제 일을 하다 무너진다. 🔴 예비 동작을 넣지 마라 — 굶어 죽는 것은 예고가 없다."""
    arm.location = home
    arm.rotation_euler = (0, 0, home_face(i))
    job = CAST[i][0]
    if t < t0:
        work(arm, job, t)
        return
    stage.pose(arm, motions.blend(motions.MOTIONS[job](t),
                                  motions.collapse((t - t0) * COLLAPSE_RATE),
                                  ease((t - t0) / 0.24)))


def dead_hide(arms, gs=None):
    """무덤이 그 자리를 대신한다 — 죽은 일곱을 렌더에서 뺀다."""
    for i in range(len(DEAD)):
        arms[i].hide_render = True
        for ob in arms[i].children:
            ob.hide_render = True


def walk_to(arm, home, target, t, t0, span):
    """t0 부터 span 초 동안 target 쪽으로 걷는다. 반환 (자리, 걸은 시간).
    🔴 나아가는 거리는 `WALK_SPEED × 걸은 시간`이다 — 눈대중이면 발이 미끄러진다."""
    wt = min(max(t - t0, 0.0), span)
    ang = math.atan2(target[1] - home[1], target[0] - home[0])
    d = min(motions.WALK_SPEED * wt,
            math.hypot(target[0] - home[0], target[1] - home[1]))
    p = (home[0] + d * math.cos(ang), home[1] + d * math.sin(ang))
    arm.location = (p[0], p[1], 0)
    arm.rotation_euler = (0, 0, ang + math.pi)      # 규약: 정면은 (-cos θ, -sin θ)
    return p, wt


def walk_pose(arm, wt, blend=0.26):
    """걷기 ↔ 멈춤 이음새."""
    stage.pose(arm, motions.blend(motions.stop(wt), motions.walk(wt), ease(wt / blend)))


def lerp3(a, b, u):
    return tuple(p + (q - p) * u for p, q in zip(a, b))


def fly(cam, key, t):
    """(시각, 자리, 겨냥) 목록 위를 지나간다. 🔴 컷이 아니라 이동이다 — 크기가 바뀌는
    줄에서만 쓴다(3D_대본_문법 §4)."""
    if t <= key[0][0]:
        loc, at = key[0][1], key[0][2]
    elif t >= key[-1][0]:
        loc, at = key[-1][1], key[-1][2]
    else:
        for (t0, l0, a0), (t1, l1, a1) in zip(key, key[1:]):
            if t0 <= t <= t1:
                u = ease((t - t0) / max(t1 - t0, 1e-6))
                loc, at = lerp3(l0, l1, u), lerp3(a0, a1, u)
                break
    stage.aim(cam, loc, at)
