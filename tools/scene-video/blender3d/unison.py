"""군무와 파열 — 연출 모드 A(설계 §1)의 뼈대.

🔴 **중간은 없다.**
   「같다」  = 위상 오차 0. 여섯이 완전히 같은 프레임에 같은 포즈.
   「갈라진다」 = 위상이 아니라 **동작 자체가 다르다.** 한 명은 밭일, 한 명은 장작.
   살짝 어긋난 여섯은 「대충 비슷한 여섯」으로 읽힌다 — 2D 번역판이 어색했던 이유의 절반이다.

이 파일의 `pose_at` 이 **주민 번호를 안 받는다.** 그게 군무의 정의다 — 번호를 받는 순간
누군가 위상을 어긋내고 싶어지고, 그러면 이 회차의 사건 ①이 깨진다.
"""
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


def personality_marks(col=None):
    """여섯 머리 위의 성격 표식. 반환: [[bar, ...], ...] (주민별 막대 셋)."""
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
