"""ep15s-2 여섯 샷이 공유하는 것 — 마을·주민 자리·위험·계기·카메라.

🔴 **그림은 대본이 말하는 사건에서 나온다**(runbook §3-0). 2D 판 화면은 참고하지 않았다.
   이 편의 사건은 하나다: **위험이 오는데 저 사람이 무엇을 하고 있는가.**

🔴 늑대는 **안 만든다**(게임에서 사라진 종족). 위험은 붉은 덩이이고 그건 ep15s-1
   아웃트로가 이미 세운 3D 어휘다 — 그 편의 예고가 곧 이 편의 훅이라 그림이 이어진다.

🔴 **2026-08-13 규약 (ep15s-3 에서 세우고 여기로 가져왔다)**
   ① 자막 **한 줄마다** 「그 줄이 말할 때 화면에서 무슨 몸짓이 나는가」를 정하고, 그 표를
      각 샷 스크립트 맨 위 독스트링에 적는다. 시각은 손으로 안 적고 `line_at()` 으로 읽는다.
   ② **사건이 나면 남이 반응한다.** 일하다 → **멈칫** → 돌아본다 → 뛴다(`startle`).
      갈아타기만 하면 그건 연기가 아니라 루프 교체다.
   ③ 화면 크기는 **그 줄이 무엇을 보여 줘야 하는가**가 정한다 — 수를 견주는 줄은 가깝게,
      「누가 먼저 뛰나」는 넓게. 넓히면 바닥·마을 바깥도 같이 넓혀야 빈 땅이 안 생긴다.
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

EP = 'ep15s-2'
FPS = 30

# ── 여섯이 무엇을 하며 어디 있나 ────────────────────────
# 🔑 이 편은 군무가 아니다. 여섯이 **제각기 제 일을 하는 마을**이라야 「한 명만 안 도망간다」가
#    대비로 읽힌다. 자리는 소품 옆이다 — 동작은 **대상이 있어야 읽힌다**(PROPS.md).
# 🔴 배고픈 사람(5번)만 소품에서 떨어진 빈 땅에 둔다. 먹을 것이 없다는 것이 이 편의 조건이라
#    밭·덤불 옆에 두면 그림이 거짓말이 된다.
# 🔴 자리는 **재서 골랐다.** 나무꾼을 (4.0, 1.5) 에 세웠더니 집(3.6,2.2) 안쪽이라
#    뛰어 나갈 각이 하나도 안 남았다(최선 0.04 m). 서쪽 나무 옆으로 옮겼다.
HUNGRY = 5
CAST = [
    ('farm', (-2.7, -2.3), 'field'),          # 밭 앞
    ('warm', (0.55, 0.75), 'fire'),           # 모닥불 옆
    ('chop', (-3.65, 0.45), (-4.8, 0.6)),     # 서쪽 나무 옆
    ('draw', (2.6, -0.9), 'well'),            # 우물 옆
    ('gather', (1.3, -2.3), 'bush'),          # 덤불 옆
    ('eat', (-0.5, -2.6), None),              # 🔴 빈 땅 — 먹을 것이 없다
]
HOME_H = CAST[HUNGRY][1]

# 위험이 들어오는 길. 🔴 **집 쪽(+Y)에서 오면 안 된다** — 집 셋이 벽이라 덩이가 지붕에 가린다.
# 🔴 그리고 **카메라와 주인공 사이로 오면 안 된다.** 첫 판이 (5.6,-5.4) 에서 들어왔는데
#    그 길이 곧 카메라 앞이라, 덩이가 화면 절반을 덮고 정작 「그가 무엇을 하고 있는가」를
#    가렸다 — 이 편의 사건이 그 하나인데 그것을 가린 것이다.
# 🔴 넓은 카메라로 바뀌면서 **시작 자리를 더 밖으로** 뺐다. 앞 판은 5.2 라 이미 마을 안이었고,
#    「다가온다」가 아니라 「거기 있다」로 보였다.
DANGER_FROM = (7.8, -1.4)
DANGER_SIZE = 0.56
DANGER_BOB = 0.11

# ── 카메라 넷. 🔑 렌즈는 샷마다 달라도 되지만 **샷 안에서는 안 바꾼다**(설계 §4-1) ──
# 🔴 크기는 **그 줄이 무엇을 보여 줘야 하는가**가 정한다:
#    · 수를 견주는 줄(막대 둘의 높이) → CLOSE·MID. 막대가 안 읽히면 그 줄은 아무 말도 못 한다
#    · 「누가 먼저 뛰나」 → WIDE. 여섯이 서로 다른 쪽으로 흩어지는 것이 한 프레임에 있어야 한다
#    · 「내가 이 마을을 들여다본다」 → DEV
# 🔴 높이는 **하늘 넓이로** 잰다 — 눈높이에 두면 화면 위 3분의 1 이 빈 하늘이 된다.
EYE = ((4.55, -6.35, 1.72), (-0.35, -1.35, 0.62))     # 마을 눈높이 조망(좁다 — 근접용)
WIDE = ((4.90, -7.90, 3.35), (0.15, 0.05, 0.90))      # 흩어지는 여섯이 한 프레임에
# 🔴 개발자 시점을 너무 세우면 **막대가 눕는다**(수직 막대가 화면에서 짧아진다).
#    이 편의 계기는 「어느 쪽이 위인가」가 전부라 그러면 샷이 말을 못 한다. 25° 로 눌렀다.
DEV = ((2.60, -7.60, 4.60), (-0.35, -1.30, 0.60))     # 개발자 시점(내려다본다)
MID = ((1.35, -5.60, 1.95), (-0.45, -2.05, 0.88))     # 한 사람 + 막대 둘이 읽히는 크기
# 🔴 카메라 자리를 오른쪽에 뒀더니 **덤불 옆 사람이 렌즈 앞을 가로막았다**(머리가 화면
#    아래 모서리로 들어왔다). 같은 거리에서 왼쪽으로 돌아 그 사람을 화각 밖에 둔다.
CLOSE = ((0.35, -5.15, 1.25), (-0.50, -2.55, 0.78))   # 배고픈 사람 앞
WIDE_LENS, MID_LENS, CLOSE_LENS, DEV_LENS = 32, 40, 44, 40


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
    """🔑 길이는 대본이 정한다 — 렌더 스크립트에 초를 손으로 적지 마라."""
    return stage.shot_seconds(EP, shot_id)


def line_at(shot_id, i):
    """그 샷 i 번째 자막 줄이 **언제 시작하나**(초). 🔴 몸짓 시각을 손으로 적지 마라 —
    판정 기준이 「그 말을 할 때 그 몸짓이 나는가」라 대본이 바뀌면 같이 움직여야 한다."""
    return stage.shot_lines(EP, shot_id)[i][0]


def home_face(i):
    """CAST[i] 가 처음 향한 각(라디안, **적용되는 회전값**). `build` 와 같은 식이라야 안 튄다."""
    _job, (x, y), spot = CAST[i]
    if not spot:
        # 🔴 **카메라가 앞을 봐야 한다.** −115° 는 그가 북동쪽을 보게 만들어서, 남쪽에 선
        #    카메라에는 훅·본문2 내내 **뒤통수만** 나왔다. 먹는 손도 무너지는 몸도 안 보인다.
        #    120° 면 훅(−44° 방위)·본문2(−72° 방위) 둘 다에서 4분의 3 앞모습이다.
        return math.radians(120)
    s = village.SPOTS[spot] if isinstance(spot, str) else spot
    return math.atan2(y - s[1], x - s[0])


def build(snow=True, wide=True):
    """마을 + 주민 여섯 + 모닥불(+눈, +마을 바깥). 반환 (village, arms, fire, flakes, obst).

    🔴 `obst`(뛸 각을 재는 데 쓰는 소품 목록)는 **마을 바깥을 심기 전에** 뽑는다.
       바깥까지 넣으면 `flee_angles` 가 각을 못 찾고 단언에서 죽는다 — 그리고 넣을 이유도
       없다. 뛰는 사람은 4.6m 에서 렌더에서 빠지고, 바깥 나무는 7m 밖이다."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    v = village.build()
    arms = []
    for i, (job, (x, y), _spot) in enumerate(CAST):
        # 🔴 **하는 일이 보는 쪽을 정한다.** 밭 밖에서 밭을 갈면 그림이 거짓말이 된다.
        _mesh, arm = stage.rigged(loc=(x, y, 0), rot_z=math.degrees(home_face(i)))
        arms.append(arm)
    bpy.context.view_layer.update()
    obst = village.obstacles(exclude=(v['ground'],))
    if wide:
        village.outskirts()
    fire = village.flame(village.SPOTS['fire'])
    # 🔑 겨울이다 — 먹을 게 없다는 이 편의 조건이 눈으로 보여야 한다(PROPS.md 「날씨」).
    flakes = village.snow() if snow else None
    return v, arms, fire, flakes, obst


