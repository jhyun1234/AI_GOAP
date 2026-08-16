"""Mixamo 네이티브 판정 그림 (M32 영상 트랙 · 2026-08-16).

    blender --background --factory-startup --python render_mixamo_check.py

**판정할 질문 하나**: 동작이 **읽히는가.** 조각이 안 흩어지는 것은 자체 점검이 보고
있으니, 여기서 볼 것은 「쏜다」·「절뚝인다」·「맞는다」가 그림으로 읽히느냐다.

🔴 한 장으로는 못 판정한다. 동작은 위상의 연속이라 여섯 장을 **한 줄에 늘어놓아야**
   보인다. 그래서 여섯을 세우고 각자 다른 위상을 준다 — 이게 `play` 가 씬 프레임을
   안 쓰는 이유의 실증이기도 하다(전역 프레임 하나였으면 이 그림을 못 만든다).
🔴 각도는 **옆모습**이다. 3/4 정면은 앞으로 뻗는 동작을 카메라 쪽으로 단축시켜
   당기는 팔을 굽은 팔로 보이게 한다(옛 판이 두 번 헛돈 자리).
"""
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mixamo, stage   # noqa: E402

OUT = os.path.join(stage.OUT_ROOT, 'mixamo_check')
os.makedirs(OUT, exist_ok=True)

H = mixamo.H
COLS = 6
GAP = 0.85 * H                     # 🔑 미터를 손으로 적지 않는다 — 키의 배수다
DIST = 6.2 * H
RES = (1680, 620)

# (동작, 목표 길이 초, 반복하나) — 길이는 **대본이 정하는 값**이지 구운 길이가 아니다.
# 🔑 한 번뿐인 동작(`death`·`flinch`)은 `loop=False` 다. 반복하면 사람이 죽었다 살아난다.
WANT = [('idle', 2.4, True), ('walk', 1.07, True), ('run', 0.67, True),
        ('attack', 0.9, False), ('shoot', 1.6, False),
        ('flinch', 0.55, False), ('death', 2.0, False), ('limp', 1.4, True),
        # 일상 — 마을이 무언가를 하고 있다는 것을 보이는 몸짓들
        ('work', 1.1, True), ('water', 2.4, True), ('carry', 1.2, True),
        ('talk', 2.6, True), ('sit', 3.0, True), ('look', 3.2, True)]

bpy.ops.wm.read_factory_settings(use_empty=True)
arms = []
x0 = -GAP * (COLS - 1) / 2
for c in range(COLS):
    _meshes, arm = mixamo.spawn(loc=(x0 + c * GAP, 0, 0), rot_z=-10)
    arms.append(arm)

cam = stage.light_camera(res=RES)
stage.aim(cam, (0.0, -DIST, 0.62 * H), (0.0, 0.0, 0.55 * H))

have = {a.name for a in bpy.data.actions}
ROWS = [r for r in WANT if r[0] in have]
missing = [n for n, _s, _l in WANT if n not in have]
if missing:
    print(f'⚠️ 아직 안 받은 동작: {", ".join(missing)} (MIXAMO.md 참조)')

for name, span, loop in ROWS:
    for c, arm in enumerate(arms):
        mixamo.play(arm, name, span * c / COLS, dur=span, loop=loop)
    bpy.context.scene.render.filepath = os.path.join(OUT, f'{name}.png')
    bpy.ops.render.render(write_still=True)
    print(f'✅ {name}: 위상 {COLS}장 · 한 주기 {span}초 (구운 길이 {mixamo.seconds(name):.2f}초)')

print(f'→ {OUT}')
