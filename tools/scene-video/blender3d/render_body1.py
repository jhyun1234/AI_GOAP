"""ep15s-1 본문1 — 증상 확정. 훅에서 **컷 없이** 이어진다.
   blender --background --factory-startup --python render_body1.py

  0.00~1.42  ① 카메라가 조망으로 오르며 여섯이 동시에 밭일을 시작한다
  1.42~2.83  ② 동시에 우물 긷기로 갈아탄다
  2.83~4.25  ③ 동시에 불 쬐기로 갈아탄다
  4.25~5.67  ④ 한기가 마을을 스친다 — 여섯이 **서로 다른 쪽으로 걸어** 흩어진다
  5.67~7.09  ⑤ **한 자리로 도로 걸어와** 겹친다

🔴 **설계 §4-1 을 글자 그대로는 못 지었다.** 비트 시트는 「①밭에 같이 ②우물에 같이
   ③불에 같이」인데, 세 자리를 실제로 도는 데는 **12.9 초**가 든다(밭→우물 5.81m,
   우물→불 3.49m, 걸음 0.72m/s). 대본은 7.09 초다 — 1.8 배 모자란다.
   빠르게 걷게 하면 발이 미끄러지고, 그건 5비트 훅이 반려된 그 문제의 반대편이다.
   🔑 그래서 **자리가 아니라 박자로** 짓는다. 대본이 말하는 것도 장소가 아니라 때다 —
   「밭에 가는 **때**도 불 쬐는 **때**도 같았고요」. 여섯이 같은 순간에 같은 일로
   갈아타는 것이 그 문장이다. 마을(밭·우물·모닥불)은 조망 화면 안에 다 들어 있다.

🔴 ④⑤ 는 **자리만** 흩어진다. 동작까지 갈라 놓으면 본문3 의 「군무가 깨진다」와 구별이
   안 되고, 그쪽이 이 편의 결말이다.
   🔑 그래서 여섯은 흩어질 때도 **같은 걷기**를 한다(위상 오차 0). 다른 것은 **방향**뿐이다 —
   본문3 은 *동작 종류*가 갈라지고 여기는 *가는 쪽*만 갈라진다. 둘은 다른 사건이다.
🔴 흩어졌다 오는 데 걸리는 시간을 **눈대중으로 정하지 마라.** 걸음이 만드는 거리에서
   역산한다(`SCATTER_SPAN`) — 빠르면 발이 미끄러지고 느리면 끌린다. 앞 판은 자리만
   0.55 초에 밀어서 여섯이 **미끄러져** 흩어졌다.

🔴 뜻층은 앰버(모닥불) + 한기(겨울) 둘 — ④⑤ 에서 상한이다.
🔑 그래서 **성격 표식이 여기서 꺼진다.** 표식은 훅의 장치이고(「표식은 다른데 움직임이
   하나다」) 그 일을 마쳤다. 켜 둔 채로 한기를 부르면 보라+앰버+한기 셋이 돼서 설계 §2 가
   깨진다 — 첫 판이 실제로 그랬다. 컷이 아니므로 사라지는 것도 **보이게** 꺼야 한다.
"""
import bpy, sys, os, math

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stage, village, unison, motions

EP, SHOT = 'ep15s-1', 'S1'
OUT = os.path.join(stage.OUT_ROOT, EP, 'body1', 'frames')
FPS = 30
os.makedirs(OUT, exist_ok=True)

DUR = stage.shot_seconds(EP, SHOT)
NF = round(DUR * FPS) + 1
JOBS = ('farm', 'draw', 'warm', 'warm', 'warm')      # 비트 다섯의 동작
BEAT = DUR / len(JOBS)
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

COLD_AT = 3.00                      # 한기가 닿는 시각 — 여기서 흩어져 **걸어** 나간다
HOLD = 0.55                         # 흩어진 채 서 있는 시간
# 🔴 흩어지는 시간은 **거리 ÷ 걸음 속도**다. 여기를 손으로 적으면 발이 미끄러지거나 끌린다.
SCATTER_D = sum(math.hypot(dx, dy) for dx, dy in unison.SCATTER[:6]) / 6
SCATTER_SPAN = SCATTER_D / motions.WALK_SPEED
BACK_AT = COLD_AT + SCATTER_SPAN + HOLD
COLD_SPAN = SCATTER_SPAN * 2 + HOLD              # 한기는 흩어졌다 오는 동안 내내 지나간다
assert BACK_AT + SCATTER_SPAN <= DUR, (
    '걸어 나갔다 올 시간이 모자란다: %.2f 초 필요, %.2f 초뿐' % (BACK_AT + SCATTER_SPAN, DUR))