def face(arm, at, from_=None):
    """그 사람을 무엇 쪽으로 돌려세운다. 반환: **적용된 회전값**(돌아서는 보간이 이 값을 쓴다)."""
    p = from_ or tuple(arm.location)[:2]
    ang = math.atan2(at[1] - p[1], at[0] - p[0]) + math.pi
    arm.rotation_euler = (0, 0, ang)
    return ang


def turn(arm, a0, a1, k):
    """a0 에서 a1 로 k(0~1)만큼 돌아선다. 🔴 `arm.rotation_euler` 를 읽어 보간하지 마라 —
    앞 프레임 값이 누적돼 **결정성 게이트가 죽는다**(같은 프레임을 두 번 그리면 값이 달라진다)."""
    d = (a1 - a0 + math.pi) % (2 * math.pi) - math.pi
    arm.rotation_euler = (0, 0, a0 + d * max(0.0, min(1.0, k)))


def danger(size=DANGER_SIZE):
    """붉은 덩이 하나. 🔴 뜻층 빨강 하나를 쓴다 — 이 편의 유일한 유채색이다."""
    bpy.ops.mesh.primitive_cube_add(size=size, location=(DANGER_FROM[0], DANGER_FROM[1], 0.55))
    d = bpy.context.object
    d.data.materials.append(stage.meaning_mat('red', strength=1.0, albedo_scale=0.25))
    return d


