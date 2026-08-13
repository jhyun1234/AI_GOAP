"""ep15s-4 일곱 샷이 공유하는 것 — 마을·주민 자리·모닥불 밭·계기·카메라.

🔴 **2D 판은 참고하지 않았다**(2026-08-13 지시). 대본도 그림도 `scene.json` 의
   `source.note` 가 적어 둔 사슬 넷에서 다시 뽑았다:
   ① 부탁 수락 → ② 자기 모닥불 건축 → ③ **소유 배정을 건너뜀**(「내 불 있음」이 안 켜진다)
   → ④ 목표 재발동, 그게 부탁보다 급해서 집은 시작도 못 한다.

🔑 **3D 라서 새로 되는 것 셋이 이 편의 그림을 정했다.**
   ① 모닥불이 **실제 소품**이다(`village._fire_pit` + `village.flame`). 하나씩 늘면 정말 덮인다
   ② 되풀이가 도형 반복이 아니라 **몸짓**이다 — 다 짓고 **돌아서서 또 짓는다**
   ③ **지은 개수와 「내 불 있음」을 한 프레임에 나란히** 놓을 수 있다. 불은 느는데 막대는
      바닥에서 안 움직인다 — 무한 건축의 원인이 우선순위가 아니라 **셈**이라는 것을
      그림 하나가 말한다. 2D 판에는 이 자리가 아예 없었다.

🔴 규약은 ep15s-2·3 과 같다(runbook · notes/기기-이관 아님): 자막 한 줄마다 「무슨 몸짓 ·
   어느 크기」를 샷 독스트링에 적고, 시각은 `line_at()` 으로 대본에서 읽는다.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import instrument       # noqa: E402
import motions          # noqa: E402
import stage            # noqa: E402
import village          # noqa: E402

EP = 'ep15s-4'
FPS = 30

# ── 여섯이 무엇을 하며 어디 있나 ────────────────────────
# 🔑 목수와 부탁한 주민 둘이 이 편의 무대이고, 나머지 넷은 **제 일을 하는 마을**이다.
#    넷이 없으면 「목수만 이상하다」가 안 보인다 — 견줄 것이 있어야 이상한 게 이상해 보인다.
CARP, ASKER = 0, 1
CAST = [
    ('hammer', (-4.20, -3.90), None),      # 목수 — 불밭 아래쪽에서 시작한다
    ('stop', (-0.45, -1.45), None),        # 부탁한 주민 — **빈 집터 바로 옆**(1.4m)
    ('farm', (-2.7, -2.3), 'field'),
    ('draw', (2.6, -0.9), 'well'),
    ('gather', (1.3, -2.3), 'bush'),
    ('chop', (-3.65, 0.45), (-4.8, 0.6)),
]

# 부탁받은 집이 설 자리. 🔴 ep15s-3 이 **카메라에서 재서** 고른 자리를 그대로 쓴다 —
#    9m 앞이고 시선축 15° 안이라 다 자란 집이 화면 안에 온전히 들어온다.
PLOT = (-1.60, -0.15)
PLOT_ROT = -22

# 모닥불 밭 — 🔴 **자리를 손으로 적지 않는다.** 격자를 깔고 소품 여유를 재서 고른다.
#    마을이 빽빽해서 눈으로 고른 자리는 늘 무언가와 겹쳤다(ep15s-3 에서 네 번 겪었다).
FIELD_C = (-5.80, -1.80)
FIELD_PITCH = 1.15
FIRE_CLEAR = 0.95          # 불 반지름 0.46 + 여유. 이보다 가까우면 소품에 파묻힌다

EYE = ((3.60, -8.20, 3.30), (-2.20, -0.60, 0.90))     # 마을 + 불밭이 한 프레임에
MID = ((-2.10, -6.20, 2.10), (-5.20, -1.80, 0.85))    # 목수와 불 두셋
CLOSE = ((-3.55, -4.75, 1.35), (-5.30, -2.05, 0.80))  # 목수 + 막대 하나
PLOTCAM = ((1.60, -5.60, 2.05), (-1.60, -1.60, 0.90))  # 빈 집터와 기다리는 주민
WIDE_LENS, MID_LENS, CLOSE_LENS, PLOT_LENS = 32, 38, 44, 38

# 계기 — 「내 불 있음」 하나뿐이다. 🔴 이 편은 **수치가 0개**라(브리프 §6) 눈금이 없다.
#    세는 것은 있다/없다 하나이고, 그래서 막대가 **바닥 아니면 끝**이다.
#    색은 행동 범주가 정한다(`palette.NEED_OF['build'] == 'need_work'`).
OWN_COLOR = 'need_work'


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
    """CAST[i] 가 처음 향한 각(적용되는 회전값)."""
    _job, (x, y), spot = CAST[i]
    if not spot:
        return math.radians(-118)          # 카메라 쪽으로 살짝 열어 둔다
    s = village.SPOTS[spot] if isinstance(spot, str) else spot
    return math.atan2(y - s[1], x - s[0])


def _fire_spots(n, obst):
    """불 자리 n 개를 **재서** 고른다. 격자를 깔고 소품 여유가 충분한 칸만 남긴 뒤
    가운데에서 가까운 순으로 준다 — 그래야 늘어나는 모양이 안에서 밖으로 퍼진다."""
    out = []
    for gy in range(-2, 3):
        for gx in range(-2, 3):
            x = FIELD_C[0] + gx * FIELD_PITCH
            y = FIELD_C[1] + gy * FIELD_PITCH
            clear = min((math.hypot(x - ox, y - oy) - r for ox, oy, r, _nm in obst),
                        default=9.0)
            if clear < FIRE_CLEAR:
                continue
            out.append((math.hypot(x - FIELD_C[0], y - FIELD_C[1]), x, y))
    out.sort()
    assert len(out) >= n, '불 자리가 %d 개밖에 안 나온다 — 밭을 옮겨라' % len(out)
    return [(x, y) for _d, x, y in out[:n]]


def build(n_fires=9):
    """마을 + 주민 여섯 + 모닥불 밭. 반환 (village, arms, fires, spots).

    `fires[i]` 는 `(재바닥, 불꽃들)` 이고 처음엔 **전부 꺼져 있다** — 샷이 하나씩 켠다.
    🔴 불 자리는 **마을 바깥을 심기 전에** 잰다(바깥까지 넣으면 자리가 안 남는다)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    v = village.build()
    arms = []
    for i, (_job, (x, y), _spot) in enumerate(CAST):
        _mesh, arm = stage.rigged(loc=(x, y, 0), rot_z=math.degrees(home_face(i)))
        arms.append(arm)
    bpy.context.view_layer.update()
    obst = village.obstacles(exclude=(v['ground'],))
    spots = _fire_spots(n_fires, obst)
    village.outskirts()
    m = village.mats()
    fires = []
    for x, y in spots:
        # 🔴 불만 세우면 **허공에 뜬 불꽃**이 된다. 모닥불은 돌·장작·불꽃 셋이다(PROPS.md).
        pit = village._fire_pit(Vector((x, y, 0)), m['stone'], m['char'])
        parts = village.flame(Vector((x, y, 0)))
        fires.append((pit, parts))
    global HOME_FIRE
    HOME_FIRE = village.flame(village.SPOTS['fire'])   # 마을 본래 모닥불 — 늘 켜 있다
    return v, arms, fires, spots


