"""ep15s-1 본문2 — 원인. **계기층(시안)이 이 편에서 처음 뜨는 자리다.**
   blender --background --factory-startup --python render_body2.py

  0.00~1.49  ① 개발자 시점으로 컷 ↑. 여섯이 밭에서 **똑같이** 일하고 있다
  1.49~2.98  ② 마을 위로 할 일 서른 칸이 뜬다 — 계기층
  2.98~4.47  ③ 성격(보라)이 **셋에만** 닿는다
  4.47~5.96  ④ 둘이 꺼진다
  5.96~7.45  ⑤ 하나만 남는다 — 나머지 표가 물러나고 그 하나가 커진다
  7.45~8.94  ⑥ 그 하나에서 실 여섯이 내려와 **여섯이 다 거기 걸려 있는 것**이 보인다

🔴 이 샷은 **컷이다.** 이 편에서 컷은 둘뿐이고(설계 §4-1) 여기가 그 하나다 —
   계기가 주인공인 자리라 개발자 시점으로 올라간다. 70mm 로 눌러 일부러 평면처럼 본다.

🔴 뜻층은 보라 하나뿐이다. 시안은 **계기층이라 뜻층 예산에 안 들어간다**(설계 §2) —
   그래서 이 샷은 시안이 화면을 덮어도 「한 프레임에 둘까지」를 안 깬다.
"""
import bpy, sys, os, json, math

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stage, village, unison, motions, instrument

EP, SHOT = 'ep15s-1', 'S2'
OUT = os.path.join(stage.OUT_ROOT, EP, 'body2', 'frames')
EPDIR = os.path.join(stage.SV_ROOT, 'episodes', EP)
FPS = 30
os.makedirs(OUT, exist_ok=True)

# 🔑 길이도 칸 수도 손으로 안 적는다. 대본이 정본이다.
_timed = json.load(open(os.path.join(EPDIR, 'build', 'timed.json'), encoding='utf-8'))
_s = next(s for s in _timed['shots'] if s['id'] == SHOT)
DUR = (sum(l['dur'] + l.get('pause', 0) for l in _s['lines']) + 350) / 1000.0
SPEC = next(s for s in json.load(open(os.path.join(EPDIR, 'scene.json'), encoding='utf-8'))
            ['shots'] if s['id'] == SHOT)['spec']
COLS, ROWS = SPEC['cols'], SPEC['rows']
LIT, SOLE = list(SPEC['litIdx']), SPEC['soleIdx']
assert SOLE in LIT, 'soleIdx 가 litIdx 안에 없다 — 남는 칸이 켜진 적이 없는 칸이 된다'

NF = round(DUR * FPS) + 1
BEAT = DUR / 6                       # 설계 §4-1 본문2 = 비트 여섯
assert BEAT <= 1.5, '비트가 홀드 상한을 넘었다: %.2f 초' % BEAT

# 개발자 시점 — 설계 §6-3. 70mm 로 눌러 일부러 평면처럼 본다.
# 🔴 설계표의 높이 9.0 으로는 안 된다. 70mm 는 수평 화각이 28.7° 뿐이라 표(4.75×3.8)와
#    밭의 여섯을 한 프레임에 넣으려면 **17m 는 떨어져야** 하고, 그 거리에서 40° 로 내려다보면
#    높이가 13.6 이 된다. 표를 이 편에서 처음 띄우는 자리라 렌즈를 줄이지 않고 거리를 벌렸다.
# 🔑 겨냥점은 표와 여섯의 **사이**다. 표에 겨냥하면 여섯이 구석으로 밀려나는데,
#    개발자 시점에서도 「여섯이 똑같다」가 먼저 읽혀야 한다(이 편의 사건 ①).
CAM = ((5.60, -11.90, 13.80), (-1.45, 0.05, 1.45))
LENS = 70
# 🔴 이 샷은 **가만히 두면 죽는다.** 첫 판이 여섯 비트 내내 인접 프레임 차 0.0004~0.0009 로,
#    `probe.frame_diff` 가 정적이라 부르는 0.0008 언저리였다 — 8.9 초짜리 홀드다.
#    사건(칸이 뜨고 꺼지는 것)은 화면의 몇 %라 그것만으로는 그림이 안 변한다.
# 🔑 그래서 카메라가 표를 **돈다.** 연출을 덧칠하는 것이 아니라, 공중에 뜬 표와 땅의 마을이
#    시차로 어긋나는 것이 「표가 세계 위에 떠 있다」를 보여 주는 유일한 방법이다.
#    ❌ 확대는 안 쓴다(설계 §6-3) — 궤도는 이동이지 확대가 아니다.
ORBIT = math.radians(19)