def danger_at(d, u, target, t, size=1.0):
    """덩이를 들어온 자리에서 `target` 까지 u(0~1)만큼 옮긴다. **물러나지 않는다** —
    왔다 갔다 하면 그건 파도이지 다가오는 위험이 아니다(ep15s-1 아웃트로에서 배운 것)."""
    k = ease(u)
    x = DANGER_FROM[0] + (target[0] - DANGER_FROM[0]) * k
    y = DANGER_FROM[1] + (target[1] - DANGER_FROM[1]) * k
    d.location = (x, y, 0.55 + DANGER_BOB * math.sin(2 * math.pi * 1.35 * t))
    d.rotation_euler = (0, 0, 0.9 * t)
    d.scale = (size, size, size)
    return (x, y)


# ── 수치를 띄운다 ─────────────────────────────────────
# 🔴 이 편이 말하는 수는 넷뿐이다: 배고픔 100(불변) · 도망 92 → 105 · 고집쟁이 95.
#    그 넷을 **막대 둘의 높이**로 띄운다 — 글자가 없으니 길이가 곧 수다.
# 🔑 왼쪽이 배고픔, 오른쪽이 도망. **어느 쪽이 위인가**가 이 편의 사건 전부다.
HUNGER, FLEE_BEFORE, FLEE_AFTER, FLEE_STUBBORN = 100, 92, 105, 95
# 🔴 색은 **행동 범주**가 정한다(`palette.NEED_OF`). 배고픔은 몸(연노랑 65°),
#    도망은 위협(자홍 310°). 막대 둘이 색으로 갈리므로 어느 쪽이 무엇인지 자막 없이 안다.
BAR_COLOR = ('need_body', 'need_threat')
EMPH = 1.9              # 대본이 그 수치를 부르는 동안 막대가 커지는 배율
EMPH_IN = 0.22          # 커지고 작아지는 데 쓰는 시간(초)


