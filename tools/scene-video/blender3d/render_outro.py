"""ep15s-1 아웃트로 — 다음 편 예고. 본문3 에서 **컷 없이** 이어진다.
   blender --background --factory-startup --python render_outro.py

  0.00~1.40  ① 여섯이 불 쪽으로 팔을 뻗는다 · 한기가 한 번 다가왔다 선다
  1.40~2.80  ② 한기가 **더 가까이** 온다 — 여섯은 그대로 뻗고 있다
  2.80~4.20  ③ 한기가 코앞까지 온다 — 여섯이 **그 자리에서 굳는다**
  4.20~7.20  한기가 굳은 여섯을 덮고 지나간다(뒤 3 초는 아웃트로 카드가 덮는다)

🔴 **0.5 초 루프는 안 쓴다.** 설계 §4-1 의 `loopSec 0.5` 는 2D 도형판의 장치였다.
   26m 짜리 마을에서 반 초마다 한기 띠가 지나가면 그건 반복이 아니라 **점멸**이고,
   인계 문서가 되돌린 목록에 「반전 플래시 — 눈이 아프다」가 이미 있다.
   🔑 뜻(「위험이 되풀이해 다가오는데 저쪽만 본다」)은 **세 번 다가오되 매번 더 가까이**
   오는 것으로 지킨다. 되풀이는 남고 점멸은 없다.

🔴 늑대는 화면에 안 만든다(게임에서 사라진 종족이다). 위험은 **한기**로만 말한다.
🔴 뜻층은 한기 + 앰버(모닥불) 둘 — 상한이다.
🔑 `reach` 는 원래 **굳음의 준비**로 만든 동작이고(motions.py), `freeze` 가 그 포즈를
   그대로 굳힌다. 이 샷이 그 둘이 처음으로 제 뜻대로 쓰이는 자리다.
"""
import bpy, sys, os, math

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stage, village, unison, motions

EP, SHOT = 'ep15s-1', 'SO'
OUT = os.path.join(stage.OUT_ROOT, EP, 'outro', 'frames')
FPS = 30
os.makedirs(OUT, exist_ok=True)

DUR = stage.shot_seconds(EP, SHOT)
NF = round(DUR * FPS) + 1
PULSES, PULSE_END = 3, 4.20        # 카드가 덮기 전까지가 보이는 시간이다
STOPS = (4.6, 2.4, 0.5)            # 한기가 멈춰 서는 자리 — 매번 여섯에 가까워진다
CREEP_TO = -3.4                    # 그 뒤로도 계속 온다. 위험은 멈추지 않는다
FREEZE_AT = 3.55


def ease(u):
    u = max(0.0, min(1.0, u))
    return u * u * (3 - 2 * u)


bpy.ops.wm.read_factory_settings(use_empty=True)
village.build()
arms = unison.place()
fire = village.flame(village.SPOTS['fire'])
front = village.cold_front(width=1.30, span=22.0)

# 본문3 이 흩어 놓은 자리에서 이어받는다 — 컷이 아니다.
HOOK_DUR = stage.shot_seconds(EP, 'SH')
HX, HY, _hd, _ph = unison.march(HOOK_DUR, HOOK_DUR / len(unison.HOOK_BEATS))
HOME = [(x + HX, y + HY) for x, y in unison.STANDS]
CX = sum(x for x, _ in HOME) / 6
CY = sum(y for _, y in HOME) / 6
AWAY = [math.atan2(y - CY, x - CX) + (i - 2.5) * 0.16 for i, (x, y) in enumerate(HOME)]
SPREAD = [(x + 1.15 * math.cos(a), y + 1.15 * math.sin(a)) for (x, y), a in zip(HOME, AWAY)]

FIRE = village.SPOTS['fire']
CAM = ((2.90, -7.60, 2.15), (-1.25, 0.45, 0.78))

cam = stage.light_camera()
stage.key_from_view(*CAM)

print('[outro] %.2f초 · 한기 %d번 · 굳음 %.2f초' % (DUR, PULSES, FREEZE_AT))

for fi in range(NF):
    t = fi / FPS
    u = t / DUR

    # ── 여섯은 불 쪽을 보고 팔을 뻗은 채 **끝까지 안 움직인다** ──
    frozen = t >= FREEZE_AT
    spec = motions.freeze(t) if frozen else motions.reach(t)
    for (x, y), arm in zip(SPREAD, arms):
        stage.pose(arm, spec)
        arm.location = (x, y, 0)
        # 🔴 여섯이 **불을 본다**. 위험은 등 뒤에 있고, 그게 이 예고의 전부다.
        arm.rotation_euler = (0, 0, math.atan2(y - FIRE.y, x - FIRE.x))

    # ── 한기가 세 번, 매번 더 가까이 ──
    #    🔴 물러나지 않는다. 왔다 갔다 하면 그건 파도이지 다가오는 위험이 아니다.
    #    🔴 화면 **옆**으로 지나가게 두지 마라 — 첫 판이 그래서 구석에 파란 조각만 보였다.
    #       마을 뒤(+Y)에서 여섯 쪽으로 내려와야 「다가온다」가 된다.
    # 🔴 세 번째 뒤에도 **멈추지 않는다.** 첫 판은 여기서 화면이 3.1 초 굳어 정적 상한(3.0)을
    #    넘겼는데, 고칠 자리는 카메라가 아니라 이야기였다 — 위험이 멈출 이유가 없다.
    #    한기가 굳어 버린 여섯을 그대로 덮고 지나가는 것이 이 예고가 하려던 말이다.
    front.hide_render = False
    front.rotation_euler = (0, 0, math.pi / 2)
    if t <= PULSE_END:
        p = t / (PULSE_END / PULSES)
        step = min(int(p), PULSES - 1)
        y_from = STOPS[step - 1] if step else 8.0
        fy = y_from + (STOPS[step] - y_from) * ease(p - step)
    else:
        fy = STOPS[-1] + (CREEP_TO - STOPS[-1]) * (t - PULSE_END) / (DUR - PULSE_END)
    front.location = (CX, fy, 0.02)

    # 🔴 불은 **안 끈다.** 첫 판은 굳는 순간 불까지 껐는데, 화면에서 앰버가 사라지니
    #    「주민이 굳었다」가 아니라 「장면이 끝났다」로 읽혔다. 굳는 것은 사람뿐이고,
    #    불이 계속 흔들려야 「세상은 도는데 이 사람들만 멈췄다」가 된다.
    village.flicker(fire, t)

    (sx, sy, sz), at = CAM
    stage.aim(cam, (sx - 0.35 * u, sy - 0.30 * u, sz + 0.10 * u), at)

    bpy.context.view_layer.update()
    bpy.context.scene.render.filepath = os.path.join(OUT, '%04d.png' % fi)
    bpy.ops.render.render(write_still=True)

print('[outro] %d frames -> %s' % (NF, OUT))
