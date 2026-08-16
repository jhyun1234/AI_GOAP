"""걸음의 지면 속도를 **잰다**. 눈대중으로 정하면 발이 미끄러진다.

    blender --background --factory-startup --python measure_gait.py -- walk run

In Place 클립은 발이 제자리에서 도는 대신 **디딘 발이 뒤로 흐른다.** 그 뒤로 흐르는
속도가 곧 「무대가 사람을 앞으로 옮겨야 하는 속도」다. 둘이 어긋나면 그 차이가 그대로
발 미끄러짐이 된다 — 옛 판이 `WALK_SLIP` 이라는 손잡이로 감추다가 걷어낸 자리다.

🔑 축을 안 맞힌다. 두 축을 다 재서 **큰 쪽**을 앞뒤로 본다.
🔴 디딘 발은 **땅에 가까운 발**이다. 「더 뒤로 가는 발」로 골랐다가 한 판 틀렸다 —
   그건 공중에서 앞으로 휘두르는 발이라 부호가 반대고, 속도도 두 배쯤 크다.
   실측(`walk`): 발끝 z 가 3.0cm 일 때 속도가 +5.2cm/프레임로 **일정**하고,
   z 가 16cm 까지 뜨는 발은 −12cm/프레임까지 간다. 높이가 스탠스를 가른다.
🔴 그리고 **체공 프레임을 빼야 한다.** 달리기는 두 발이 다 뜬 순간이 있어서, 그때는
   「더 낮은 발」도 공중이다. 그 프레임을 섞으면 속도가 부풀고(실측 +4.09 vs +3.0 대),
   미끄러짐 검사도 있지도 않은 미끄러짐을 잡는다 — 발이 안 닿았는데 미끄러질 수는 없다.
"""
import os
import statistics
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mixamo   # noqa: E402

TOES = ('mixamorig:LeftToeBase', 'mixamorig:RightToeBase')
FPS = 30.0
# 가장 낮은 발끝에서 이만큼 안이면 「디뎠다」로 본다(미터). 실측: `walk` 의 스탠스가
# 바닥+0.000~0.002, 스윙이 +0.13 까지 뜬다. 여유는 넉넉해도 둘이 안 섞인다.
STANCE_Z = 0.02