def emphasis(lines, t, focus):
    """지금 어느 막대를 키울까. `focus[줄번호]` 가 그 줄이 부르는 막대 번호(없으면 없음).

    🔴 **강조는 대본이 정한다.** 시각은 `stage.shot_lines`(=`timed.json`)에서 온다."""
    out = {}
    for i, (t0, dur) in enumerate(lines):
        which = focus.get(i)
        if which is None or not (t0 - EMPH_IN <= t <= t0 + dur + EMPH_IN):
            continue
        u = min((t - (t0 - EMPH_IN)) / EMPH_IN, ((t0 + dur + EMPH_IN) - t) / EMPH_IN, 1.0)
        k = ease(max(0.0, u))
        for w in (which if isinstance(which, (list, tuple)) else [which]):
            out[w] = max(out.get(w, 1.0), 1.0 + (EMPH - 1.0) * k)
    return out


def cam_right(cam_loc, at):
    """카메라에서 봤을 때 **화면 가로 방향**(단위벡터, xy). 계기를 나란히 놓을 때 쓴다."""
    dx, dy = at[0] - cam_loc[0], at[1] - cam_loc[1]
    n = math.hypot(dx, dy) or 1.0
    return (-dy / n, dx / n)


def gauges(n=2, ceiling=False):
    """막대 n 개. 🔴 **주민에게 안 묶는다** — 사람이 돌면 막대도 같이 돌아 화면에서 눕는다.

    `ceiling` 이면 **도망 막대에 문턱 표시**를 단다 — 그 선이 곧 「배고픔보다 앞설 수 없다」는
    안전장치다. 채움이 그 선을 못 넘는 것이 본문1 의 사건이고, 넘는 것이 본문3 의 사건이다."""
    g = [instrument.gauge((0.0, 0.0, 1.35), color=BAR_COLOR[i % len(BAR_COLOR)])
         for i in range(n)]
    if ceiling and n > 1:
        # 🔴 선은 **계기 시안**이다. 막대와 같은 자홍으로 두면 채움이 지나는 순간 먹혀서
        #    사라진다 — 그 순간이 바로 이 편의 사건인데.
        instrument.gauge_mark(g[1], instrument.gauge_k(HUNGER), color='instrument')
    return g


def gauge_place(g, arm, right, z=1.35, emph=None, side=0.0):
    """막대 둘을 그 사람 옆(또는 위)에 나란히 놓는다(화면 가로 방향으로).

    🔴 **강조는 짝을 통째로 키운다.** 하나만 키우면 그 막대의 홈까지 커져서, 값이 더 낮은
       막대가 화면에서 더 **길어진다** — 이 편은 「어느 쪽이 위인가」가 사건 전부라
       그 순간 그림이 거짓말을 한다(본문3 프레임 220 에서 잡았다: 도망 95 가 배고픔 100 보다
       길어 보였다).
    🔑 `side` 는 짝 전체를 화면 가로로 밀어 사람 **옆에** 세우는 값이다. 가까운 샷에서
       머리 위에 두면 막대 꼭대기가 화면 밖으로 나간다."""
    x, y = arm.location.x, arm.location.y
    m = max((emph or {}).values(), default=1.0)
    for i, gg in enumerate(g):
        instrument.gauge_emph(gg, m)
        off = side + (i - (len(g) - 1) / 2) * instrument.GAUGE_GAP * gg['m']
        instrument.gauge_at(gg, x + right[0] * off, y + right[1] * off, z)


def gauge_show(g, on, ceiling=None):
    """막대를 켜고 끈다. `ceiling` 을 따로 주면 문턱 표시만 갈라서 끈다(원칙이 깨지는 순간)."""
    for gg in g:
        gg['track'].hide_render = not on
        gg['fill'].hide_render = not on
        if gg.get('mark') is not None:
            gg['mark'].hide_render = not (on if ceiling is None else ceiling)


