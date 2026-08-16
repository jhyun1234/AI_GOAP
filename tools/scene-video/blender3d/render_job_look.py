"""**한 몸짓이 세 가지 일로 읽히는가** — 이 설계의 유일한 위험 지점 판정.
   blender --background --factory-startup --python render_job_look.py

`mixamo.JOB` 은 도끼질·곡괭이질·망치질에 **같은 `work` 클립**을 쓴다. 다른 것은
손에 든 것뿐이다. 그 도박이 맞는지는 굽어서 봐야 안다 — 안 읽히면 클립을 셋 받는다.

🔴 같은 **위상**으로 세운다. 위상이 다르면 「도구가 갈랐는가」와 「자세가 갈랐는가」가
   안 갈린다. 셋이 똑같은 순간이라야 도구만 남는다.
"""
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mixamo, stage, village   # noqa: E402

OUT = os.path.join(stage.OUT_ROOT, 'job_look')
os.makedirs(OUT, exist_ok=True)

H = mixamo.H
AT = float(os.environ.get('JOB_AT', '0.62'))   # 한 주기의 어디를 볼 것인가 (내려치는 순간 근처)
DUR = 1.1
GAP = 1.20 * H

# (라벨, AnimKind) — `mixamo.JOB` 이 클립과 도구를 안다. 여기서 다시 안 적는다.
KINDS = ['Chop', 'Mine', 'Hammer', 'Water', 'Attack']

bpy.ops.wm.read_factory_settings(use_empty=True)
mats = village.mats()          # 도구가 마을 재질을 쓴다

x0 = -GAP * (len(KINDS) - 1) / 2
for i, kind in enumerate(KINDS):
    clip, tool_name = mixamo.JOB[kind]
    _meshes, arm = mixamo.spawn(loc=(x0 + i * GAP, 0, 0), rot_z=-70)
    mixamo.play(arm, clip, AT * DUR, dur=DUR, loop=(clip == 'work'))
    tool = getattr(village, tool_name)()
    # 🔴 뼈 자식은 **뼈 꼬리**에 붙는다 — 손뼈 꼬리는 손끝이다. 자루가 뼈 로컬 +Y 를
    #    따라 눕도록 만들어 뒀으므로(`village._haft`) 손바닥 쪽으로 조금 당겨 준다.
    # 🔴 `size` 는 마을 축척이다. 도구는 옛 주민(0.95m) 기준으로 지어졌고 `build()` 를
    #    안 불렀으니 축척이 안 먹었다 — 여기서 먹인다.
    # 쥐는 자세는 `stage.GRIP_*` 이 기본을 알고, 예외는 도구 쪽(`village.GRIP_ROT`)이 안다
    stage.hold(arm, tool, rot=village.grip_rot(tool_name,
                                               stage.GRIP_ROT), size=village.SCALE)
    print(f'· {kind}: 클립 {clip} · 도구 {tool_name}')

cam = stage.light_camera(res=(1680, 620))
stage.aim(cam, (0.0, -8.2 * H, 0.70 * H), (0.0, 0.0, 0.55 * H))
bpy.context.scene.render.filepath = os.path.join(OUT, f'jobs_{AT:.2f}.png')
bpy.ops.render.render(write_still=True)
print(f'→ {bpy.context.scene.render.filepath}')
