"""ep16s-1 다섯 샷이 공유하는 것 — 마을·죽는 일곱·무덤·도구.

🔴 **2026-08-14 전면 재조정.** 첫 판은 게이트를 다 통과하고도 사용자 판정에서
   「처음 보는 사람이 내용을 전혀 이해 못 한다」로 반려됐다. 완성본 실측이 이유였다:
   **인물 키가 화면 세로의 4%**(그림판이 화면의 44% · 그 안에서 인물이 10%).
   자막이 다 말하고 화면은 배경이었다.

   고친 것 셋:
   ① 그림판 970×846 → **1080×1050** (`stage.BAND_*` · `style.css .vis`)
   ② 카메라를 좌표가 아니라 **크기에서 역산**한다 — `stage.cam_for(at, frac=…)`.
      `frac` = 사람 키가 화면 세로에서 차지하는 비율. 이 회차는 **0.28~0.50**
      (옛 판은 전 샷 0.10~0.32였다).
   ③ **한 프레임에 일곱을 다 넣지 않는다.** 「수는 자막이 진다」는 규약을 이제 구성에서도
      지킨다 — 카메라는 한둘을 크게 잡고, 일곱은 **카메라 이동**이 센다.

그림 계약(대본 §0 · 다섯 편 공통):
  이름 = **그 사람이 쓰던 도구** · 이름이 없다 = 빈 비석에 아무것도 안 섬 ·
  이름을 새겼다 = 비석 옆에 그 도구가 선다(뜻층 보라) · 수(일곱·+1)는 자막.
🔴 불을 안 켠다 — 뜻층은 보라 하나면 된다.
"""
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import motions          # noqa: E402
import stage            # noqa: E402
import village          # noqa: E402

EP = 'ep16s-1'
FPS = 30

# ── 죽는 일곱 ────────────────────────────────────────
# 🔴 **모으고 줄였다.** 옛 판은 7.1m 로 벌려 놔서 일곱이 한 프레임에 들어오려면 카메라가
#    8m 밖으로 나가야 했고, 그 순간 사람이 안 보였다. 지금은 **세 무리**로 모아 두고
#    카메라가 무리 하나씩 크게 잡는다 — 일곱은 카메라가 옮겨 가며 센다.
# 🔴 소품(밭 −2.7,−1.5 · 우물 3.0,−0.4 · 덤불 1.1,−2.9 · 모닥불 −0.2,1.0)을 피해서 골랐다.
DEAD = [
    ('water',  (-2.55, -2.45), 'field',       'wateringcan'),   # 무리 A
    ('mine',   (-1.65, -3.15), None,          'pickaxe'),       # 무리 A
    ('chop',   (-3.35, -3.35), (-4.3, -3.9),  'axe'),           # 무리 A
    ('hammer', (-0.45, -1.55), (0.2, 4.0),    'hammer'),        # 무리 B
    ('water',  ( 0.55, -2.35), 'field',       'wateringcan'),   # 무리 B
    ('mine',   ( 2.05, -1.95), None,          'pickaxe'),       # 무리 C
    ('chop',   ( 2.75, -3.15), (3.7, -3.5),   'axe'),           # 무리 C
]
GROUP_A, GROUP_B, GROUP_C = (0, 1, 2), (3, 4), (5, 6)
ALIVE = ('farm', (-3.65, -1.05), 'field')      # 끝까지 밭을 가는 사람
CAST = DEAD + [ALIVE]
LIVE_I = len(DEAD)

FALL0, FALL_GAP = 0.30, 0.22
RISE0, RISE_GAP = 0.40, 0.16

# 🔴 크기 손잡이 — 좌표가 아니라 **사람이 화면에서 얼마나 큰가**로 적는다.
FIG_WIDE, FIG_MID, FIG_CLOSE = 0.28, 0.38, 0.50
LENS_WIDE, LENS_MID, LENS_CLOSE = 34, 38, 42


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


def spot_of(i):
    return CAST[i][1] if isinstance(i, int) else i


def mid_of(idx):
    """무리의 한가운데. 카메라는 사람이 아니라 **무리**를 겨눌 때가 많다."""
    pts = [spot_of(i) for i in idx]
    return (sum(p[0] for p in pts) / len(pts), sum(p[1] for p in pts) / len(pts))


def cam_at(who, frac=FIG_MID, lens_mm=LENS_MID, yaw=-90.0, elev=13.0, dz=0.62,
           off=(0.0, 0.0)):
    """그 사람(또는 자리)을 **frac 크기로** 잡는 카메라. 반환 (loc, at)."""
    x, y = spot_of(who)
    return stage.cam_for((x + off[0], y + off[1], dz), frac=frac, lens=lens_mm,
                         yaw=yaw, elev=elev)


