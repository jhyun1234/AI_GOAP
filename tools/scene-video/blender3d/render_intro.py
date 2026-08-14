"""인트로 — 개발자가 위에서 마을이 생기는 것을 지켜본다. **45 편이 공유하므로 한 번만 렌더한다.**
   blender --background --factory-startup --python render_intro.py

  0.00~0.55  **위에서 본 빈 땅.** 초록 획(코드)이 땅을 훑는다
  0.55~1.50  집 셋이 차례로 솟는다
  1.50~2.20  나무와 밭·우물·덤불·모닥불이 놓인다
  2.20~2.90  주민 여섯이 **마을 곳곳에** 하나씩 놓인다
  2.60~4.30  계기층이 주민 머리 위에 뜬다 — **지켜보는 사람이 있다**
  2.90~4.85  여섯이 **제 자리로 걸어 들어온다**(카메라가 내려오는 동안에도 계속 걷는다)
  3.40~5.14  카메라가 개발자 시점(9.0m)에서 훅의 시작 자리로 내려앉는다
  4.85~5.14  걷기를 멈추고 **동시에 고개를 든다**

🔴 **끝나는 자리가 훅의 첫 프레임이다.** 여섯은 `unison.STANDS` 에, 방향은 `unison.FACE` 로
   끝나야 한다 — 딴 데서 끝내면 이음새에서 여섯이 순간이동한다. 그래서 「돌아다닌다」를
   **각자 다른 데서 제 자리로 걸어 들어오는 것**으로 지었다. 걸어 나가면 자리가 안 맞는다.

🔴 **렌즈는 45mm 하나다.** 설계 §6-3 의 개발자 시점은 70mm 지만, 샷 안에서 렌즈를 바꾸면
   그건 확대이고 그 규칙이 금지하는 것이다(본문1 이 같은 이유로 45mm 로 끝난다).
   그래서 개발자 시점은 **높이로만** 만든다 — 9.0m 에서 내려다본다.

🔴 유채색은 **초록 하나뿐**이다(코드 = 시스템이 한 일). 계기층 시안은 예산 밖이다
   (`check.mjs` 가 그렇게 센다). 주민 표식(보라)은 여기서 안 쓴다 — 그건 훅의 장치다.

🔴 길이는 **모든 편이 같다**(`stage.INTRO_SEC`). 앞 판은 `DUR = 5.143` 이 박혀 있었는데
   그건 ep15s-1 의 S0 길이였고, 다른 편은 3.54~4.60 초라 그림과 소리가 어긋났다.
   이제 **대본이 인트로에 맞춘다** — 회차의 S0 `pauseAfter` 를 이 길이가 되게 준다.
   실측: 말 2993ms 편은 쉼 1800 · 말 4049ms 편은 쉼 744.
"""
import bpy, sys, os, math

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stage, village, unison, motions, instrument

OUT = os.path.join(stage.OUT_ROOT, '_intro', 'frames')     # 회차 밖 — 45 편이 공유한다
FPS = 30
DUR = stage.INTRO_SEC
NF = round(DUR * FPS) + 1
os.makedirs(OUT, exist_ok=True)

# ── 비트 시각 ─────────────────────────────────────────
SWEEP_END = 0.55
WALK_FROM, WALK_TO = 2.90, 4.85        # 걸어 들어오는 구간
CAM_FROM = 3.40                        # 카메라가 내려앉기 시작한다(걷는 동안 겹친다)
LOOK_AT_T = 4.85                       # 동시에 고개를 드는 순간
MARK_FROM, MARK_TO = 2.60, 4.30        # 계기층이 떴다 꺼진다
assert WALK_TO <= LOOK_AT_T <= DUR, '고개 드는 순간이 인트로 밖이다'

# 걸어 들어오는 거리 — 🔴 **걸음이 만드는 거리다.** 손으로 적으면 발이 미끄러진다.
WALK_SPAN = WALK_TO - WALK_FROM
WALK_D = motions.WALK_SPEED * WALK_SPAN
BLEND = 0.26                           # 걷기로 들고 나는 이음새

# 개발자 시점 — 높이 9.0(설계 §6-3). 마을 한가운데를 내려다본다
# 🔴 9.0m·45mm 로는 **마을이 다 안 들어온다**(수평 화각 43.6° → 9m 에서 7.2m 폭인데
#    마을은 10m 가 넘는다). 설계표의 9.0 은 70mm 개발자 시점의 값이고 우리는 렌즈를 못
#    바꾸므로 **높이로 갚는다.**
# 🔴 겨냥을 **마을 한가운데가 아니라 여섯이 모이는 자리** 쪽으로 둔다. (0, 0.2) 로 겨눴더니
#    주민이 화면 왼쪽 아래로 몰려 잘렸다 — `STANDS` 가 x −1.31 줄에 y −0.5~−5.2 로 선다.
DEV_CAM = ((1.60, -6.40, 12.60), (-0.70, -2.00, 0.00))