HOME_FIRE = None


def flicker_home(t):
    """마을 본래 모닥불. 🔴 안 부르면 불이 굳어 있고, 굳은 불은 불로 안 보인다."""
    if HOME_FIRE:
        village.flicker(HOME_FIRE, t)


def fire_show(fires, i, on, t=0.0, grow=1.0):
    """불 하나를 켜고 끈다. `grow` 로 자라 오르게 한다(0 이면 안 보인다)."""
    pit, parts = fires[i]
    village.pit_show(pit, on)          # 🔴 재만 끄면 돌 테두리가 남는다
    if not on:
        for o in parts:
            o.hide_render = True
        return
    village.flicker(parts, t, k=max(grow, 0.03))


def fire_all(fires, lit, t, growing=None, grow_k=1.0):
    """앞에서 `lit` 개까지 켠다. `growing` 은 지금 자라는 중인 하나."""
    for i in range(len(fires)):
        if i == growing:
            fire_show(fires, i, grow_k > 0.02, t, grow_k)
        else:
            fire_show(fires, i, i < lit, t)


def face(arm, at, from_=None):
    p = from_ or tuple(arm.location)[:2]
    ang = math.atan2(at[1] - p[1], at[0] - p[0]) + math.pi
    arm.rotation_euler = (0, 0, ang)
    return ang


