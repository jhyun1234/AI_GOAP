"""군무와 파열 — 연출 모드 A(설계 §1)의 뼈대.

🔴 **중간은 없다.**
   「같다」  = 위상 오차 0. 여섯이 완전히 같은 프레임에 같은 포즈.
   「갈라진다」 = 위상이 아니라 **동작 자체가 다르다.** 한 명은 밭일, 한 명은 장작.
   살짝 어긋난 여섯은 「대충 비슷한 여섯」으로 읽힌다 — 2D 번역판이 어색했던 이유의 절반이다.

이 파일의 `pose_at` 이 **주민 번호를 안 받는다.** 그게 군무의 정의다 — 번호를 받는 순간
누군가 위상을 어긋내고 싶어지고, 그러면 이 회차의 사건 ①이 깨진다.
"""
import math

import motions

N = 6
# 광장에 서는 자리 — **한 줄로, 깊이로 물러난다.**
# 🔴 가로로 나란히 세우면 세로 화면(1080×846)에서 각자가 화면의 10% 밖에 안 된다.
#    「주인공은 화면의 36~71%」를 못 맞춘다(인계 문서 §3). 비스듬히 보는 한 줄로 두면
#    앞사람이 크고 뒤로 갈수록 작아져 원근이 생기고, 사용자가 승인한 훅의 구도와도 같다.
# 🔑 자리를 흩뜨리지 않는다. 여섯이 **같은 간격 같은 줄**에 서 있는 것 자체가 「똑같다」다.
STANDS = [(i * 1.15 - 2.875, -1.60) for i in range(6)]

# 갈라질 때 여섯이 고르는 동작. 사건 ③ 「열다섯 쌍 전부 갈라짐」의 3D 판이다.
# 🔴 여섯이 **네 종류**를 나눠 갖는다 — 여섯 종류를 만들면 어휘집이 아니라 짐이 된다.
BREAK = ['farm', 'chop', 'draw', 'walk', 'stop', 'farm']
TOGETHER = 'walk'              # 같이 있을 때 여섯이 하는 것

# 🔑 훅이 시작하는 카메라 자리. **인트로가 여기로 착지하고 훅이 여기서 출발한다** —
#    그래야 「컷 없이 이어진다」가 말이 아니라 사실이 된다. 두 파일에 따로 적으면 갈라진다.
HOOK_CAM = ((-6.55, -5.15, 1.55), (0.05, -1.45, 0.58))


# ── 훅 비트 시트 — 여덟 ──────────────────────────────
# 🔴 5비트 판이 반려됐다: 「제자리에서 가만히 걷는 모습은 별로 인상 깊지 않았다」.
#    원인은 비트 수가 아니라 **여섯이 아무 데도 안 갔다는 것**이다 — 다리만 저었고
#    발밑 좌표는 첫 프레임 그대로였다. 화면에서 안 변한 것을 몇 번 반복해도 안 변한다.
# 🔑 그래서 걷는 비트에서 여섯이 **실제로 나아간다**(march). 비트를 여덟로 늘린 것은
#    그 다음이다 — 나아간 거리가 눈에 들어오려면 걷는 비트가 **둘씩 붙어** 있어야 한다.
#    하나짜리 걷기 비트는 한 걸음 반이라 다시 제자리걸음으로 읽힌다.
HOOK_BEATS = ('stand', 'walk', 'walk', 'stop', 'turn', 'walk', 'walk', 'stop')

# ⑤ 에서 여섯이 함께 도는 각.
# 🔴 **꺾는 쪽이 화면에서 무엇이 되는지 보고 정한다.** 처음 보던 쪽(-Y)은 이 카메라에서
#    「왼→오른쪽 가로지르기 + 살짝 다가오기」로 읽힌다. 음수로 꺾으면 카메라를 향해 걸어와서
#    맨 앞 주민의 발이 화면 아래로 잘렸다(실측 — -50° 판). 양수로 꺾으면 깊이를 안 바꾸고
#    **가로지르기가 더 순수해진다** — 배경이 가장 많이 흐르는 방향이고, 그게 「걸어간다」다.
HOOK_TURN = math.radians(40)
TURN_SPAN = 0.60        # 도는 데 쓰는 비트의 비율. 나머지는 돌고 나서 서 있는 시간이다
BLEND = 0.26            # 걷기↔멈춤 이음새(초). 0 이면 다리가 차렷으로 순간이동한다


def _ease(u):
    u = max(0.0, min(1.0, u))
    return u * u * (3 - 2 * u)


def march(t, beat):
    """t 초에 여섯이 **어디에 서서 어디를 보는가.** 반환 (x, y, heading, phase).

    x·y 는 처음 자리에서의 변위, heading 은 처음 보던 쪽에서 더 돈 각(라디안),
    phase 는 **걸은 시간**이다(walk 에 넣을 값).

    🔴 위상도 자리도 **걷는 비트에서만** 흐른다. 멈춘 동안에도 t 를 그대로 주면
       다시 걸을 때 다리가 순간이동하고, 자리를 계속 밀면 선 채로 미끄러진다.
    🔴 나아가는 거리는 `motions.WALK_SPEED` 에서 나온다 — 다리가 만드는 보폭이다.
       여기서 눈대중으로 올리면 발이 미끄러진다(제자리걸음의 반대편 함정)."""
    x = y = phase = 0.0
    heading = 0.0
    for i, kind in enumerate(HOOK_BEATS):
        u = min(t - i * beat, beat)
        if u <= 0:
            break
        if kind == 'walk':
            phase += u
            d = motions.WALK_SPEED * u
            x += d * math.sin(heading)      # heading 0 = 처음 보던 쪽(-Y)
            y -= d * math.cos(heading)
        elif kind == 'turn':
            heading += HOOK_TURN * _ease(u / (beat * TURN_SPAN))
    return x, y, heading, phase


