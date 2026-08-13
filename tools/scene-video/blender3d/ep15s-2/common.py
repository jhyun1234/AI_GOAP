"""ep15s-2 여섯 샷이 공유하는 것 — 마을·주민 자리·위험·카메라.

🔴 **그림은 대본이 말하는 사건에서 나온다**(runbook §3-0). 2D 판 화면은 참고하지 않았다.
   이 편의 사건은 하나다: **위험이 오는데 저 사람이 무엇을 하고 있는가.**
   그래서 여기 있는 것은 「값을 그리는 도구」가 아니라 **사람·위험·카메라** 셋뿐이다.

🔴 늑대는 **안 만든다**(게임에서 사라진 종족). 위험은 붉은 덩이이고 그건 ep15s-1
   아웃트로가 이미 세운 3D 어휘다 — 그 편의 예고가 곧 이 편의 훅이라 그림이 이어진다.
"""
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
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
# 🔴 자리는 **재서 골랐다.** 처음에 나무꾼을 (4.0, 1.5) 에 세웠더니 집(3.6,2.2 · 여유 0.98)
#    안쪽이라 **뛰어 나갈 각이 하나도 안 남았다**(최선이 0.04 m). 집과 나무 사이가 아니라
#    서쪽 나무(-4.8,0.6) 옆으로 옮겼다 — 뒤가 트여 있어 어느 쪽으로든 나갈 수 있다.
HUNGRY = 5
CAST = [
    ('farm', (-2.7, -2.3), 'field'),          # 밭 앞
    ('warm', (0.55, 0.75), 'fire'),           # 모닥불 옆
    ('chop', (-3.65, 0.45), (-4.8, 0.6)),     # 서쪽 나무 옆
    ('draw', (2.6, -0.9), 'well'),            # 우물 옆
    ('gather', (1.3, -2.3), 'bush'),          # 덤불 옆
    ('eat', (-0.5, -2.6), None),              # 🔴 빈 땅 — 먹을 것이 없다
]

# 위험이 들어오는 길. 🔴 **집 쪽(+Y)에서 오면 안 된다** — 집 셋이 벽이라 덩이가 지붕에 가린다.
# 🔴 그리고 **카메라와 주인공 사이로 오면 안 된다.** 첫 판이 (5.6,-5.4) 에서 들어왔는데
#    그 길이 곧 카메라 앞이라, 덩이가 화면 절반을 덮고 정작 「그가 무엇을 하고 있는가」를
#    가렸다 — 이 편의 사건이 그 하나인데 그것을 가린 것이다(프레임 시트로 잡았다).
#    그래서 **옆에서 들어와 그의 뒤쪽에 선다.** 크기도 0.62 → 0.5 로 줄였다.
DANGER_FROM = (5.2, -1.1)
DANGER_SIZE = 0.50
DANGER_BOB = 0.11

# 카메라 둘. 🔑 렌즈는 샷마다 달라도 되지만 **샷 안에서는 안 바꾼다**(설계 §4-1).
# 🔴 높이는 **재서 정했다.** 개발자 시점을 8.4 m 로 뒀더니 주민이 화면에서 점이 됐다 —
#    「무엇을 하려는지」가 안 보이면 그 샷은 아무 말도 못 한다. 5.6 m 로 내리고 겨냥을
#    주인공 쪽으로 당겼다(설계 §6-3 의 개발자 시점은 70mm 값이고 우리는 45mm 다).
EYE = ((4.55, -6.35, 1.72), (-0.35, -1.35, 0.62))     # 마을 눈높이 조망
DEV = ((2.60, -6.10, 5.60), (-0.50, -1.70, 0.35))     # 개발자 시점(내려다본다)
# 🔴 카메라 자리를 오른쪽에 뒀더니 **덤불 옆 사람이 렌즈 앞을 가로막았다**(머리가 화면
#    아래 모서리로 들어왔다). 같은 거리에서 왼쪽으로 돌아 그 사람을 화각 밖에 둔다.
CLOSE = ((0.30, -5.30, 0.95), (-0.50, -2.60, 0.58))   # 배고픈 사람 2.8 m 앞 — 화면의 46%


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


def build(snow=True):
    """마을 + 주민 여섯 + 모닥불(+눈). 반환 (village, arms, fire, flakes)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    v = village.build()
    arms = []
    for job, (x, y), spot in CAST:
        # 🔴 **하는 일이 보는 쪽을 정한다.** 밭 밖에서 밭을 갈면 그림이 거짓말이 된다.
        if spot:
            s = village.SPOTS[spot] if isinstance(spot, str) else spot
            face = math.atan2(y - s[1], x - s[0])     # 규약: 정면은 (-cos θ, -sin θ)
        else:
            face = math.radians(-115)                 # 카메라 쪽으로 살짝 열어 둔다
        _mesh, arm = stage.rigged(loc=(x, y, 0), rot_z=math.degrees(face))
        arms.append(arm)
    fire = village.flame(village.SPOTS['fire'])
    # 🔑 겨울이다 — 먹을 게 없다는 이 편의 조건이 눈으로 보여야 한다(PROPS.md 「날씨」).
    flakes = village.snow() if snow else None
    return v, arms, fire, flakes


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


# ── 수치를 띄운다 (2026-08-13 사용자 지시: 「수치가 안 보여서 밋밋하다」) ──
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
    반환 {막대번호: 배율}.

    🔴 **강조는 대본이 정한다**(2026-08-13 지시). 줄이 말해지는 동안 커졌다가 끝나면
       돌아온다 — 시청자가 「지금 강조하는 게 도망이고, 다음은 밥이구나」를 알게 하는 장치다.
    🔑 시각은 `stage.shot_lines`(= `timed.json`)에서 온다. 손으로 적으면 대본이 바뀌는 날 어긋난다."""
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


