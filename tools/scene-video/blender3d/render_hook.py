"""ep15s-1 훅 — 8 비트 · 9.58 초. **설계의 시금석이다.**
   blender --background --factory-startup --python render_hook.py

  0.00~1.20  ① 마을 어귀에 한 줄로 선 여섯 머리 위에 **서로 다른** 성격 표식이 뜬다
  1.20~3.59  ②③ 동시에 걷기 시작해 **줄줄이 마을 안으로 들어간다**
  3.59~4.79  ④ 동시에 멈춰 서서 같은 쪽으로 몸을 돌린다
  4.79~8.38  ⑤⑥⑦ 동시에 다시 걸어 **집 사이까지 들어간다**
  8.38~9.58  ⑧ 동시에 멈춘다 — 마지막 대사(「멈추는 자리도 같았고요」)가 여기 얹힌다

🔴 판정 둘을 받아서 여기까지 왔다.
   5비트 판 — 「제자리에서 가만히 걷는 모습은 별로 인상 깊지 않았다」. `arm.location` 을
   한 번도 안 건드려서 여섯이 다리만 젓고 아무 데도 안 갔다. `unison.march` 가 그 답이다.
   8비트 판 — 「마을 안쪽으로 걷는 동선」. 그래서 여섯을 **한 줄(세로)** 로 돌려세웠다.
   가로 대열은 폭이 5.75 m 라 이 마을에 물리적으로 못 들어간다(unison.STANDS 주석).

🔑 길이는 SH 내레이션에서 나온다. 한 줄(「가는 곳도, 멈추는 자리도 같았고요.」)을 더해
   6.61 → 9.58 초가 됐고, 여덟 비트면 비트당 1.198 초다(홀드 상한 1.5 초 안).

🔴 뜻층은 **보라(성격 표식)와 앰버(모닥불) 둘**이다 — 설계 §2 의 상한이다.
   여기에 색을 하나 더 넣으면 그때 규약이 깨진다. 시안은 계기층이라 예산 밖이다.
"""
import bpy, sys, os, json, math

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stage, village, unison

EP = 'ep15s-1'
OUT = os.path.join(stage.OUT_ROOT, EP, 'hook', 'frames')
TIMED = os.path.join(os.path.dirname(stage.SV_ROOT), 'scene-video', 'episodes', EP,
                     'build', 'timed.json')
FPS = 30
os.makedirs(OUT, exist_ok=True)


def shot_seconds(shot_id):
    """🔑 길이를 손으로 안 적는다. 대본이 바뀌면 훅 길이도 같이 바뀌어야 한다 —
    엔진 타임라인이 이 값으로 돌기 때문에 어긋나면 그림과 소리가 통째로 밀린다."""
    d = json.load(open(TIMED, encoding='utf-8'))
    s = next(s for s in d['shots'] if s['id'] == shot_id)
    return (sum(l['dur'] + l.get('pause', 0) for l in s['lines']) + 350) / 1000.0


DUR = shot_seconds('SH')
NF = round(DUR * FPS) + 1
BEAT = DUR / len(unison.HOOK_BEATS)
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초 — 비트를 늘려라' % BEAT


def ease(u):
    u = max(0.0, min(1.0, u))
    return u * u * (3 - 2 * u)


bpy.ops.wm.read_factory_settings(use_empty=True)
v = village.build()
arms = unison.place()
marks = unison.personality_marks(arms)     # 🔑 주민에게 묶는다 — 걸어가면 같이 간다
# 🔥 모닥불은 인트로가 지폈다 — 컷 없이 이어지므로 여기서도 타고 있어야 한다.
# 🔴 앰버가 뜻층 예산 하나를 쓴다. 이 샷은 보라(성격 표식)와 둘이라 규약 상한이다 —
#    여기에 색을 하나 더 넣으면 그때 설계 §2 가 깨진다.
fire = village.flame(village.SPOTS['fire'])
cam = stage.light_camera()
stage.key_from_view(*unison.HOOK_CAM)      # 🔴 안 부르면 여섯이 통째로 역광이 된다

HOME = [tuple(a.location) for a in arms]
BASE_Z = [[b.location.z for b in row] for row in marks]
BASE_SZ = [[b.scale.z for b in row] for row in marks]

# 🔴 궤적이 소품을 뚫으면 그 프레임 하나로 3D 가 거짓말이 된다. **미리 잰다.**
#    이 검사가 가로 대열을 접고 한 줄로 돌아서게 만들었다 — 손으로 지킬 규칙이 아니다.
# 🔑 장애물은 **주민 키(0.95)를 기준으로** 고른다.
#    · 너무 낮은 것은 밟고 지나간다 — 밭 0.05, 이랑 0.09
#    · 밑동이 머리 위에 있는 것은 **밑으로 지나간다** — 지붕 처마가 1.05 에서 시작한다.
#      높이만 보고 걸렀더니 처마 밑을 스치는 길을 「집을 뚫는다」로 잡았다
BODY_R, HEAD = 0.22, 0.90
OBSTACLES = [(o.location.x, o.location.y, max(o.dimensions.x, o.dimensions.y) / 2, o.name)
             for o in bpy.context.scene.objects
             if o.type == 'MESH' and o is not v['ground'] and not o.parent
             and o.dimensions.z >= 0.15 and o.location.z - o.dimensions.z / 2 < HEAD]
_worst = min(
    (((hx + unison.march(DUR * k / 400, BEAT)[0] - ox) ** 2
      + (hy + unison.march(DUR * k / 400, BEAT)[1] - oy) ** 2) ** 0.5 - r - BODY_R, nm)
    for k in range(401) for hx, hy, _hz in HOME for ox, oy, r, nm in OBSTACLES)
assert _worst[0] > 0, '주민 궤적이 %s 를 %.2f m 뚫는다' % (_worst[1], -_worst[0])
print('[hook] %d비트 · %.2f초 · 이동 %.2fm · 소품 최소 여유 %.2fm (%s)'
      % (len(unison.HOOK_BEATS), DUR,
         (lambda p: (p[0] ** 2 + p[1] ** 2) ** 0.5)(unison.march(DUR, BEAT)),
         _worst[0], _worst[1]))

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
        arm.rotation_euler = (0, 0, unison.FACE + heading)

    # 카메라 — 인트로가 착지한 자리에서 출발해 계속 흐른다(컷 없음).
    # 🔑 **따라가되 같이 걷지 않는다.** 겨냥점은 여섯을 그대로 따라가고(팬), 자리는 그 3할만
    #    따라간다. 완전히 따라붙이면 화면에서 여섯이 안 움직여서 **또 제자리걸음이 된다** —
    #    걷는 것이 보이는 건 마을이 흘러 지나가기 때문이다.
    (sx, sy, sz), (ax, ay, az) = unison.HOOK_CAM
    u = t / DUR
    # 🔑 u 항은 **뒤로 빠지는 것**이다(시선 반대 방향). 여섯이 마을 안으로 들어갈수록
    #    집·나무가 사이로 끼어들어 여섯을 셀 수 없게 된다 — 그만큼 뒤로 물러나 자리를 준다.
    stage.aim(cam, (sx + 0.35 * x + 1.10 * u, sy + 0.35 * y - 0.45 * u, sz + 0.28 * u),
              (ax + x, ay + y, az))

    village.flicker(fire, t)

    bpy.context.view_layer.update()
    bpy.context.scene.render.filepath = os.path.join(OUT, '%04d.png' % fi)
    bpy.ops.render.render(write_still=True)

print('[hook] %d frames -> %s' % (NF, OUT))
