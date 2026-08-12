"""ep15s-1 훅 — 5 비트 · 6.61 초. **설계의 시금석이다.**
   blender --background --factory-startup --python render_hook.py

  0.00~1.30  ① 여섯 머리 위에 **서로 다른** 성격 표식이 뜬다
  1.30~2.60  ② 동시에 걷는다
  2.60~3.90  ③ **동시에 멈춘다**
  3.90~5.20  ④ 동시에 같은 쪽으로 몸을 돌린다
  5.20~6.61  ⑤ 동시에 다시 걷는다

🔴 물을 것은 하나다 — **자막 없이, 처음 0.3 초에 「표식은 다른데 움직임이 하나다」가
   읽히는가.** 안 읽히면 연출 모드 A(군무·파열)를 통째로 다시 본다(설계 §9).

🔑 같은 걷기를 6.6 초 트는 것이 아니라 **멈추고·돌고·다시 걷는 것**을 넣는다. 비트당 1.32 초.
   같은 루프를 늘리면 그건 홀드이고, 인계 문서가 측정해 둔 바로 그 실패(4.57 초)다.
   **똑같음이 네 번 반복되면서 불편해지는 것**이 이 훅이 노리는 감정이다.

🔴 뜻층은 보라 하나뿐이다(한 프레임에 둘까지 — 설계 §2).
"""
import bpy, sys, os, math

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stage, village, unison, motions

OUT = os.path.join(stage.OUT_ROOT, 'ep15s-1', 'hook5', 'frames')
FPS, DUR = 30, 6.61
NF = round(DUR * FPS) + 1
BEAT = 1.322                      # 6.61 / 5
TURN = math.radians(-34)          # ④ 에서 여섯이 함께 도는 각
os.makedirs(OUT, exist_ok=True)


def ease(u):
    u = max(0.0, min(1.0, u))
    return u * u * (3 - 2 * u)


bpy.ops.wm.read_factory_settings(use_empty=True)
village.build()
arms = unison.place()
marks = unison.personality_marks()
cam = stage.light_camera()

BASE_Z = [[b.location.z for b in row] for row in marks]
BASE_SZ = [[b.scale.z for b in row] for row in marks]

for fi in range(NF):
    t = fi / FPS

    # ── ① 표식이 뜬다. 여섯이 **동시에** 뜨는 것도 군무다 ──
    up = ease(t / 0.55)
    for row, zs, szs in zip(marks, BASE_Z, BASE_SZ):
        for b, z0, s0 in zip(row, zs, szs):
            b.scale.z = max(s0 * up, 1e-4)
            b.location.z = unison.MARK_Z + (z0 - unison.MARK_Z) * up   # 바닥에서 자란다
            b.hide_render = up <= 0.001

    # ── ②~⑤ 걷다 → 멈추다 → 돌다 → 다시 걷다 ──
    # 🔴 walk 의 위상은 **걷는 구간에서만** 흐른다. 멈춘 동안에도 t 를 그대로 주면
    #    다시 걸을 때 다리가 순간이동한다(이어 붙일 때 튀는 그 문제).
    if t < BEAT:                                  # ① 아직 안 걷는다
        phase, spec = 0.0, motions.stop(t)
    elif t < BEAT * 2:                            # ② 걷는다
        phase = t - BEAT
        spec = motions.walk(phase)
    elif t < BEAT * 4:                            # ③ 멈춤 ④ 돌기 — 숨만 쉰다
        phase = BEAT
        spec = motions.stop(t - BEAT * 2)
    else:                                         # ⑤ 다시 걷는다
        phase = BEAT + (t - BEAT * 4)
        spec = motions.walk(phase)

    turn = TURN * ease((t - BEAT * 3) / 0.42)     # ④ 여섯이 함께 돈다
    for arm in arms:
        stage.pose(arm, spec)                     # 여섯이 **같은 포즈** — 위상 오차 0
        arm.rotation_euler = (0, 0, math.radians(90) + turn)

    # 카메라 — 인트로가 착지한 자리에서 출발해 계속 흐른다(컷 없음)
    (sx, sy, sz), at = unison.HOOK_CAM
    u = t / DUR
    stage.aim(cam, (sx + 1.35 * u, sy - 0.25 * u, sz + 0.20 * u), at)

    bpy.context.view_layer.update()
    bpy.context.scene.render.filepath = os.path.join(OUT, '%04d.png' % fi)
    bpy.ops.render.render(write_still=True)

print('[hook5] %d frames -> %s' % (NF, OUT))