def gauges(n=2):
    """막대 n 개. 🔴 **주민에게 안 묶는다** — 사람이 돌면 막대도 같이 돌아 화면에서 눕는다.
    매 프레임 `gauge_place` 로 사람 위에 놓고, 늘 화면을 보게 둔다."""
    import instrument
    return [instrument.gauge((0.0, 0.0, 1.35), color=BAR_COLOR[i % len(BAR_COLOR)])
            for i in range(n)]


def gauge_place(g, arm, right, z=1.35, emph=None):
    """막대 둘을 그 사람 머리 위에 나란히 놓는다(화면 가로 방향으로).
    `emph` 를 주면 그 막대만 커진다 — **대본이 그 수치를 부르는 동안**이다."""
    import instrument
    x, y = arm.location.x, arm.location.y
    for i, gg in enumerate(g):
        instrument.gauge_emph(gg, (emph or {}).get(i, 1.0))
        off = (i - (len(g) - 1) / 2) * instrument.GAUGE_GAP * gg['m']
        instrument.gauge_at(gg, x + right[0] * off, y + right[1] * off, z)


def gauge_show(g, on):
    for gg in g:
        gg['track'].hide_render = not on
        gg['fill'].hide_render = not on


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
#    2.7 m 면 화면 밖으로 나가기에 충분하다 — 나간 뒤에는 그 자리에 있어도 안 보인다.
FLEE_SPAN = 3.4          # 🔴 1.7 은 **프레임 안에서 멈춰 서게** 만들었다(사용자가 잡았다)
FLEE_GONE = 4.6          # 이만큼 멀어지면 마을 밖이다 — 렌더에서 뺀다


def flee(arm, home, ang, t, t0, span=FLEE_SPAN):
    """t0 부터 ang 쪽으로 **뛴다.** 나아가는 거리는 `motions.RUN_SPEED × 뛴 시간`이다 —
    눈대중으로 밀면 발이 미끄러진다(2026-08-13 발 고정 이후로는 더 그렇다)."""
    rt = max(0.0, t - t0)
    if span is not None:
        rt = min(rt, span)
    d = motions.RUN_SPEED * rt
    arm.location = (home[0] + d * math.cos(ang), home[1] + d * math.sin(ang), 0)
    arm.rotation_euler = (0, 0, ang + math.pi)        # 규약: 정면은 (-cos θ, -sin θ)
    # 🔴 **뛰다 만 사람을 화면에 남기지 마라.** 앞 판은 1.7초에 멈춰서 밭 한가운데
    #    굳은 채 서 있었다 — 사용자가 잡은 그 버그다. 멀어지면 렌더에서 뺀다.
    arm.hide_render = d > FLEE_GONE
    for ob in arm.children:
        ob.hide_render = arm.hide_render
    return rt


def flee_pose(arm, t, t0, prev_job, blend=0.24):
    """제 일 → 뛰기 이음새. 🔴 그냥 갈아타면 관절이 순간이동한다."""
    rt = max(0.0, t - t0)
    k = ease(rt / blend)
    stage.pose(arm, motions.blend(motions.MOTIONS[prev_job](t), motions.run(rt), k))


# 뛰어 나가는 쪽 — 🔴 **각을 손으로 정하지 마라.** 세 번 해 봤고 세 번 다 소품을 뚫었다
#    (궤적 검사가 0.39 m 로 잡았다). 마을 북쪽은 집 셋이 벽이고(y 2.2~4.0), 우물·나무·덤불이
#    남쪽에 흩어져 있어서 「열린 쪽」이 눈에 안 보인다. **재서 고른다.**
FLEE_CANDS = [math.radians(a) for a in range(-175, 6, 5)]     # 아래쪽 반원(카메라 쪽·좌우)


def flee_angles(homes, obstacles, run_time=FLEE_SPAN, body_r=0.22, gap=0.62):
    """여섯이 서로도 소품도 안 뚫고 뛰어 나가는 각을 **하나씩 재서** 고른다(그리디).

    🔑 점수 셋을 본다: 소품 여유 · 서로 벌어지는 정도 · 아래쪽(카메라 쪽)으로 나가는가.
       마을이나 자리가 바뀌어도 이 함수는 그대로 산다 — 손으로 적은 각은 그때 죽는다."""
    picked = []
    for h in homes:
        best, best_s = None, -9e9
        for a in FLEE_CANDS:
            path = [(h[0] + motions.RUN_SPEED * run_time * k / 24 * math.cos(a),
                     h[1] + motions.RUN_SPEED * run_time * k / 24 * math.sin(a))
                    for k in range(25)]
            # 🔴 **선 자리는 안 잰다.** 나무를 패는 사람은 나무 옆에 서 있는 것이 정상이고,
            #    거기서 재면 어떤 각도 통과를 못 한다(실측으로 배웠다 — 각이 하나도 안 남았다).
            #    재는 것은 「나아간 뒤에 무엇을 뚫는가」다.
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