def hook_pose(t, beat):
    """지금 여섯이 취할 포즈. 🔴 **주민 번호를 안 받는다** — 그게 군무다(`pose_at` 과 같은 이유).

    걷기와 멈춤 사이는 `motions.blend` 로 잇는다. 걷기를 반 주기에서 끊으면 허벅지가
    26° 에서 0 으로 튀는데, 그건 「동시에 멈췄다」가 아니라 렉으로 읽힌다."""
    i = min(int(t / beat), len(HOOK_BEATS) - 1)
    walking = HOOK_BEATS[i] == 'walk'
    prev_walking = i > 0 and HOOK_BEATS[i - 1] == 'walk'
    w = motions.walk(march(t, beat)[3])
    s = motions.stop(t)
    k = 1.0 if walking == prev_walking else _ease((t - i * beat) / BLEND)
    return motions.blend(s, w, k) if walking else motions.blend(w, s, k)


def motion_at(t, i, k):
    """i 번째 주민이 지금 무슨 동작을 하는가. k 0 이면 여섯이 같고, 1 이면 갈라진다."""
    return BREAK[i % len(BREAK)] if k >= 0.5 else TOGETHER


def pose_at(motion_name, t):
    """🔴 주민 번호를 **안 받는다.** 그게 군무다."""
    return motions.MOTIONS[motion_name](t)


# 성격 표식 — 주민 머리 위에 뜨는 보라 막대 셋. 여섯이 **서로 다른 높이 조합**을 가진다.
# 🔴 색으로 안 나눈다(설계 §2). 여섯을 여섯 색으로 칠하면 중구난방이고, 무엇보다
#    「여섯이 똑같다」가 이 편의 사건 ①이다. **한 색 안에서 형태로만** 다르다 —
#    그래서 「성격은 서로 다른데 움직임은 하나다」가 한 화면에 같이 선다.
PERSONALITY = [(0.95, 0.40, 0.62), (0.32, 0.95, 0.50), (0.60, 0.58, 0.95),
               (0.92, 0.80, 0.28), (0.38, 0.52, 0.95), (0.70, 0.30, 0.85)]
MARK_Z = 1.22          # 머리 꼭대기(0.95) 위
MARK_W, MARK_GAP, MARK_H = 0.045, 0.062, 0.17


def personality_marks(arms=None, col=None):
    """여섯 머리 위의 성격 표식. 반환: [[bar, ...], ...] (주민별 막대 셋).

    🔴 `arms` 를 주면 표식을 **주민에게 묶는다.** 안 묶으면 주민이 걸어가도 표식은 처음
       자리에 남는다 — 여섯이 실제로 나아가는 판에서 이건 그냥 버그다.
       `matrix_parent_inverse` 로 묶어야 지금 있는 자리를 그대로 유지한 채 따라간다
       (그냥 `parent` 만 넣으면 표식이 주민 발밑으로 끌려간다).
    🔴 그 역행렬은 `matrix_world` 가 아니라 **`matrix_basis`** 에서 뽑는다. 방금 만든
       오브젝트의 `matrix_world` 는 뎁스그래프가 아직 안 돌아 단위행렬이고, 그 상태로
       묶으면 표식이 주민 자리만큼 한 번 더 회전·이동해 하늘로 흩어진다(실제로 그랬다).
       아마추어는 부모가 없어서 `matrix_basis` 가 곧 월드 변환이다."""
    import bpy
    import stage
    col = col or bpy.context.collection
    mat = stage.meaning_mat('violet', strength=1.0)
    out = []
    for i, (x, y) in enumerate(STANDS[:N]):
        bars = []
        for j, h in enumerate(PERSONALITY[i % len(PERSONALITY)]):
            bpy.ops.mesh.primitive_cube_add(
                size=1, location=(x, y + (j - 1) * MARK_GAP, MARK_Z + MARK_H * h / 2))
            b = bpy.context.object
            b.scale = (MARK_W, MARK_W, MARK_H * h)
            b.data.materials.append(mat)
            if arms:
                b.parent = arms[i]
                b.matrix_parent_inverse = arms[i].matrix_basis.inverted()
            bars.append(b)
        out.append(bars)
    return out


def place(n=N, rot_z=90):
    """광장에 n 명을 세운다. bpy 가 필요하므로 블렌더 안에서만 부른다."""
    import stage
    arms = []
    for i in range(n):
        x, y = STANDS[i % len(STANDS)]
        _mesh, arm = stage.rigged(loc=(x, y, 0), rot_z=rot_z)
        arms.append(arm)
    return arms


def apply(arms, motion_name, t):
    """여섯 전부에 **같은** 포즈. 한 번만 계산해서 그대로 나눠 준다."""
    import stage
    spec = pose_at(motion_name, t)
    for arm in arms:
        stage.pose(arm, spec)


def break_apart(arms, t, k):
    """여섯이 서로 다른 동작으로 갈라진다."""
    import stage
    for i, arm in enumerate(arms):
        stage.pose(arm, motions.MOTIONS[motion_at(t, i, k)](t))
