"""모캡 파일럿 판정 그림 (M32 영상 트랙 · 2026-08-16).

    blender --background --factory-startup --python render_mocap_pilot.py

**판정할 질문 하나**: 모캡에서 구워 온 `shoot` 과 손으로 짠 `attack` 이 **같은 사람으로
보이는가.** 보이면 나머지 열하나를 모캡으로 태우고, 안 보이면 손으로 짠다.

🔴 한 프레임으로는 못 판정한다. 스타일 차이는 진폭이 아니라 **타이밍**에 있고(모캡은 예비
   동작이 짧고 고르다), 타이밍은 위상을 여러 장 늘어놓아야 보인다. 그래서 위상 여덟 장이다.

🔴 각도는 **옆모습(-10°)** 이다. 3/4 정면은 앞으로 뻗는 동작을 카메라 쪽으로 단축시켜
   `attack` 의 뻗은 주먹을 굽은 팔로 보이게 한다 (`render_motion_sheet.py` 가 두 판 헛돈 자리).
   활을 당기는 팔도 같은 축이라 같은 함정에 걸린다.
"""
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stage, motions, mocap   # noqa: E402

OUT = os.path.join(stage.OUT_ROOT, 'mocap_pilot')
os.makedirs(OUT, exist_ok=True)

COLS = int(os.environ.get("PILOT_COLS", 8))
# (라벨, f(t), 한 주기 초) — 위상 여덟 장을 한 주기에 고르게 뿌린다.
# 🔑 `shoot` 은 구운 길이가 5.03초인데 명세의 목표는 1.6초다. 위상 정규화가 그 환산을
#    맡으므로 여기서는 **목표 길이**를 그대로 적는다 (`3D_대본_문법.md` §5 규약).
ROWS = [
    ('mocap_shoot', mocap.load('shoot', dur=1.6), 1.6),
    ('mocap_flinch', mocap.load('flinch', dur=0.55, loop=False), 0.55),
    ('mocap_limp', mocap.load('limp', dur=1.4), 1.4),
    ('hand_attack', motions.MOTIONS['attack'], 0.9),
    ('hand_walk', motions.MOTIONS['walk'], 1.0 / motions.WALK_HZ),
]

bpy.ops.wm.read_factory_settings(use_empty=True)
mesh, arm = stage.rigged(loc=(0, 0, 0), rot_z=-10)   # 옆모습 — 오른팔이 안 가린다
cam = stage.light_camera(res=(420, 620))
stage.aim(cam, (-2.10, -2.16, 1.00), (0, 0, 0.45))   # 넓게 — 파일럿은 전신이 다 보여야 판정이 된다

for label, fn, span in ROWS:
    for c in range(COLS):
        t = span * c / COLS
        stage.pose(arm, fn(t))
        bpy.context.scene.render.filepath = os.path.join(OUT, f'{label}_{c:02d}.png')
        bpy.ops.render.render(write_still=True)
    print(f'✅ {label}: {COLS}장 · 한 주기 {span}초')

print(f'출력 {OUT}')