def ease(u):
    u = max(0.0, min(1.0, u))
    return u * u * (3 - 2 * u)


bpy.ops.wm.read_factory_settings(use_empty=True)
village.build()

# 여섯은 밭에서 **똑같이** 일한다. 위에서 봐도 그게 먼저 읽혀야 한다.
arms = []
for x, y in instrument.work_block(village.SPOTS['field']):
    _m, arm = stage.rigged(loc=(x, y, 0), rot_z=math.degrees(unison.FACE))
    arms.append(arm)

grid = instrument.build(COLS, ROWS)
pos = instrument.cells(COLS, ROWS)
violet = stage.meaning_mat('violet', strength=1.0)
cyan = stage.instrument_mat(strength=1.0)
sole_xy = pos[SOLE]

# ⑥ 의 실 여섯 — 미리 만들어 두고 껐다 켠다(매 프레임 만들면 결정성이 깨진다)
threads = [instrument.thread((x, y, 0.95), (sole_xy[0], sole_xy[1], instrument.Z), cyan)
           for x, y in instrument.work_block(village.SPOTS['field'])]

cam = stage.light_camera()
cam.data.lens = LENS
stage.key_from_view(*CAM)

for cell in grid:
    instrument.show(cell, False)
for t_ in threads:
    t_.hide_render = True

print('[body2] 6비트 · %.2f초 · 칸 %d×%d=%d · 보라 %s · 남는 칸 %d'
      % (DUR, COLS, ROWS, COLS * ROWS, LIT, SOLE))

for fi in range(NF):
    t = fi / FPS
    b = t / BEAT                                   # 지금 몇 번째 비트인가(실수)

    # ── ① 여섯이 똑같이 일한다. 계기가 뜨기 전에 **세계가 먼저** 보여야 한다 ──
    spec = motions.farm(t)
    for arm in arms:
        stage.pose(arm, spec)                      # 여섯이 같은 포즈 — 위상 오차 0

    # ── ② 서른 칸이 뜬다. 훑고 지나가듯 순서대로 — 「시스템이 세계를 스캔한다」 ──
    # ⑤ 남은 하나는 커지며 마을 쪽으로 내려오고, 나머지 표는 물러난다(꺼지는 게 아니라 작아진다)
    # 🔴 자리와 크기를 **한 군데서** 정한다. 첫 판은 뜨는 애니메이션을 아래 ⑤ 루프가
    #    덮어써서 칸이 자라지 않고 툭 튀어나왔다 — 같은 속성을 두 곳에서 쓰면 늘 이렇게 된다.
    sole_k = ease((b - 4.0) / 0.8)
    for i, cell in enumerate(grid):
        k = ease((b - 1.0 - i / len(grid) * 0.55) / 0.30)
        instrument.show(cell, k > 0.001)
        s = k * (1 + 0.60 * sole_k) if i == SOLE else k * (1 - 0.25 * sole_k)
        cell.scale = (s, s, s)
        cell.location.z = (instrument.Z + 1.10 * (1 - k)          # 위에서 내려앉는다
                           - (0.55 * sole_k if i == SOLE else 0))

    # ── ③ 셋에만 보라가 닿는다 ④ 둘이 꺼진다 ──
    for i in LIT:
        touched = b >= 2.0
        instrument.paint(grid[i], violet if touched else cyan)
        if i != SOLE:
            instrument.show(grid[i], b < 3.0 or not touched)

    # ── ⑥ 실 여섯이 그 하나로 올라온다 ──
    for j, t_ in enumerate(threads):
        k = ease((b - 5.0 - j * 0.06) / 0.35)
        t_.hide_render = k <= 0.001
        t_.scale.x = t_.scale.y = 0.020 * k

    u = t / DUR
    (sx, sy, sz), at = CAM
    dx, dy = sx - at[0], sy - at[1]
    a = math.atan2(dy, dx) + ORBIT * u
    r = math.hypot(dx, dy)
    stage.aim(cam, (at[0] + r * math.cos(a), at[1] + r * math.sin(a), sz - 0.55 * u), at)

    bpy.context.view_layer.update()
    bpy.context.scene.render.filepath = os.path.join(OUT, '%04d.png' % fi)
    bpy.ops.render.render(write_still=True)

print('[body2] %d frames -> %s' % (NF, OUT))