def ease(u):
    u = max(0.0, min(1.0, u))
    return u * u * (3 - 2 * u)


def turn_to(a, b, k):
    """a 에서 b 로 **짧은 쪽으로** 돈다. 안 감아 주면 주민이 반대로 한 바퀴 돈다."""
    return a + ((b - a + math.pi) % (2 * math.pi) - math.pi) * k


bpy.ops.wm.read_factory_settings(use_empty=True)
v = village.build()
arms = unison.place()
cam = stage.light_camera()
# 🔴 훅과 **같은 빛**이어야 한다. 착지하는 자리(HOOK_CAM)로 키라이트를 맞춘다 — 두 샷이
#    다른 태양 방위를 쓰면 컷 없이 이어지는 그 순간 밝기가 튄다.
stage.key_from_view(*unison.HOOK_CAM)

# 코드 획 — 초록. 땅을 가로지르며 **지나간 자리에서** 마을이 솟는다
sweep_mat = stage.meaning_mat('green', strength=1.0)
bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0.02))
line = bpy.context.object
line.scale = (0.09, 30, 0.02)
line.data.materials.append(sweep_mat)

# 무엇이 언제 솟는가. 집 → 나무 → 살림 순서다(사람이 마을을 짓는 순서와 같다).
RISE = {}
for i, h in enumerate(v['houses']):
    RISE[h] = (0.55 + i * 0.32, 0.40)
for i, t_ in enumerate(v['trees']):
    RISE[t_] = (1.50 + i * 0.13, 0.32)
RISE[v['field']] = (1.80, 0.34)
RISE[v['well']] = (1.98, 0.32)
RISE[v['bush']] = (2.10, 0.28)
RISE[v['campfire']] = (2.20, 0.30)
# 🔴 `build()` 가 돌려주는 것은 **손잡이 하나뿐**이다 — 덤불은 덩이 셋 + 잔가지 셋,
#    우물은 기둥·도르래·두레박, 모닥불은 돌 일곱 + 장작 넷이 따로 서 있다. 손잡이만 키우면
#    나머지는 **첫 프레임부터 서 있고**, 그러면 「빈 땅에서 시작한다」가 거짓이 된다
#    (검사가 첫 프레임 밝은 화소로 잡는다). 남은 것을 가장 가까운 손잡이에 붙인다.
_keys = list(RISE)
for o in bpy.context.scene.objects:
    if o.type != 'MESH' or o in RISE or o is v['ground'] or o is line:
        continue
    if o.parent is not None:                 # 주민 메시는 아마추어 자식이다 — 아래에서 따로 다룬다
        continue
    near = min(_keys, key=lambda k: (k.location.x - o.location.x) ** 2
                                     + (k.location.y - o.location.y) ** 2)
    RISE[o] = RISE[near]

BASE_Z = {o: o.location.z for o in RISE}
BASE_SZ = {o: o.scale.z for o in RISE}

# 🔴 **인트로는 불을 안 켠다.** 앰버는 뜻층이고 인트로 예산은 초록 하나다.
#    앞 판이 켜고도 통과한 것은 그때 카메라가 일찍 착지해 **불이 프레임 밖으로 나간 뒤**
#    켜졌기 때문이다 — 개발자 시점은 늦게까지 높이 있어서 불이 화면 안에서 켜진다.
#    🔑 불은 훅이 켠다. 착지 시점에 불은 어차피 화면 밖이라 이음새에서 안 튄다.

# ── 계기층 — 주민 머리 위에 뜨는 작은 칸 여섯 ──────────
# 🔑 **이것이 「개발자가 지켜본다」를 말하는 유일한 장치다.** 사람을 그리지 않는다 —
#    시점(높이)과 계기층 둘이면 충분하고, 새 어휘를 안 늘린다.
marks = instrument.build(6, 1, pitch=1.0, cell=0.07)
# 🔴 표식을 **머리 바로 위에 두지 마라.** 위에서 내려다보는 시점이라 머리 위 물체가
#    주민을 통째로 덮는다(첫 판이 그랬다 — 사람이 안 보이고 계기만 보였다).
#    옆으로 비켜 세운다. 「가리키는 것」이지 「덮는 것」이 아니다.
MARK_OFF, MARK_H = (0.34, 0.34), 1.02
MARK_SCALE = 0.42                      # 🔑 주민만 한 표식은 「따라다니는 칸」이지 부호가 아니다

