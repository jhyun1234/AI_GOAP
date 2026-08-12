"""동작 여덟. **순수 함수다** — `bpy` 를 안 만지므로 블렌더 없이 검사된다.

🔴 축 규약(rig.py 가 roll 을 `GLOBAL_POS_X` 로 고정했다): **X = 앞뒤 · Z = 좌우 · Y = 비틀림.**

   ⚠️ **부호는 뼈가 향한 쪽에 따라 뒤집힌다.** 로컬 X 축이 뼈 방향의 함수라서 그렇다.
       · 아래를 향한 뼈(팔·다리) → **X 음수가 앞**
       · 위를 향한 뼈(척추·목·머리) → **X 양수가 뒤로 젖힘(=위를 봄)**
   이걸 놓쳐서 `look_up` 이 고개를 **숙이고** 있었다. 사용자가 인트로를 보고 잡았다 —
   「주민이 나온 다음에 다같이 고개 숙이고 끝」. 검사가 아니라 사람 눈이 잡은 버그다.
   그래서 아래 test_motions 에 **위 뼈·아래 뼈 부호를 둘 다** 박아 뒀다.

🔴 **안 쓰는 동작은 안 만든다.** 여기 여덟은 전부 ep15s-1 의 비트 시트가 실제로 쓰는 것이다.
   필요한 동작이 생기면 그 회차가 처음 만든다 — 어휘집이 아니라 짐이 되지 않게.

⚠️ 시간 인자 `t` 는 **초**다. 주기가 있는 동작은 t 로 감고, 한 번뿐인 동작은 t 로 진행한다.
"""
import math

R = math.radians
WALK_HZ = 1.15          # engine/motions.mjs 의 walk 와 같은 주기 — 표를 갈라 두지 않는다


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
        'thigh.L': (R(-26) * s, 0, 0), 'shin.L': (R(34) * knee_l, 0, 0),
        'thigh.R': (R(26) * s, 0, 0), 'shin.R': (R(34) * knee_r, 0, 0),
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


def freeze(t):
    """굳음 — 뻗은 자세 그대로 정지. **숨도 안 쉰다.** 그게 이 동작의 뜻이다."""
    return reach(1.0)


MOTIONS = {'look_up': look_up, 'walk': walk, 'stop': stop, 'farm': farm,
           'chop': chop, 'draw': draw, 'reach': reach, 'freeze': freeze}

# 주기가 있는 동작과 그 주기(초). 이어 붙일 때 튀지 않으려면 이 값으로 감아야 한다.
CYCLE = {'walk': 1.0 / WALK_HZ, 'stop': 1.0 / 0.26, 'farm': 1.4, 'chop': 1.1, 'draw': 1.6}
