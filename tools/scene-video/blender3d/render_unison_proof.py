"""군무 3 초 → 파열 1.5 초. **사람이 보고 판정한다.**
   blender --background --factory-startup --python render_unison_proof.py

🔴 이 판이 설계의 시금석이다(설계 §9). 물을 것은 하나다 —
   **자막 없이, 처음 0.3 초에 「여섯이 똑같다」가 읽히는가.**
   안 읽히면 연출 모드 A(군무·파열)를 통째로 다시 본다."""
import bpy, sys, os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stage, village, unison

OUT = os.path.join(stage.OUT_ROOT, 'proof', 'unison', 'frames')
FPS, DUR, BREAK_AT = 30, 4.5, 3.0
os.makedirs(OUT, exist_ok=True)


def ease(u):
    u = max(0.0, min(1.0, u))
    return u * u * (3 - 2 * u)


bpy.ops.wm.read_factory_settings(use_empty=True)
village.build()
arms = unison.place()
cam = stage.light_camera()

for fi in range(round(DUR * FPS) + 1):
    t = fi / FPS
    # 파열은 0.4 초 안에 끝난다 — 뜸들이면 「갈라졌다」가 아니라 「흩어졌다」로 읽힌다
    k = 0.0 if t < BREAK_AT else min(1.0, (t - BREAK_AT) / 0.4)
    if k < 0.5:
        unison.apply(arms, unison.TOGETHER, t)
    else:
        unison.break_apart(arms, t, k)

    # 카메라는 계속 움직인다(설계 §6-3: 컷은 사건에만, 이동은 연속)
    u = ease(t / DUR)
    # 시작 자리는 unison.HOOK_CAM — 인트로가 착지하는 바로 그 자리다
    (sx, sy, sz), at = unison.HOOK_CAM
    stage.aim(cam, (sx + 1.05 * u, sy - 0.30 * u, sz + 0.25 * u), at)

    bpy.context.view_layer.update()
    bpy.context.scene.render.filepath = os.path.join(OUT, '%04d.png' % fi)
    bpy.ops.render.render(write_still=True)

print('[unison] %d frames -> %s' % (round(DUR * FPS) + 1, OUT))
