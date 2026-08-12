"""ep15s-1 본문3 — 해결. 개발자 시점에서 시작해 **마을로 컷 ↓** 하고 끝난다.
   blender --background --factory-startup --python render_body3.py

  0.00~1.50  ① 사람이 채우던 것이 가득 — 백여든 칸이 훑고 뜬다
  1.50~3.00  ② 그중 서른여섯에 보라가 든다 — **사람이 손으로 적은 것**
  3.00~4.49  ③ 나머지 백마흔넷이 접혀 사라지고, 서른여섯이 커지며 자리를 다시 잡는다
  4.49~5.99  ④ 초록 획이 훑고 지나가며 **코드가 나머지를 채운다**
  5.99~7.49  ⑤ 마을로 **컷 ↓**
  7.49~8.99  ⑥ 군무가 깨진다 — 여섯이 처음으로 서로 다른 동작을 한다
  8.99~10.49 ⑦ 여섯이 서로 다른 곳으로 걸어간다

🔴 이 편의 컷 **둘째이자 마지막**이 ⑤ 다(설계 §4-1). 본문2 에서 여기까지는 컷이 없다 —
   개발자 시점 카메라를 `instrument.DEV_CAM` 으로 공유하는 이유다.

🔴 색 순서가 곧 이야기다. **보라(사람이 적은 것) → 초록(코드가 채운 것).**
   두 색을 한 프레임에 겹치지 않게 한다 — 겹치면 앰버(모닥불)와 합쳐 셋이 되고 설계 §2 가
   깨진다. 그래서 ④ 에서 초록이 들어올 때 서른여섯은 **시안으로 돌아간다**(시스템이 됐다).

🔑 ⑥⑦ 이 이 편의 결말이다. 훅에서 하나였던 움직임이 여섯으로 갈라진다 —
   본문1 의 「잠깐 흩어짐」은 **자리만**이었고, 여기는 **동작이** 갈라진다.
"""
import bpy, sys, os, math

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stage, village, unison, motions, instrument

EP, SHOT = 'ep15s-1', 'S3'
OUT = os.path.join(stage.OUT_ROOT, EP, 'body3', 'frames')
FPS = 30
os.makedirs(OUT, exist_ok=True)

DUR = stage.shot_seconds(EP, SHOT)
NF = round(DUR * FPS) + 1
NB = 7
BEAT = DUR / NB
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT
CUT_AT = 4.0 * BEAT                      # ⑤ 마을로 내려가는 컷

# 손으로 채우면 백여든 칸(성격 여섯 × 일 서른), 실제로 적은 것은 서른여섯 줄.
# 🔑 수를 여기서 만들지 않는다 — 대본이 말하는 수다("손으로 백여든 칸", "서른여섯 줄만").
FULL_COLS, FULL_ROWS = 30, 6
KEEP_COLS, KEEP_ROWS = 6, 6
FULL_PITCH, FULL_CELL = 0.28, 0.155
KEEP_PITCH, KEEP_CELL = 0.62, 0.40
N_FULL, N_KEEP = FULL_COLS * FULL_ROWS, KEEP_COLS * KEEP_ROWS
assert N_FULL == 180 and N_KEEP == 36, (N_FULL, N_KEEP)


def ease(u):
    u = max(0.0, min(1.0, u))
    return u * u * (3 - 2 * u)


bpy.ops.wm.read_factory_settings(use_empty=True)
village.build()
arms = unison.place()
fire = village.flame(village.SPOTS['fire'])

# 본문1 이 남긴 자리에서 시작한다(⑤ 가 컷이라 그림은 안 이어지지만, 여섯이 순간이동해
# 있으면 같은 마을로 안 읽힌다 — 마을 샷끼리는 자리를 이어 둔다).
HOOK_DUR = stage.shot_seconds(EP, 'SH')
HX, HY, HEADING, _ph = unison.march(HOOK_DUR, HOOK_DUR / len(unison.HOOK_BEATS))
HOME = [(x + HX, y + HY) for x, y in unison.STANDS]

grid = instrument.build(FULL_COLS, FULL_ROWS, pitch=FULL_PITCH, cell=FULL_CELL)
POS_FULL = instrument.cells(FULL_COLS, FULL_ROWS, FULL_PITCH)
POS_KEEP = instrument.cells(KEEP_COLS, KEEP_ROWS, KEEP_PITCH)
cyan = stage.instrument_mat(strength=1.0)
violet = stage.meaning_mat('violet', strength=1.0)
green = stage.meaning_mat('green', strength=1.0)

# ④ 의 초록 획 — 인트로가 마을을 훑던 그 도형을 표 위에서 다시 쓴다
sweep = stage.meaning_mat('green', strength=1.0)
bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, instrument.Z))
line = bpy.context.object
line.scale = (0.07, 4.0, 0.07)
line.data.materials.append(sweep)

cam = stage.light_camera()
cam.data.lens = instrument.DEV_LENS
stage.key_from_view(*instrument.DEV_CAM)

