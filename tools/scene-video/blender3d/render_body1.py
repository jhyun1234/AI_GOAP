"""ep15s-1 본문1 — 증상 확정. 훅에서 **컷 없이** 이어진다.
   blender --background --factory-startup --python render_body1.py

  0.00~1.42  ① 카메라가 조망으로 오르며 여섯이 동시에 밭일을 시작한다
  1.42~2.83  ② 동시에 우물 긷기로 갈아탄다
  2.83~4.25  ③ 동시에 불 쬐기로 갈아탄다
  4.25~5.67  ④ 한기가 마을을 스친다 — 여섯이 잠깐 흩어진다
  5.67~7.09  ⑤ 도로 겹친다

🔴 **설계 §4-1 을 글자 그대로는 못 지었다.** 비트 시트는 「①밭에 같이 ②우물에 같이
   ③불에 같이」인데, 세 자리를 실제로 도는 데는 **12.9 초**가 든다(밭→우물 5.81m,
   우물→불 3.49m, 걸음 0.72m/s). 대본은 7.09 초다 — 1.8 배 모자란다.
   빠르게 걷게 하면 발이 미끄러지고, 그건 5비트 훅이 반려된 그 문제의 반대편이다.
   🔑 그래서 **자리가 아니라 박자로** 짓는다. 대본이 말하는 것도 장소가 아니라 때다 —
   「밭에 가는 **때**도 불 쬐는 **때**도 같았고요」. 여섯이 같은 순간에 같은 일로
   갈아타는 것이 그 문장이다. 마을(밭·우물·모닥불)은 조망 화면 안에 다 들어 있다.

🔴 ④ 는 **자리만** 흩어진다. 동작까지 갈라 놓으면 본문3 의 「군무가 깨진다」와 구별이
   안 되고, 그쪽이 이 편의 결말이다.

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

COLD_AT, COLD_SPAN = 3.0, 1.9       # 한기가 지나가는 구간(비트 ④ 전후)


def ease(u):
    u = max(0.0, min(1.0, u))
    return u * u * (3 - 2 * u)


bpy.ops.wm.read_factory_settings(use_empty=True)
village.build()
arms = unison.place()
marks = unison.personality_marks(arms)
fire = village.flame(village.SPOTS['fire'])
front = village.cold_front()

# 🔑 **훅이 끝난 자리에서 시작한다.** 컷이 아니므로 여섯의 자리·방향·카메라가 이어져야 한다.
#    값을 손으로 옮겨 적지 않는다 — 훅과 같은 함수를 같은 인자로 부른다.
HOOK_DUR = stage.shot_seconds(EP, 'SH')
HX, HY, HEADING, _ph = unison.march(HOOK_DUR, HOOK_DUR / len(unison.HOOK_BEATS))
HOME = [(x + HX, y + HY) for x, y in unison.STANDS]
(hsx, hsy, hsz), (hax, hay, haz) = unison.HOOK_CAM
CAM0 = ((hsx + 0.35 * HX + 1.10, hsy + 0.35 * HY - 0.45, hsz + 0.28),
        (hax + HX, hay + HY, haz))                   # 훅 마지막 프레임의 카메라 그대로
CAM1 = ((2.60, -9.40, 3.50), (-1.30, 0.30, 0.75))    # 마을 조망(설계 §6-3 높이 3.5)

for arm, (x, y) in zip(arms, HOME):
    arm.location = (x, y, 0)
    arm.rotation_euler = (0, 0, unison.FACE + HEADING)

BASE_Z = [[b.location.z for b in row] for row in marks]
BASE_SZ = [[b.scale.z for b in row] for row in marks]

cam = stage.light_camera()
stage.key_from_view(*CAM0)

print('[body1] %d비트 · %.2f초 · 동작 %s' % (len(JOBS), DUR, list(JOBS)))

for fi in range(NF):
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
    spec = motions.sequence(JOBS, t, BEAT)
    # ── ④⑤ 한기가 스치는 동안만 자리가 흩어졌다 돌아온다 ──
    k = ease((t - COLD_AT) / 0.55) - ease((t - COLD_AT - COLD_SPAN) / 0.75)
    for i, (arm, (x, y)) in enumerate(zip(arms, HOME)):
        dx, dy = unison.scatter_at(i, k)
        stage.pose(arm, spec)                        # 동작은 여섯이 **끝까지 같다**
        arm.location = (x + dx, y + dy, 0)

    village.sweep(front, (t - COLD_AT) / COLD_SPAN)
    village.flicker(fire, t)

    # 카메라 — 훅이 끝난 자리에서 조망으로 **계속** 오른다(컷 없음).
    # 🔴 렌즈는 안 건드린다. 설계표의 조망은 50mm 지만 훅이 45mm 로 끝났고, 샷 안에서
    #    렌즈를 바꾸면 그게 확대다(설계 §6-3 이 금지한 것). 높이로만 조망을 만든다.
    e = ease(u)
    loc = tuple(a + (b - a) * e for a, b in zip(CAM0[0], CAM1[0]))
    at = tuple(a + (b - a) * e for a, b in zip(CAM0[1], CAM1[1]))
    stage.aim(cam, loc, at)

    bpy.context.view_layer.update()
    bpy.context.scene.render.filepath = os.path.join(OUT, '%04d.png' % fi)
    bpy.ops.render.render(write_still=True)

print('[body1] %d frames -> %s' % (NF, OUT))
