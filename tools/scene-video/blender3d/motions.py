"""동작 여덟. **순수 함수다** — `bpy` 를 안 만지므로 블렌더 없이 검사된다.

🔴 축 규약(rig.py 가 roll 을 `GLOBAL_POS_X` 로 고정했다): **X = 앞뒤 · Z = 좌우 · Y = 비틀림.**

   ⚠️ **부호는 뼈가 향한 쪽에 따라 뒤집힌다.** 로컬 X 축이 뼈 방향의 함수라서 그렇다.
       · 아래를 향한 뼈(팔·다리) → **X 음수가 앞**
       · 위를 향한 뼈(척추·목·머리) → **X 양수가 뒤로 젖힘(=위를 봄)**
   이걸 놓쳐서 `look_up` 이 고개를 **숙이고** 있었다. 사용자가 인트로를 보고 잡았다 —
   「주민이 나온 다음에 다같이 고개 숙이고 끝」. 검사가 아니라 사람 눈이 잡은 버그다.
   그래서 아래 test_motions 에 **위 뼈·아래 뼈 부호를 둘 다** 박아 뒀다.

🔴 **안 쓰는 동작은 안 만든다.** 여기 아홉은 전부 ep15s-1 의 비트 시트가 실제로 쓰는 것이다.
   필요한 동작이 생기면 그 회차가 처음 만든다 — 어휘집이 아니라 짐이 되지 않게.

⚠️ 시간 인자 `t` 는 **초**다. 주기가 있는 동작은 t 로 감고, 한 번뿐인 동작은 t 로 진행한다.
"""
import math

R = math.radians
WALK_HZ = 1.15          # engine/motions.mjs 의 walk 와 같은 주기 — 표를 갈라 두지 않는다

# ── 걷기가 **실제로 나아가는 속도** ──────────────────────
# 🔴 이 값을 눈대중으로 정하지 마라. 자리를 다리보다 빨리 밀면 발이 미끄러지고,
#    느리게 밀면 발이 끌린다. 둘 다 「걷는 것 같지 않다」로 보인다.
#    다리가 실제로 만드는 보폭에서 역산한다: 한 걸음 = 2·다리길이·sin(허벅지 진폭),
#    한 주기에 두 걸음.
# 🔑 진폭이 곧 보폭이다. 26° 판은 한 걸음 0.245 라 훅 내내 1.9 m 밖에 못 갔고, 그 거리는
#    화면에서 「걸어갔다」로 안 읽혔다. 34° 는 성큼 걷는 걸음이고 훅에서 2.4 m 를 간다.
WALK_THIGH = R(34)      # walk() 의 thigh 진폭과 **같은 값**이어야 한다(검사가 대조한다)
LEG = 0.28              # 골반(rig.py thigh 머리 z=0.300)에서 발바닥까지. 주민 키는 0.95
WALK_SLIP = 1.0         # 🔑 손잡이. 렌더에서 발이 미끄러져 보이면 여기를 0.9 쪽으로 내려라
WALK_STRIDE = 2 * LEG * math.sin(WALK_THIGH)
WALK_SPEED = WALK_STRIDE * 2 * WALK_HZ * WALK_SLIP      # m/s


def _ease(u):
    u = max(0.0, min(1.0, u))
    return u * u * (3 - 2 * u)


def look_up(t):
    """고개를 든다. 인트로 마지막 비트이자 훅의 첫 비트 — 여섯이 **동시에** 한다.

    🔴 목·머리·척추는 **위를 향한 뼈**라 X 양수가 위를 보는 쪽이다(모듈 주석의 부호 규약).
       음수로 두면 고개를 숙인다 — 첫 판이 정확히 그랬다."""
    k = _ease(t / 0.35)
    return {'neck': (R(15) * k, 0, 0), 'head': (R(13) * k, 0, 0),
            'spine': (R(4) * k, 0, 0)}


def walk(t):
    ph = 2 * math.pi * WALK_HZ * t
    s, c = math.sin(ph), math.cos(ph)
    knee_l = max(0.0, math.sin(ph - 0.9))
    knee_r = max(0.0, math.sin(ph + math.pi - 0.9))
    return {
        'thigh.L': (-WALK_THIGH * s, 0, 0), 'shin.L': (R(34) * knee_l, 0, 0),
        'thigh.R': (WALK_THIGH * s, 0, 0), 'shin.R': (R(34) * knee_r, 0, 0),
        'foot.L': (R(12) * s, 0, 0), 'foot.R': (R(-12) * s, 0, 0),
        'upperarm.L': (R(17) * s, 0, 0), 'upperarm.R': (R(-17) * s, 0, 0),
        'forearm.L': (R(-12) * (1 - c) / 2, 0, 0),
        'forearm.R': (R(-12) * (1 + c) / 2, 0, 0),
        'spine': (R(2) * c, 0, 0),
    }


def stop(t):
    """멈춤 — 숨만 쉰다. 완전히 굳으면 마네킹으로 읽힌다."""
    b = math.sin(2 * math.pi * 0.26 * t)
    return {'spine': (R(1.2) * b, 0, 0), 'neck': (R(-0.8) * b, 0, 0)}