def turn(arm, a0, a1, k):
    """🔴 `arm.rotation_euler` 를 읽어 보간하지 마라 — 앞 프레임 값이 누적돼 결정성이 죽는다."""
    d = (a1 - a0 + math.pi) % (2 * math.pi) - math.pi
    arm.rotation_euler = (0, 0, a0 + d * max(0.0, min(1.0, k)))


def cam_right(cam_loc, at):
    dx, dy = at[0] - cam_loc[0], at[1] - cam_loc[1]
    n = math.hypot(dx, dy) or 1.0
    return (-dy / n, dx / n)


def own_gauge():
    """「내 불 있음」 막대 하나. 🔴 문턱 표시를 안 단다 — 넘고 말고 할 선이 없다.
    이 값은 있다/없다 하나이고, **안 켜지는 것**이 이 편의 사건이다."""
    # 🔴 연두는 밝은 색이라 홈 기본값(0.06)이면 **빈 막대가 차 있는 것으로 읽힌다.**
    #    이 편은 「안 켜진다」가 사건이라 그 오독이 곧 대본을 뒤집는다.
    # 🔴 그리고 **꼭대기에 선을 긋는다.** 빈 막대만 있으면 그냥 초록 막대기로 보인다 —
    #    「여기까지 차야 하는데 안 찬다」가 되려면 차야 할 자리가 보여야 한다.
    g = instrument.gauge((0.0, 0.0, 0.95), color=OWN_COLOR, dim=0.025)
    instrument.gauge_mark(g, 1.0, color='instrument')
    return g


EMPH, EMPH_IN = 1.9, 0.22


def emphasis_pair(lines, t, which=(0, 1)):
    """대본이 그 값을 부르는 동안 막대를 키운다. 반환: 배율 하나.

    🔴 ep15s-2 에서 잡은 함정 — 막대가 둘일 때 **하나만** 키우면 홈까지 커져서 값이 낮은
       쪽이 더 길어 보인다. 이 편은 막대가 **하나뿐**이라 그 문제가 없다."""
    m = 1.0
    for i in which:
        if i >= len(lines):
            continue
        t0, dur = lines[i]
        if not (t0 - EMPH_IN <= t <= t0 + dur + EMPH_IN):
            continue
        u = min((t - (t0 - EMPH_IN)) / EMPH_IN, ((t0 + dur + EMPH_IN) - t) / EMPH_IN, 1.0)
        m = max(m, 1.0 + (EMPH - 1.0) * ease(max(0.0, u)))
    return m


def gauge_at(g, arm, right, z=0.62, side=0.58):
    """막대를 그 사람 옆에 세운다. 🔴 가까운 샷에서 머리 위에 두면 꼭대기가 잘린다."""
    x, y = arm.location.x, arm.location.y
    instrument.gauge_at(g, x + right[0] * side, y + right[1] * side, z)


def gauge_show(g, on):
    for key in ('track', 'fill', 'mark'):
        if g.get(key) is not None:
            g[key].hide_render = not on


def plot_ring():
    """집이 설 자리를 땅에 그린다. 🔴 **빈 땅은 아무 말도 안 한다** — 「여기가 그 자리인데
    비어 있다」가 되려면 자리가 보여야 한다(본문3 프레임에서 잡았다).
    🔑 집이 다 서면 끈다 — 그때부터는 집 자체가 그 자리다."""
    return instrument.ring(1.15)


def walk_to(arm, home, target, t, t0, span, run=False):
    """나아가는 거리는 `WALK_SPEED × 걸은 시간`이다 — 눈대중이면 발이 미끄러진다."""
    wt = min(max(t - t0, 0.0), span)
    ang = math.atan2(target[1] - home[1], target[0] - home[0])
    speed = motions.RUN_SPEED if run else motions.WALK_SPEED
    d = min(speed * wt, math.hypot(target[0] - home[0], target[1] - home[1]))
    p = (home[0] + d * math.cos(ang), home[1] + d * math.sin(ang))
    arm.location = (p[0], p[1], 0)
    arm.rotation_euler = (0, 0, ang + math.pi)
    return p, wt