def behind(subject, cam_loc, dist=0.95):
    """카메라에서 봤을 때 `subject` **뒤쪽** 자리. 🔴 위험이 앞에 서면 주인공을 가린다 —
    이 편에서 봐야 하는 것은 위험이 아니라 **그가 무엇을 하고 있는가**다."""
    dx, dy = subject[0] - cam_loc[0], subject[1] - cam_loc[1]
    n = math.hypot(dx, dy) or 1.0
    return (subject[0] + dx / n * dist, subject[1] + dy / n * dist)


def work(arm, job, t):
    """제 일을 한다. 🔑 여섯이 **같은 시각에 다른 일**을 하는 것이 이 마을의 기본값이다."""
    stage.pose(arm, motions.MOTIONS[job](t))


# 🔴 **뛰는 시간에 상한을 둔다.** 안 두면 4초 × 1.57 m/s = 6.5 m 라 10 m 짜리 마을을
#    가로지르고, 그러면 어떤 각을 골라도 무언가를 뚫는다(각이 없다고 검사가 막았다).
FLEE_SPAN = 3.4          # 🔴 1.7 은 **프레임 안에서 멈춰 서게** 만들었다(사용자가 잡았다)
FLEE_GONE = 4.6          # 이만큼 멀어지면 마을 밖이다 — 렌더에서 뺀다
# 🔴 **알아차리는 데 걸리는 시간.** 0 이면 일하다 곧장 뛰어서 「루프 교체」로 보인다.
NOTICE_TURN = 0.26       # 몸이 위험 쪽으로 도는 데 쓰는 시간
NOTICE_HOLD = 0.30       # 굳어서 보는 시간 — **이 멈칫이 이 편의 반응 동작이다**
NOTICE_LEAD = NOTICE_TURN + NOTICE_HOLD


def flee(arm, home, ang, t, t0, span=FLEE_SPAN):
    """t0 부터 ang 쪽으로 **뛴다.** 나아가는 거리는 `motions.RUN_SPEED × 뛴 시간`이다."""
    rt = max(0.0, t - t0)
    if span is not None:
        rt = min(rt, span)
    d = motions.RUN_SPEED * rt
    arm.location = (home[0] + d * math.cos(ang), home[1] + d * math.sin(ang), 0)
    arm.rotation_euler = (0, 0, ang + math.pi)        # 규약: 정면은 (-cos θ, -sin θ)
    # 🔴 **뛰다 만 사람을 화면에 남기지 마라.** 앞 판은 1.7초에 멈춰서 밭 한가운데 굳어 있었다.
    arm.hide_render = d > FLEE_GONE
    for ob in arm.children:
        ob.hide_render = arm.hide_render
    return rt


def startle(arm, i, home, job, t, run_at, ang, danger_xy, span=FLEE_SPAN):
    """**일한다 → 멈칫하고 돌아본다 → 뛴다.** 이 편의 반응 동작이다.

    🔴 앞 판은 `work` 에서 `run` 으로 곧장 갈아탔다. 그러면 화면이 말하는 것은 「알아차렸다」가
       아니라 「동작이 바뀌었다」다 — 2026-08-13 판정(「애니메이션이 상황에 맞게 안 바뀐다」)의
       한가운데가 여기였다.
    🔑 세 마디다: ① 하던 손을 멈추고 몸이 위험 쪽으로 돈다 ② 굳어서 본다(0.30초)
       ③ 반대로 돌아 뛴다. ②가 없으면 ①과 ③이 한 동작으로 뭉쳐서 안 보인다.
    """
    notice = run_at - NOTICE_LEAD
    a0 = home_face(i)
    if t < notice:
        work(arm, job, t)
        arm.location = home
        arm.rotation_euler = (0, 0, a0)
        return
    if t < run_at:
        # ①② 손이 멈추고 몸이 돌아 **본다**. `look_up` 이 고개를 드는 한 번뿐인 동작이다
        k = ease((t - notice) / NOTICE_TURN)
        arm.location = home
        a1 = math.atan2(danger_xy[1] - home[1], danger_xy[0] - home[0]) + math.pi
        turn(arm, a0, a1, k)
        stage.pose(arm, motions.blend(motions.MOTIONS[job](t),
                                      motions.look_up(t - notice), k))
        return
    # ③ 돌아서서 뛴다
    rt = flee(arm, home, ang, t, run_at, span)
    k = ease(rt / 0.24)
    # 🔴 `flee` 가 넣은 각으로 **한 프레임에 뒤집히면** 사람이 순간이동한다.
    #    본 쪽에서 뛰는 쪽으로 0.22초에 걸쳐 돈다.
    a1 = math.atan2(danger_xy[1] - home[1], danger_xy[0] - home[0]) + math.pi
    turn(arm, a1, ang + math.pi, ease(rt / 0.22))
    stage.pose(arm, motions.blend(motions.look_up(NOTICE_LEAD + rt), motions.run(rt), k))


