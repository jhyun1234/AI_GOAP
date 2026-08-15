"""동작 어휘 전부의 컨택트 시트 — 각도 둘(3/4 정면 · 옆). **웨이트가 새는지 여기서 본다.**
   blender --background --factory-startup --python render_motion_sheet.py

🔴 `chop` 은 팔을 머리 위로 든다 — 골반이 딸려 오면 여기서 보인다. 이 모델은 손이 골반에
   붙어 있던 것을 떼어 물샐틈없게 만든 덕에 뼈 히트가 18본 전부 붙었지만, 붙었다는 것과
   깨끗하게 변형된다는 것은 다른 일이다."""
import bpy, sys, os, math

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stage, motions, village

OUT = os.path.join(stage.OUT_ROOT, 'models', 'motion_sheet')
os.makedirs(OUT, exist_ok=True)

# 동작마다 「가장 잘 보이는 순간」이 다르다. 주기 동작은 극점을, 한 번뿐인 것은 끝을 찍는다.
# 🔑 동작을 추가하면 **여기에 한 줄만** 넣으면 된다 — 아래 루프가 이 표를 돈다.
AT = {'look_up': 0.35, 'walk': 0.25 / motions.WALK_HZ, 'stop': 0.0,
      # 🔴 타격 동작은 **타격 순간**을 잡는다. 위상을 바로잡기 전(2026-08-13)에는 이 값들이
      #    거꾸로 된 구간을 가리키고 있었다 — 시트가 헛것을 판정하고 있었던 셈이다.
      'farm': 0.35, 'chop': 0.93 * 1.1, 'draw': 0.4, 'reach': 0.9, 'freeze': 0.9,
      'warm': 0.18, 'huddle': 0.25, 'eat': 0.58 * 1.5,
      'run': 0.25 / motions.RUN_HZ,          # 체공 직전, 다리가 제일 벌어진 순간
      'rest': 0.0,
      'gather': 0.55 * 1.8,                  # 팔이 제일 높이 올라간 순간
      'attack': 0.555,                       # 타격 순간(예비 0.34 + 가속 0.42)
      'look_around': 0.8,                    # 고개가 한쪽 끝에 가 있는 순간
      'mine': 0.87 * 1.25,                   # 뿔이 박히는 순간
      'hammer': 0.90 * 0.75,                 # 망치가 닿는 순간
      'water': 0.30 * 2.6,
      'slash': motions.SLASH_CYCLE * 0.55,     # 칼이 몸 앞을 지나는 순간
      'thrust': motions.THRUST_CYCLE * 0.52}   # 팔이 다 펴진 순간                   # 통이 가장 기운 순간

bpy.ops.wm.read_factory_settings(use_empty=True)
# 🔴 rot_z 135 는 정면 벡터가 시선과 **정확히 수직**이라 완전한 옆모습이 나온다(실측).
#    카메라 방향(-0.70, -0.72)에 대해 정면이 3/4 로 열리는 자리가 80 이다.
mesh, arm = stage.rigged(loc=(0, 0, 0), rot_z=80)
cam = stage.light_camera(res=(560, 800))
stage.aim(cam, (-1.30, -1.34, 0.68), (0, 0, 0.50))   # 3/4 앞 — 팔과 골반이 같이 보이는 각도

missing = sorted(set(motions.MOTIONS) - set(AT))
assert not missing, '컨택트 시트에 안 실린 동작이 있다: %s' % missing

# 🔴 각도 하나로 판정하지 마라. 3/4 정면은 **앞으로 뻗는 동작을 카메라 쪽으로 단축시킨다** —
#    `attack` 의 뻗은 주먹이 굽은 팔로 보여서 「가드 자세다」로 두 판을 헛돌았다.
#    몸을 90° 돌린 옆모습을 같이 굽는다. 앞뒤 스윙은 옆에서만 제 길이로 보인다.
#    🔴 옆모습은 **-10° 다(170° 이 아니다).** `eat`·`gather`·`attack` 은 전부 **오른팔**이
#       일하는데, 170° 은 왼쪽 옆구리를 보여 줘서 그 오른팔이 몸통 뒤에 가린다.
#       가려진 팔을 보고 「팔을 안 들었다」고 두 판을 헛돌았다.
VIEWS = (('', 80), ('_side', -10))

# 도구를 쥐는 동작. 🔴 `chop`·`mine`·`hammer` 는 **도구 없이는 서로 구별이 안 된다** —
#    셋 다 「팔을 들었다 내린다」다. 그래서 시트에서도 쥐고 찍는다.
TOOLS = {'chop': ('axe', (0.0, 0.02, 0.0), 0.0),
         'mine': ('pickaxe', (0.0, 0.02, 0.0), 0.0),
         'hammer': ('hammer', (0.0, 0.02, 0.0), 0.0),
         'water': ('wateringcan', (0.0, 0.03, 0.0), 0.0)}
_built = {}


def tool_for(name):
    """이 동작이 쥐는 도구를 손에 붙이고, 나머지 도구는 렌더에서 뺀다."""
    for root in _built.values():
        for c in root.children:
            c.hide_render = True
    if name not in TOOLS:
        return
    kind, loc, rot_x = TOOLS[name]
    if kind not in _built:
        _built[kind] = getattr(village, kind)()
    root = _built[kind]
    stage.hold(arm, root, 'hand.R', loc, rot_x)
    for c in root.children:
        c.hide_render = False

for name in sorted(AT):
    tool_for(name)
    for suffix, rot_z in VIEWS:
        arm.rotation_euler = (0, 0, math.radians(rot_z))
        stage.pose(arm, motions.MOTIONS[name](AT[name]))
        bpy.context.view_layer.update()
        bpy.context.scene.render.filepath = os.path.join(OUT, name + suffix + '.png')
        bpy.ops.render.render(write_still=True)
    stage.pose(arm, motions.MOTIONS[name](AT[name]))
    bpy.context.view_layer.update()
    bpy.context.scene.render.filepath = os.path.join(OUT, name + '.png')
    bpy.ops.render.render(write_still=True)
    print('[motion]', name)