def walk_pose(arm, wt, blend=0.26, run=False):
    k = ease(wt / blend)
    go = motions.run(wt) if run else motions.walk(wt)
    stage.pose(arm, motions.blend(motions.stop(wt), go, k))


def others(arms, t, home, skip=(CARP, ASKER), watch=None, watch_at=0.0):
    """나머지 넷은 제 일을 한다. `watch` 가 오면 그 시각부터 일을 멈추고 그쪽을 본다."""
    for i, arm in enumerate(arms):
        if i in skip:
            continue
        arm.location = home[i]
        work = motions.MOTIONS[CAST[i][0]](t)
        a0 = home_face(i)
        if watch is None or t < watch_at:
            arm.rotation_euler = (0, 0, a0)
            stage.pose(arm, work)
            continue
        d = watch_at + 0.18 * i
        k = ease((t - d) / 0.45)
        if k <= 0.0:
            arm.rotation_euler = (0, 0, a0)
            stage.pose(arm, work)
            continue
        a1 = math.atan2(watch[1] - home[i][1], watch[0] - home[i][0]) + math.pi
        turn(arm, a0, a1, k)
        stage.pose(arm, motions.blend(work, motions.stop(t - d), k))


# 목수가 불 하나를 짓는 마디 — 🔴 **되풀이의 박자**다.
# 걸어간다 → 친다 → 선다. 이 마디가 반복되는 것이 이 편의 그림 전부다.
# 🔴 걷는 시간을 고정값으로 적지 마라 — 자리 간격이 제각각이라 도착 전에 망치질이 시작되거나
#    다 걷고도 남는다(첫 판이 0.85 고정이었고 발이 미끄러졌다). **거리에서 나온다.**
STEP_HAMMER = 0.62
STAND_OFF = 0.62          # 불 **옆에** 선다 — 위에 서면 사람이 불에 파묻힌다


def plan_builds(spots, start, order, t0):
    """마디 목록을 미리 짠다. [(시작초, 출발, 설 자리, 불자리, 번호, 걷는 시간)]"""
    out, t, frm = [], t0, start
    for i in order:
        to = spots[i]
        ang = math.atan2(to[1] - frm[1], to[0] - frm[0])
        stand = (to[0] - STAND_OFF * math.cos(ang), to[1] - STAND_OFF * math.sin(ang))
        span = math.hypot(stand[0] - frm[0], stand[1] - frm[1]) / motions.WALK_SPEED
        out.append((t, frm, stand, to, i, span))
        t += span + STEP_HAMMER
        frm = stand
    return out


def run_builds(arm, plan, t):
    """계획을 t 에 태운다. 반환 (자리, 다 지은 개수, 자라는 번호, 자람 0~1)."""
    if not plan:
        return (0.0, 0.0), 0, None, 0.0
    if t < plan[0][0]:
        p = plan[0][1]
        arm.location = (p[0], p[1], 0)
        stage.pose(arm, motions.stop(t))
        return p, 0, None, 0.0
    done = 0
    for n, (t0, frm, stand, to, i, span) in enumerate(plan):
        if t < t0:
            break
        if t < t0 + span:
            p, _wt = walk_to(arm, frm, stand, t, t0, span)
            walk_pose(arm, t - t0)
            return p, n, None, 0.0
        if t < t0 + span + STEP_HAMMER:
            ht = t - t0 - span
            arm.location = (stand[0], stand[1], 0)
            face(arm, to, stand)
            stage.pose(arm, motions.blend(motions.walk(span), motions.hammer(ht),
                                          ease(ht / 0.20)))
            return stand, n, i, ease(ht / STEP_HAMMER)
        done = n + 1
    last, lastto = plan[-1][2], plan[-1][3]
    arm.location = (last[0], last[1], 0)
    face(arm, lastto, last)
    stage.pose(arm, motions.stop(t))
    return last, done, None, 1.0