# 흩어져 걸어가는 쪽 / 돌아오는 쪽. 🔴 `stage.rigged` 규약: 회전 θ 의 정면은 (-cos θ, -sin θ).
OUT_FACE = [math.atan2(-dy, -dx) for dx, dy in unison.SCATTER[:6]]
BACK_FACE = [math.atan2(dy, dx) for dx, dy in unison.SCATTER[:6]]
BLEND = 0.26                        # 걷기로 들고 나는 이음새. 0 이면 다리가 차렷으로 튄다

# ── 한기의 세기 ────────────────────────────────────────
# 🔑 값 하나가 불·눈·사람 셋을 동시에 움직인다. 셋을 따로 적으면 반드시 어긋나고,
#    어긋나면 「바람이 분다」가 아니라 「세 가지가 각자 흔들린다」가 된다.
GUST = 0.62                         # 한기 최대일 때 바람 세기(불이 눕는 각·눈이 밀리는 정도)
SNOW_BASE = 0.10                    # 한기 전에도 눈은 내린다 — 겨울은 사건이 아니라 계절이다


def ease(u):
    u = max(0.0, min(1.0, u))
    return u * u * (3 - 2 * u)


def leg(t, t0):
    """t0 에 시작한 걷기 다리가 **지금까지 걸은 시간.** 다리 밖에서는 안 흐른다."""
    return min(max(t - t0, 0.0), SCATTER_SPAN)


def leg_mix(t, t0):
    """그 다리의 걷기 섞임값 0~1 — 앞뒤 이음새를 다 포함한다."""
    x = t - t0
    if x <= 0 or x >= SCATTER_SPAN:
        return 0.0
    return min(ease(x / BLEND), ease((SCATTER_SPAN - x) / BLEND))


def turn_to(a, b, k):
    """a 에서 b 로 **짧은 쪽으로** 돈다. 안 감아 주면 여섯이 반대로 한 바퀴 돈다."""
    return a + ((b - a + math.pi) % (2 * math.pi) - math.pi) * k


bpy.ops.wm.read_factory_settings(use_empty=True)
village.build()
arms = unison.place()
marks = unison.personality_marks(arms)
fire = village.flame(village.SPOTS['fire'])
flakes = village.snow()

# 🔑 **훅이 끝난 자리에서 시작한다.** 컷이 아니므로 여섯의 자리·방향·카메라가 이어져야 한다.
#    값을 손으로 옮겨 적지 않는다 — 훅과 같은 함수를 같은 인자로 부른다.
HOOK_DUR = stage.shot_seconds(EP, 'SH')
HX, HY, HEADING, _ph = unison.march(HOOK_DUR, HOOK_DUR / len(unison.HOOK_BEATS))
HOME = [(x + HX, y + HY) for x, y in unison.STANDS]
(hsx, hsy, hsz), (hax, hay, haz) = unison.HOOK_CAM
CAM0 = ((hsx + 0.35 * HX + 1.10, hsy + 0.35 * HY - 0.45, hsz + 0.28),
        (hax + HX, hay + HY, haz))                   # 훅 마지막 프레임의 카메라 그대로
CAM1 = ((2.60, -9.40, 3.50), (-1.30, 0.30, 0.75))    # 마을 조망(설계 §6-3 높이 3.5)

FACE0 = unison.FACE + HEADING       # 훅이 끝났을 때 여섯이 보던 쪽 — 걷기 전후로 여기 돌아온다
for arm, (x, y) in zip(arms, HOME):
    arm.location = (x, y, 0)
    arm.rotation_euler = (0, 0, FACE0)

BASE_Z = [[b.location.z for b in row] for row in marks]
BASE_SZ = [[b.scale.z for b in row] for row in marks]