# ⑦ 여섯이 흩어져 가는 쪽. 🔴 무리 중심에서 **바깥으로** 뻗는다 — 안쪽으로 보내면
#    서로 겹쳐서 「갈라졌다」가 아니라 「뭉쳤다」가 된다.
CX = sum(x for x, _ in HOME) / 6
CY = sum(y for _, y in HOME) / 6
AWAY = [math.atan2(y - CY, x - CX) + (i - 2.5) * 0.16 for i, (x, y) in enumerate(HOME)]

VCAM = ((4.60, -9.60, 3.30), (-1.80, 0.10, 0.80))     # ⑤ 컷이 내려앉는 자리
# 🔴 낮게 붙이면 여섯이 겹쳐서 「갈라졌다」가 안 보인다. 갈라짐은 **간격**이 보여야 한다.

print('[body3] %d비트 · %.2f초 · %d칸 → %d칸 · 컷 %.2f초' % (NB, DUR, N_FULL, N_KEEP, CUT_AT))

for fi in range(NF):
    t = fi / FPS
    b = t / BEAT

    # ── ①~④ 계기층 ────────────────────────────────
    fold = ease((b - 2.0) / 0.85)                 # ③ 접힘
    code = ease((b - 3.0) / 0.30)                 # ④ 코드가 채우기 시작
    line_u = (b - 3.0) / 0.85
    for i, cell in enumerate(grid):
        keep = i < N_KEEP
        up = ease((b - i / len(grid) * 0.80) / 0.35)      # ① 훑으며 뜬다
        if keep:
            fx, fy = POS_FULL[i]
            kx, ky = POS_KEEP[i]
            cell.location = (fx + (kx - fx) * fold, fy + (ky - fy) * fold, instrument.Z)
            s = up * (FULL_CELL + (KEEP_CELL - FULL_CELL) * fold) / FULL_CELL
            # ② 보라(사람이 적은 것) → ④ 시안(시스템이 됐다). 🔴 초록과 겹치지 않게 되돌린다
            # 🔴 보라는 초록이 들어오는 **그 순간** 꺼진다. 겹치면 보라+초록+앰버 셋이 된다.
            instrument.paint(cell, violet if 1.0 <= b < 3.0 else cyan)
            instrument.show(cell, up > 0.001)
        else:
            # 접혀 사라졌다가, 초록 획이 지나간 자리에서 **코드가 채운다**
            fx, fy = POS_FULL[i]
            gone = fold * (1 - ease((line_u - (fx + 4.0) / 8.0) / 0.12))
            cell.location = (fx, fy, instrument.Z)
            s = up * max(0.0, 1 - gone)
            instrument.paint(cell, green if code > 0.5 and gone < 0.5 else cyan)
            instrument.show(cell, up > 0.001 and s > 0.02)
        cell.scale = (s, s, s)
    line.location.x = -4.6 + 9.2 * min(max(line_u, 0.0), 1.0)
    line.hide_render = not (0.0 < line_u < 1.05)

    # ── ⑥⑦ 마을 — 군무가 깨진다 ──────────────────
    k = 1.0 if b >= 5.0 else 0.0                  # ⑥ 여섯이 서로 다른 동작
    # ⑦ 서로 다른 쪽으로 벌어진다. 🔴 BREAK 에서 걷는 사람은 하나뿐이라 그것만으로는
    #    「궤적이 갈라진다」가 안 보인다 — **여섯 다** 제 쪽으로 벌어져야 간격이 생긴다.
    drift = 1.15 * ease((b - 6.0) / 1.0)
    for i, (arm, (x, y)) in enumerate(zip(arms, HOME)):
        name = unison.motion_at(t, i, k)
        stage.pose(arm, motions.MOTIONS[name](t))
        arm.location = (x + drift * math.cos(AWAY[i]), y + drift * math.sin(AWAY[i]), 0)
        arm.rotation_euler = (0, 0, AWAY[i] + math.pi if drift > 0.02
                              else unison.FACE + HEADING)

    village.flicker(fire, t)

    # ── 카메라 — ⑤ 에서 **한 프레임에** 마을로 떨어진다(컷) ──
    if t < CUT_AT:
        instrument.dev_view(cam, t / CUT_AT)
        for o in grid:
            o.hide_render = o.hide_render          # 그대로
    else:
        w = (t - CUT_AT) / (DUR - CUT_AT)
        (vx, vy, vz), at = VCAM
        stage.aim(cam, (vx - 0.55 * w, vy + 0.35 * w, vz + 0.20 * w), at)
        for cell in grid:                          # 계기층은 컷과 함께 사라진다
            instrument.show(cell, False)
        line.hide_render = True

    if abs(t - CUT_AT) < 1.0 / FPS:                # 컷 순간 빛도 같이 옮긴다
        stage.key_from_view(*VCAM)

    bpy.context.view_layer.update()
    bpy.context.scene.render.filepath = os.path.join(OUT, '%04d.png' % fi)
    bpy.ops.render.render(write_still=True)

print('[body3] %d frames -> %s' % (NF, OUT))