def watch(arm, i, home, job, t, at, target, span=0.45):
    """일하다 그 시각부터 **하던 일을 멈추고 그쪽을 본다**(뛰지는 않는다).
    🔑 사건은 **남이 반응해야** 사건이다. 아무도 안 보면 그건 배경이다."""
    arm.location = home
    a0 = home_face(i)
    if t < at:
        arm.rotation_euler = (0, 0, a0)
        work(arm, job, t)
        return
    k = ease((t - at) / span)
    a1 = math.atan2(target[1] - home[1], target[0] - home[0]) + math.pi
    turn(arm, a0, a1, k)
    stage.pose(arm, motions.blend(motions.MOTIONS[job](t), motions.stop(t - at), k))


# 뛰어 나가는 쪽 — 🔴 **각을 손으로 정하지 마라.** 세 번 해 봤고 세 번 다 소품을 뚫었다
#    (궤적 검사가 0.39 m 로 잡았다). 마을 북쪽은 집 셋이 벽이고 우물·나무·덤불이 남쪽에
#    흩어져 있어서 「열린 쪽」이 눈에 안 보인다. **재서 고른다.**
FLEE_CANDS = [math.radians(a) for a in range(-175, 6, 5)]     # 아래쪽 반원(카메라 쪽·좌우)


def flee_angles(homes, obstacles, run_time=FLEE_SPAN, body_r=0.22, gap=0.62):
    """여섯이 서로도 소품도 안 뚫고 뛰어 나가는 각을 **하나씩 재서** 고른다(그리디).

    🔑 점수 셋을 본다: 소품 여유 · 서로 벌어지는 정도 · 아래쪽(카메라 쪽)으로 나가는가."""
    picked = []
    for h in homes:
        best, best_s = None, -9e9
        for a in FLEE_CANDS:
            path = [(h[0] + motions.RUN_SPEED * run_time * k / 24 * math.cos(a),
                     h[1] + motions.RUN_SPEED * run_time * k / 24 * math.sin(a))
                    for k in range(25)]
            # 🔴 **선 자리는 안 잰다.** 나무를 패는 사람은 나무 옆에 서 있는 것이 정상이고,
            #    거기서 재면 어떤 각도 통과를 못 한다. 재는 것은 「나아간 뒤에 무엇을 뚫는가」다.
            clear = min(math.hypot(px - ox, py - oy) - r - body_r
                        for px, py in path[4:] for ox, oy, r, _nm in obstacles)
            if clear <= 0.05:
                continue
            sep = min((min(math.hypot(px - qx, py - qy) for (px, py), (qx, qy) in zip(path, q))
                       for q in picked), default=9.0)
            if sep < gap:
                continue
            s = clear + 0.6 * min(sep, 2.0) + 0.35 * math.cos(a + math.pi / 2)
            if s > best_s:
                best, best_s = a, s
        assert best is not None, '뛰어 나갈 각이 없다 — 자리를 옮겨라 (%.2f, %.2f)' % h
        picked.append([(h[0] + motions.RUN_SPEED * run_time * k / 24 * math.cos(best),
                        h[1] + motions.RUN_SPEED * run_time * k / 24 * math.sin(best))
                       for k in range(25)])
        yield best