def farm(t):
    """밭일 — 허리를 굽혀 앞으로 훑는다. 주기 1.4 초."""
    s = math.sin(2 * math.pi * t / 1.4)
    return {'spine': (R(-32) + R(9) * s, 0, 0), 'neck': (R(-10), 0, 0),
            'upperarm.L': (R(-46) + R(16) * s, 0, 0), 'forearm.L': (R(-28), 0, 0),
            'upperarm.R': (R(-46) - R(16) * s, 0, 0), 'forearm.R': (R(-28), 0, 0),
            'thigh.L': (R(-8), 0, 0), 'thigh.R': (R(-8), 0, 0)}


def chop(t):
    """장작 — 들었다 내리친다. 주기 1.1 초. **내리칠 때가 빠르다**(들 때 0.62, 칠 때 0.38)."""
    u = (t % 1.1) / 1.1
    swing = _ease(u / 0.62) if u < 0.62 else 1 - _ease((u - 0.62) / 0.38)
    up = 1 - swing
    return {'upperarm.L': (R(-150) + R(140) * up, 0, 0),
            'upperarm.R': (R(-150) + R(140) * up, 0, 0),
            'forearm.L': (R(-20), 0, 0), 'forearm.R': (R(-20), 0, 0),
            'spine': (R(-6) - R(16) * up, 0, 0)}


def draw(t):
    """우물 — 두레박 줄을 번갈아 당긴다. 주기 1.6 초."""
    s = math.sin(2 * math.pi * t / 1.6)
    return {'upperarm.L': (R(-70) + R(26) * s, 0, 0), 'forearm.L': (R(-52) - R(24) * s, 0, 0),
            'upperarm.R': (R(-70) - R(26) * s, 0, 0), 'forearm.R': (R(-52) + R(24) * s, 0, 0),
            'spine': (R(-9), 0, 0)}


def reach(t):
    """뻗기 — 0.62 초에 다 뻗고 그 뒤로는 **한 톨도 안 움직인다**(굳음의 준비)."""
    k = _ease(min(t, 0.62) / 0.62)
    return {'upperarm.L': (R(-58) * k, 0, R(6)), 'forearm.L': (R(-30) * k, 0, 0),
            'upperarm.R': (R(-40) * k, 0, R(-6)), 'forearm.R': (R(-22) * k, 0, 0),
            'spine': (R(-9) * k, 0, 0), 'neck': (R(-5) * k, 0, 0)}


def warm(t):
    """불 쬐기 — 두 손을 불 쪽으로 내밀고 아주 조금 흔들린다. 주기 2.2 초.

    🔴 `reach` 로 때우지 마라. 뻗기는 **굳음의 준비**라 뜻이 이미 배정돼 있고(freeze 가
       그 포즈를 그대로 쓴다), 같은 포즈에 뜻 둘을 얹으면 아웃트로의 「굳었다」가 안 선다."""
    b = math.sin(2 * math.pi * t / 2.2)
    return {'upperarm.L': (R(-52) + R(4) * b, 0, R(9)), 'forearm.L': (R(-36), 0, 0),
            'upperarm.R': (R(-52) - R(4) * b, 0, R(-9)), 'forearm.R': (R(-36), 0, 0),
            'spine': (R(-7), 0, 0), 'neck': (R(-4), 0, 0)}


def freeze(t):
    """굳음 — 뻗은 자세 그대로 정지. **숨도 안 쉰다.** 그게 이 동작의 뜻이다."""
    return reach(1.0)


def blend(a, b, k):
    """포즈 둘 사이. k 0 이면 a, 1 이면 b.

    🔴 걷다가 멈추는 이음새에 쓴다. 안 쓰면 다리가 **한 프레임에 차렷으로 순간이동한다** —
       걷기를 반 주기에서 끊으면 허벅지가 26° 에서 0 으로 튄다.
    🔴 한쪽에만 있는 뼈는 반대쪽을 **0(차렷)** 으로 친다. `stage.pose` 가 안 적힌 뼈를
       0 으로 되돌리는 것과 같은 규약이다 — 여기서 규약을 갈라 놓으면 이음새가 튄다."""
    k = max(0.0, min(1.0, k))
    z = (0.0, 0.0, 0.0)
    return {bone: tuple(p + (q - p) * k
                        for p, q in zip(a.get(bone, z), b.get(bone, z)))
            for bone in set(a) | set(b)}


def sequence(names, t, beat, span=0.30):
    """비트마다 동작을 갈아탄다. `names[i]` 가 i 번째 비트의 동작이다.

    🔴 그냥 갈아타면 관절이 순간이동한다 — 밭일은 허리를 32° 굽히고 있어서 우물 긷기로
       바로 넘기면 상체가 한 프레임에 튄다. `unison.hook_pose` 가 걷기↔멈춤에 쓰는 것과
       같은 이음새를 여기서도 쓴다."""
    i = min(int(t / beat), len(names) - 1)
    cur = MOTIONS[names[i]](t)
    if i == 0 or names[i] == names[i - 1]:
        return cur
    return blend(MOTIONS[names[i - 1]](t), cur, _ease((t - i * beat) / span))


MOTIONS = {'look_up' : look_up, 'walk': walk, 'stop': stop, 'farm': farm,
           'chop': chop, 'draw': draw, 'warm': warm, 'reach': reach, 'freeze': freeze}

# 주기가 있는 동작과 그 주기(초). 이어 붙일 때 튀지 않으려면 이 값으로 감아야 한다.
CYCLE = {'walk': 1.0 / WALK_HZ, 'stop': 1.0 / 0.26, 'farm': 1.4, 'chop': 1.1,
         'draw': 1.6, 'warm': 2.2}
