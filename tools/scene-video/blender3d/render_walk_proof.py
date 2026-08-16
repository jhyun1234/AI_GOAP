"""여섯이 마을로 **실제로 걸어 들어간다** — 판정 그림.
   blender --background --factory-startup --python render_walk_proof.py

**판정할 질문 둘**
① 여섯이 같은 걸음을 하는가 (군무 — 위상 오차 0)
② 그리고 **자리가 실제로 변하는가.** 앞 판이 반려된 이유가 이것이었다:
   *"제자리에서 가만히 걷는 모습은 별로 인상 깊지 않았다."* 다리만 젓고 발밑 좌표는
   첫 프레임 그대로였다. 화면에서 안 변한 것은 몇 번 반복해도 안 변한다.

🔴 나아가는 거리를 **눈대중으로 정하지 않는다.** `mixamo.ground_speed` 가 클립에서
   직접 잰다 — 다리가 만드는 보폭이 곧 무대가 옮겨야 할 거리다. 어긋난 만큼이 미끄러짐이다.
"""
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mixamo, stage, village   # noqa: E402

OUT = os.path.join(stage.OUT_ROOT, 'walk_proof')
os.makedirs(OUT, exist_ok=True)

S = village.SCALE
N = 6
MOTION = 'walk'
SHOTS = (0.0, 1.1, 2.2, 3.3)          # 몇 초 지점을 볼 것인가

# 여섯이 서는 줄. 🔑 **한 줄로, 깊이로 물러난다** — 비스듬히 보면 앞사람이 크고 뒤로
# 갈수록 작아져 원근이 생긴다. 가로로 나란히 세우면 세로 화면에서 각자가 너무 작다.
# 🔴 좌표는 옛 축척(주민 0.95m)에서 실측한 것이라 `S` 를 곱한다.
COLUMN_X, COLUMN_LEAD, COLUMN_GAP = -1.0, -1.70, 0.95
# 🔴 **180 이다.** 옛 리그 규약(-90)을 그대로 쓰면 여섯이 줄과 직각으로 걸어간다 —
#    Mixamo 캐릭터는 rot_z=0 에서 −Y 를 보고, 옛 리그는 −X 를 봤다(90° 차이).
#    마을 안쪽은 +Y(집 셋이 y 2.2~4.0)이므로 `forward(180) = (0, +1)` 이다.
FACE = 180

bpy.ops.wm.read_factory_settings(use_empty=True)
village.build()

arms = []
for i in range(N):
    _meshes, arm = mixamo.spawn(
        loc=(COLUMN_X * S, (COLUMN_LEAD - i * COLUMN_GAP) * S, 0), rot_z=FACE)
    arms.append(arm)

SPEED = mixamo.ground_speed(MOTION)
FX, FY = mixamo.forward(FACE)
print(f'· {MOTION}: 지면속도 {SPEED:.3f} m/s · 정면 ({FX:+.2f}, {FY:+.2f})')

def _blocked(loc, at):
    """카메라와 겨냥점 사이에 무엇이 있나. 🔴 없으면 이런 사고를 렌더가 끝난 뒤에야 안다."""
    from mathutils import Vector
    a, b = Vector(loc), Vector(at)
    d = b - a
    hit, _p, _n, _i, ob, _m = bpy.context.scene.ray_cast(
        bpy.context.evaluated_depsgraph_get(), a, d.normalized(), distance=d.length * 0.92)
    return ob.name if hit and ob and not ob.name.startswith('villager') else None


home = [tuple(a.location) for a in arms]
cam = stage.light_camera(res=(1080, 846))

for t in SHOTS:
    d = SPEED * t
    for arm, (hx, hy, hz) in zip(arms, home):
        arm.location = (hx + FX * d, hy + FY * d, hz)
        # 🔴 주민 번호를 안 넘긴다. 그게 군무다 — 번호가 들어가는 순간 누군가 위상을
        #    어긋내고 싶어지고, 그러면 「여섯이 똑같다」가 깨진다.
        mixamo.play(arm, MOTION, t, dur=None)
    # 🔴 카메라는 **맨 앞사람**을 잡는다. 줄 한가운데를 `cam_for` 로 잡으면 한 사람 크기로
    #    거리를 역산하므로 여덟 미터짜리 줄이 통째로 화면 밖으로 나간다(첫 판이 그랬다).
    #    앞사람을 규격 크기로 두면 나머지는 원근으로 작아지며 뒤로 물러난다 — 그게 줄이다.
    lead = (COLUMN_X * S + FX * d, COLUMN_LEAD * S + FY * d, 0.55 * mixamo.H)
    # 🔴 yaw 는 **음수**여야 한다(마을 남쪽). 북쪽은 집 셋이 벽처럼 서 있어서(y 3.9~7.2)
    #    +62 로 뒀더니 여섯이 마을로 들어갈수록 카메라가 그 벽 안으로 들어가 3.3초 프레임이
    #    통째로 갈색이었다. 아래 검사가 이제 그걸 렌더 전에 잡는다.
    loc, at = stage.cam_for(lead, frac=0.30, lens=45, yaw=-62, elev=12)
    blocked = _blocked(loc, at)
    if blocked:
        print(f'🔴 {t:.1f}초: 카메라와 인물 사이에 «{blocked}» 이 있다 — yaw 를 바꿔라')
    stage.key_from_view(loc, at)
    stage.aim(cam, loc, at)
    bpy.context.view_layer.update()
    bpy.context.scene.render.filepath = os.path.join(OUT, f'walk_{t:.1f}s.png')
    bpy.ops.render.render(write_still=True)
    print(f'✅ {t:.1f}초 · 나아간 거리 {d:.2f}m')

print(f'→ {OUT}')