# ── 여섯이 걸어 들어오는 출발 자리 ────────────────────
# 🔴 **제 자리로 들어온다.** 훅이 `STANDS` 에서 시작하므로 여기서 나가면 이음새가 깨진다.
# 🔑 방향을 고르게 흩는다 — 여섯이 한쪽에서 몰려오면 그건 행렬이지 마을이 아니다.
COME = [math.radians(a) for a in (200, 140, 65, 20, 300, 250)]
START = [(x - WALK_D * math.cos(c), y - WALK_D * math.sin(c))
         for (x, y), c in zip(unison.STANDS, COME)]


def draw(fi):
    t = fi / FPS

    # ── ① 초록 획이 땅을 훑는다 ──
    line.location.x = -14 + 28 * ease(t / SWEEP_END)
    line.hide_render = t > SWEEP_END + 0.04

    # ── ②③ 획이 지나간 자리에서 마을이 솟는다 ──
    for o, (t0, span) in RISE.items():
        k = ease((t - t0) / span)
        o.scale.z = max(BASE_SZ[o] * k, 1e-4)
        o.location.z = BASE_Z[o] * k
        o.hide_render = k <= 0.001

    # ── ④⑤ 주민이 놓이고, 제 자리로 걸어 들어온다 ──
    walked = min(max(t - WALK_FROM, 0.0), WALK_SPAN)
    wk = motions.WALK_SPEED * walked / WALK_D if WALK_D else 1.0
    # 걷기 ↔ 멈춤 이음새. 들 때와 날 때 둘 다 부드럽게
    ww = min(ease((t - WALK_FROM) / BLEND), ease((WALK_TO - t) / BLEND))
    ww = max(0.0, min(1.0, ww))
    look = motions.look_up(max(0.0, t - LOOK_AT_T))
    spec = motions.blend(motions.blend(motions.stop(t), motions.walk(walked), ww), look,
                         ease((t - LOOK_AT_T) / 0.30))

    for i, arm in enumerate(arms):
        t0 = 2.20 + i * 0.11
        k = ease((t - t0) / 0.18)
        hidden = k <= 0.001
        arm.hide_render = hidden
        for ob in arm.children:
            ob.hide_render = hidden
        sx, sy = START[i]
        gx, gy = unison.STANDS[i]
        arm.location = (sx + (gx - sx) * wk, sy + (gy - sy) * wk, -0.55 * (1 - k))
        stage.pose(arm, spec)
        # 걷는 동안은 **가는 쪽**을 보고, 도착하면 훅이 보던 쪽으로 돌아선다
        arm.rotation_euler = (0, 0, turn_to(COME[i] + math.pi, unison.FACE,
                                            ease((t - WALK_TO) / 0.30)))

        # 계기층 — 주민 머리 위. 뜰 때 순서대로 뜨고, 착지 전에 꺼진다
        mk = min(ease((t - MARK_FROM - i * 0.06) / 0.22),
                 1.0 - ease((t - MARK_TO) / 0.35))
        cell = marks[i]
        instrument.show(cell, mk > 0.02 and not hidden)
        cell.scale = (mk * MARK_SCALE,) * 3
        cell.location = (arm.location.x + MARK_OFF[0], arm.location.y + MARK_OFF[1], MARK_H)

    # ── ⑥ 카메라가 개발자 시점에서 훅의 시작 자리로 내려앉는다 ──
    # 🔑 **걷는 동안 내려온다.** 착지를 걷기 뒤에 두면 그 1.4 초가 죽은 시간이 된다.
    u = ease((t - CAM_FROM) / (DUR - CAM_FROM))
    (sx, sy, sz), (ax, ay, az) = DEV_CAM
    (ex, ey, ez), (bx, by, bz) = unison.HOOK_CAM
    stage.aim(cam,
              tuple(a + (b - a) * u for a, b in ((sx, ex), (sy, ey), (sz, ez))),
              tuple(a + (b - a) * u for a, b in ((ax, bx), (ay, by), (az, bz))))


print('[intro] %.3f초 · %d프레임 · 걸어 들어오는 거리 %.2fm(%.2f초)'
      % (DUR, NF, WALK_D, WALK_SPAN))
stage.bake(OUT, NF, FPS, draw, 'intro')