def measure(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _meshes, arm = mixamo.spawn(loc=(0, 0, 0), rot_z=0)
    f0, f1 = mixamo.span_frames(mixamo.action(name))
    n = int(f1 - f0) + 1
    span = n / FPS

    track = []
    for i in range(n):
        mixamo.play(arm, name, span * i / n, dur=span)
        bpy.context.view_layer.update()
        track.append([(arm.matrix_world @ arm.pose.bones[t].matrix.translation).copy()
                      for t in TOES])

    # 어느 축이 앞뒤인가 — 발이 가장 크게 오간 축
    rng = [max(p[k][ax] for p in track) - min(p[k][ax] for p in track)
           for ax in (0, 1) for k in (0, 1)]
    axis = 0 if max(rng[0], rng[1]) >= max(rng[2], rng[3]) else 1

    floor = min(p[k].z for p in track for k in (0, 1))
    vs, flight = [], 0
    for a, b in zip(track, track[1:]):
        k = 0 if a[0].z <= a[1].z else 1          # 땅에 가까운 발 = 디딘 발
        if a[k].z > floor + STANCE_Z:             # 둘 다 떠 있다 — 체공
            flight += 1
            continue
        if b[k].z > floor + STANCE_Z:             # 발이 바뀌는 프레임 — 스탠스가 아니다
            continue
        vs.append((b[k][axis] - a[k][axis]) * FPS)
    ground = statistics.median(vs)                # **부호 있는** 지면 속도

    lift = max(p[k].z for p in track for k in (0, 1))
    print(f'{name}: 주기 {span:.2f}초 · 축 {"XY"[axis]} · 지면속도 {ground:+.3f} m/s '
          f'· 한 주기 이동 {abs(ground) * span:.3f} m · 발끝 최고 {lift:.3f} m '
          f'· 체공 {flight}/{len(track) - 1}프레임')
    return ground, span, axis


SLIP_MAX = 0.02 * (mixamo.H / 0.95)      # 옛 게이트 2cm 를 키 비율로 옮긴 값
# 문턱을 넘는 프레임이 이 비율 아래면 통과. 🔴 **최대값 하나로 판정하지 않는다** —
# 원본 클립에 있는 한 번의 발 끌림(`limp` 의 다친 발이 실제로 긁는다)까지 잡아서
# 「고칠 수 없는 실패」가 되고, 그러면 검사가 무시된다. 재는 것은 **계통적 미끄러짐**이다.
SLIP_FRAC = 0.10
NOT_LOCOMOTION = 0.05                    # 이보다 느리면 걷는 동작이 아니다(m/s)


def verify(name, ground, axis):
    """잰 속도로 무대가 옮겼을 때 **디딘 발이 실제로 서 있는가.**

    🔴 이게 없으면 속도는 그냥 주장이다. 옛 판이 `WALK_SLIP` 이라는 손잡이로 미끄러짐을
       감추다 걷어낸 자리이고, 사람 눈이 가장 먼저 잡는 결함이다."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _meshes, arm = mixamo.spawn(loc=(0, 0, 0), rot_z=0)
    f0, f1 = mixamo.span_frames(mixamo.action(name))
    n = int(f1 - f0) + 1
    span = n / FPS

    world = []
    for i in range(n):
        t = span * i / n
        mixamo.play(arm, name, t, dur=span)
        # 🔴 무대는 지면이 흐르는 **반대**로 옮긴다. 그래야 디딘 발이 세계에 못 박힌다.
        loc = [0.0, 0.0, 0.0]
        loc[axis] = -ground * t
        arm.location = tuple(loc)
        bpy.context.view_layer.update()
        world.append([(arm.matrix_world @ arm.pose.bones[tb].matrix.translation).copy()
                      for tb in TOES])

    # 디딘 발이 세계에서 얼마나 흘렀나. 🔴 체공 프레임은 안 센다 — 안 닿은 발은
    #    미끄러질 수 없고, 그걸 세면 달리기가 영원히 못 통과한다.
    # 🔴 **양쪽 프레임 모두** 디딘 발이어야 센다. 한쪽만 보면 발이 바뀌는 프레임에서
    #    「방금 디딘 발이 직전에 공중에서 움직인 거리」를 미끄러짐으로 센다.
    floor = min(p[k].z for p in world for k in (0, 1))
    slips = []
    for a, b in zip(world, world[1:]):
        k = 0 if a[0].z <= a[1].z else 1
        if a[k].z <= floor + STANCE_Z and b[k].z <= floor + STANCE_Z:
            slips.append((b[k] - a[k]).xy.length)
    if not slips:
        print(f'⚠️ {name}: 디딘 프레임이 없다 — 잴 것이 없다')
        return True
    over = sum(1 for v in slips if v > SLIP_MAX)
    frac = over / len(slips)
    ok = '✅' if frac <= SLIP_FRAC else '🔴'
    print(f'{ok} {name}: 디딘 발 중앙값 {statistics.median(slips) * 100:.2f}cm · '
          f'최대 {max(slips) * 100:.2f}cm · 문턱({SLIP_MAX * 100:.1f}cm) 초과 '
          f'{over}/{len(slips)}프레임')
    return frac <= SLIP_FRAC


if __name__ == '__main__':
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else ['walk']
    bad = []
    for nm in argv:
        ground, _span, axis = measure(nm)
        if abs(ground) < NOT_LOCOMOTION:
            # 🔑 제자리 동작은 무대가 안 옮기므로 미끄러짐 검사의 대상이 아니다.
            #    `attack` 이 여기다 — 골반이 0.47m 나아가지만 그건 **찌르는 발놀림**이고
            #    지면 속도는 0 이다. 걷기 검사에 넣으면 영원히 빨간불이다.
            print(f'· {nm}: 지면 속도 {ground:+.3f} m/s — 걷는 동작이 아니다(검사 건너뜀)')
            continue
        if not verify(nm, ground, axis):
            bad.append(nm)
    if bad:
        print(f'🔴 발이 미끄러진다: {", ".join(bad)}')