def face_yaw(i, off=32.0):
    """그 사람의 **앞쪽**에서 잡는 카메라 방위(도).

    🔴 첫 판은 카메라 방위를 −90(남쪽)으로 박아 두고 사람은 제 일감을 보게 세웠다.
       그래서 화면에 **뒤통수**가 찼다 — 무너지는 순간에도 얼굴 쪽이 안 보였다.
    🔑 규약상 정면은 `(−cos θ, −sin θ)`(`home_face` 의 반대쪽)이므로 카메라는 거기서
       `off` 만큼 비껴 선다. 정면 0° 는 평면적이라 30° 안팎이 기본이다."""
    return math.degrees(home_face(i)) + 180.0 + off


def home_face(i):
    _job, (x, y), spot, *_ = CAST[i]
    if not spot:
        # 🔴 누운 몸을 정면에서 잡으면 흰 공 하나가 된다(런북 §7).
        return math.radians(58)
    s = village.SPOTS[spot] if isinstance(spot, str) else spot
    return math.atan2(y - s[1], x - s[0])


def build(graves=True):
    """마을 + 여덟 + (무덤 일곱). 반환 (village, arms, graves)."""
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
    for o, sc, z0 in parts:
        o.hide_render = k <= 0.02
        o.scale = (sc[0], sc[1], max(sc[2] * k, 1e-4))
        o.location.z = z0 * k


def grave_scale(parts, s):
    for o, sc, z0 in parts:
        o.hide_render = False
        o.scale = (sc[0] * s, sc[1] * s, sc[2] * s)
        o.location.z = z0 * s


def grave_show(parts, on):
    for o, _sc, _z0 in parts:
        o.hide_render = not on


TOOL_OF = {'axe': village.axe, 'pickaxe': village.pickaxe,
           'hammer': village.hammer, 'wateringcan': village.wateringcan}

VIOLET = None


def violet():
    global VIOLET
    if VIOLET is None:
        VIOLET = stage.meaning_mat('violet', strength=0.88, albedo_scale=0.16)
    return VIOLET


def tool(kind, tinted=False):
    root = TOOL_OF[kind]()
    if tinted:
        m = violet()
        for ch in root.children:
            ch.data.materials.clear()
            ch.data.materials.append(m)
    return root


def tool_stand(root, x, y, s=1.5, face=0.0):
    """비석 옆에 세운다. 🔴 1.0 배는 화면에서 보라 점이다 — 1.5 가 기본이다."""
    root.location = (x + 0.40, y - 0.18, 0.0)
    root.rotation_mode = 'XYZ'
    root.rotation_euler = (math.radians(90), 0.0, face)
    root.scale = (s, s, s)


def tool_show(root, on):
    for ch in root.children:
        ch.hide_render = not on


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


def hide_person(arm, on):
    arm.hide_render = not on
    for ob in arm.children:
        ob.hide_render = not on


def dead_hide(arms):
    for i in range(len(DEAD)):
        hide_person(arms[i], False)


def walk_to(arm, home, target, t, t0, span):
    wt = min(max(t - t0, 0.0), span)
    ang = math.atan2(target[1] - home[1], target[0] - home[0])
    d = min(motions.WALK_SPEED * wt,
            math.hypot(target[0] - home[0], target[1] - home[1]))
    p = (home[0] + d * math.cos(ang), home[1] + d * math.sin(ang))
    arm.location = (p[0], p[1], 0)
    arm.rotation_euler = (0, 0, ang + math.pi)
    return p, wt


def walk_pose(arm, wt, blend=0.26):
    stage.pose(arm, motions.blend(motions.stop(wt), motions.walk(wt), ease(wt / blend)))


def lerp3(a, b, u):
    return tuple(p + (q - p) * u for p, q in zip(a, b))


def fly(cam, key, t):
    """(시각, 자리, 겨냥) 목록 위를 지나간다."""
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


def report(tag, key, lens_mm):
    """이 샷에서 사람이 화면 세로의 몇 할로 보이나를 **굽는 로그에 남긴다.**
    🔴 「크기를 지켰다」는 주장이 아니라 값이어야 한다(하한 `stage.FIG_MIN`)."""
    fr = [stage.fig_frac(k[1], k[2], lens_mm) for k in key]
    lo = min(fr)
    print('[%s] 인물 크기 %s (하한 %.2f)%s'
          % (tag, ' · '.join('%.2f' % f for f in fr), stage.FIG_MIN,
             '  🔴 미달' if lo < stage.FIG_MIN else ''))
    return lo