cam = stage.light_camera()
stage.key_from_view(*CAM0)

print('[body1] %d비트 · %.2f초 · 동작 %s · 흩어짐 %.2fm 을 %.2f초에 걸어간다(%.2f~%.2f, 복귀 %.2f)'
      % (len(JOBS), DUR, list(JOBS), SCATTER_D, SCATTER_SPAN, COLD_AT,
         COLD_AT + SCATTER_SPAN, BACK_AT))

def draw(fi):
    t = fi / FPS
    u = t / DUR

    # 표식이 꺼진다 — 훅에서 자란 것을 거꾸로 되감는다
    off = 1 - ease(t / 0.90)
    for row, zs, szs in zip(marks, BASE_Z, BASE_SZ):
        for b, z0, s0 in zip(row, zs, szs):
            b.scale.z = max(s0 * off, 1e-4)
            b.location.z = unison.MARK_Z + (z0 - unison.MARK_Z) * off
            b.hide_render = off <= 0.001

    # ── ①②③ 여섯이 **같은 순간에** 같은 일로 갈아탄다 ──
    job = motions.sequence(JOBS, t, BEAT)
    # ── ④⑤ 서로 다른 쪽으로 **걸어** 흩어졌다가, 한 자리로 **걸어** 돌아온다 ──
    #    🔑 자리는 걸은 시간에 비례해 민다 — 그래야 보폭과 이동이 맞아 발이 안 미끄러진다.
    # 한기가 닿았다 지나가는 세기 0~1~0. 흩어졌다 돌아오는 동안 내내 분다.
    CHILL = math.sin(math.pi * min(max((t - COLD_AT) / COLD_SPAN, 0.0), 1.0))

    out, back = leg(t, COLD_AT), leg(t, BACK_AT)
    k = (out - back) / SCATTER_SPAN
    wo, wb = leg_mix(t, COLD_AT), leg_mix(t, BACK_AT)
    # 동작은 여섯이 **끝까지 같다**(위상 오차 0) — 갈라지는 것은 방향뿐이다
    spec = motions.blend(job, motions.walk(out + back), max(wo, wb))
    # 🔴 한기는 **일하는 채로** 맞는다 — 움츠림으로 통째로 갈아타면 하던 일이 사라져서
    #    「추워서 일을 멈췄다」가 된다. 이 편의 사건은 그게 아니다.
    spec = motions.blend(spec, motions.huddle(t), CHILL * 0.55)
    for i, (arm, (x, y)) in enumerate(zip(arms, HOME)):
        dx, dy = unison.scatter_at(i, k)
        stage.pose(arm, spec)
        arm.location = (x + dx, y + dy, 0)
        arm.rotation_euler = (0, 0, turn_to(turn_to(FACE0, OUT_FACE[i], wo),
                                            BACK_FACE[i], wb))

    # 🔴 한기를 **그리지 않는다.** 앞 판은 파란 판때기를 마을 위로 밀었는데, 한기는 물체가
    #    아니라 사건이라 물체로 그리면 저급해진다. 지금은 같은 세기(`CHILL`)를 셋에 나눠 준다:
    #    불이 눕고 · 눈이 몰아치고 · 사람이 움츠린다. 반응하는 것이 곧 한기다.
    village.flicker(fire, t, lean=CHILL * GUST)
    village.snowfall(flakes, t, wind=SNOW_BASE + CHILL * GUST, k=1.0)

    # 카메라 — 훅이 끝난 자리에서 조망으로 **계속** 오른다(컷 없음).
    # 🔴 렌즈는 안 건드린다. 설계표의 조망은 50mm 지만 훅이 45mm 로 끝났고, 샷 안에서
    #    렌즈를 바꾸면 그게 확대다(설계 §6-3 이 금지한 것). 높이로만 조망을 만든다.
    e = ease(u)
    loc = tuple(a + (b - a) * e for a, b in zip(CAM0[0], CAM1[0]))
    at = tuple(a + (b - a) * e for a, b in zip(CAM0[1], CAM1[1]))
    stage.aim(cam, loc, at)


stage.bake(OUT, NF, FPS, draw, 'body1')
