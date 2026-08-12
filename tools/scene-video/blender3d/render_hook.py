"""ep15s-1 훅 — 8 비트 · 6.61 초. **설계의 시금석이다.**
   blender --background --factory-startup --python render_hook.py

  0.00~0.83  ① 여섯 머리 위에 **서로 다른** 성격 표식이 뜬다
  0.83~2.48  ②③ 동시에 걷기 시작해 **나란히 마을을 가로지른다**
  2.48~3.30  ④ 동시에 멈춘다
  3.30~4.13  ⑤ 동시에 같은 쪽으로 몸을 돌린다
  4.13~5.78  ⑥⑦ 동시에 다시 걸어 **카메라 쪽으로 다가온다**
  5.78~6.61  ⑧ 동시에 멈춘다

🔴 5비트 판의 판정: **「제자리에서 가만히 걷는 모습은 별로 인상 깊지 않았다」.**
   원인은 비트 수가 아니라 **여섯이 아무 데도 안 갔다는 것**이었다 — 다리는 저었는데
   `arm.location` 을 한 번도 안 건드려서 발밑 좌표가 첫 프레임 그대로였다.
   그래서 이 판은 `unison.march` 가 자리를 밀고, 카메라는 겨냥만 따라간다.
   🔑 비트를 여덟로 늘린 것은 그 다음이다 — 걷는 비트를 **둘씩 붙여야** 거리가 눈에 든다.

🔴 길이는 6.61 초에서 못 늘린다. SH 샷 내레이션이 6.606 초이고 엔진 타임라인이 그 값으로
   돈다(`build/timed.json`). 비트를 늘리면 길어지는 것이 아니라 **비트가 짧아진다** —
   여덟 × 0.826 초. 홀드 상한 1.5 초 안이다.

🔴 뜻층은 보라 하나뿐이다(한 프레임에 둘까지 — 설계 §2).
"""
import bpy, sys, os, math

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stage, village, unison

OUT = os.path.join(stage.OUT_ROOT, 'ep15s-1', 'hook8', 'frames')
FPS, DUR = 30, 6.606               # SH 내레이션 길이. 여기서 늘리면 소리와 어긋난다
NF = round(DUR * FPS) + 1
BEAT = DUR / len(unison.HOOK_BEATS)
os.makedirs(OUT, exist_ok=True)


def ease(u):
    u = max(0.0, min(1.0, u))
    return u * u * (3 - 2 * u)


bpy.ops.wm.read_factory_settings(use_empty=True)
village.build()
arms = unison.place()
marks = unison.personality_marks(arms)     # 🔑 주민에게 묶는다 — 걸어가면 같이 간다
cam = stage.light_camera()

HOME = [tuple(a.location) for a in arms]
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

    # ── ②~⑧ 걷는다 → 멈춘다 → 돈다 → 다시 걷는다 → 멈춘다 ──
    x, y, heading, _phase = unison.march(t, BEAT)
    spec = unison.hook_pose(t, BEAT)
    for arm, (hx, hy, hz) in zip(arms, HOME):
        stage.pose(arm, spec)                          # 여섯이 **같은 포즈** — 위상 오차 0
        arm.location = (hx + x, hy + y, hz)            # 여섯이 **같은 만큼** 나아간다
        arm.rotation_euler = (0, 0, math.radians(90) + heading)

    # 카메라 — 인트로가 착지한 자리에서 출발해 계속 흐른다(컷 없음).
    # 🔑 **따라가되 같이 걷지 않는다.** 겨냥점은 여섯을 그대로 따라가고(팬), 자리는 그 절반만
    #    따라간다. 완전히 따라붙이면 화면에서 여섯이 안 움직여서 **또 제자리걸음이 된다** —
    #    걷는 것이 보이는 건 배경이 흐르기 때문이다.
    (sx, sy, sz), (ax, ay, az) = unison.HOOK_CAM
    u = t / DUR
    stage.aim(cam, (sx + 0.35 * x + 0.50 * u, sy + 0.35 * y - 0.15 * u, sz + 0.18 * u),
              (ax + x, ay + y, az))

    bpy.context.view_layer.update()
    bpy.context.scene.render.filepath = os.path.join(OUT, '%04d.png' % fi)
    bpy.ops.render.render(write_still=True)

print('[hook8] %d frames -> %s' % (NF, OUT))
