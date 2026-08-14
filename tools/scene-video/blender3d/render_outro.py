"""ep15s-1 아웃트로 — 다음 편 예고. 본문3 에서 **컷 없이** 이어진다.
   blender --background --factory-startup --python render_outro.py

  0.00~4.20  여섯이 **제각기 제 볼일을 본다** — 밭 가는 사람, 불 옆에서 밥 먹는 둘,
             서로 다른 쪽으로 걸어가는 셋. 그 앞 허공에서 붉은 덩이가 위아래로 흔들리며
             세 번 다가온다 — 여섯은 한 번도 그쪽을 안 본다
  4.20~7.20  걷던 셋이 멈춰 숨을 쉰다. 밭일과 밥은 계속된다(뒤 3 초는 아웃트로 카드가 덮는다)

🔴 **여섯을 굳히지 않는다.** 앞 판은 `reach`→`freeze` 로 여섯을 세워 뒀는데, 대본이 말하는
   것은 「굶주린 주민이 늑대 앞에서 **안 도망가요**」다. 굳은 여섯은 「못 움직인다」로
   읽히고 그건 다른 말이다 — **평소처럼 제 일을 하는데 위험을 무시하는 것**이 이 예고다.

🔴 **여섯을 같은 짓 시키지 마라.** 그 앞 판은 여섯을 전부 고리 위에 올려 돌렸는데,
   서로 다른 방향이어도 같은 곡선을 같은 속도로 그리니 「셋씩 짝지어 원을 돈다」로 읽혔다.
   군무는 훅의 장치이고 본문3 이 이미 깨뜨렸다 — 여기서 되살리면 결말이 무효가 된다.
   🔑 그래서 **동작 자체가 셋으로 갈린다**: 밭일 · 밥 · 걷기. 자리는 본문3 이 어디로
   보냈는지가 정한다(밭 위에 선 사람이 밭을 갈고, 불 곁에 선 둘이 밥을 먹는다).

🔴 늑대는 화면에 안 만든다(게임에서 사라진 종족이다). 위험은 **붉은 덩이**로 말한다.
   🔑 앞 판의 파란 띠(`village.cold_front`)를 걷어냈다. 한기는 「겨울이 온다」(본문1)에
   이미 배정된 뜻이고, 같은 도형에 뜻 둘을 얹으면 본문1 의 겨울이 흐려진다.
   붉은색은 설계 §2 에서 **이미 벌어진 실패·죽음**이다 — 늑대 앞의 주민에 맞는 색이다.
🔴 뜻층은 붉은색 + 앰버(모닥불) 둘 — 상한이다. 여기에 하나 더 넣으면 그때 규약이 깨진다.
🔴 붉은 덩이는 **끝까지 허공에 뜬 채로만 논다**(z 1.45 아래로 안 내려온다). 땅에 가까워지면
   「굴러오는 상자」가 되고, 공중에 떠서 흔들려야 짐승도 물건도 아닌 **위험 그 자체**로 읽힌다.
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
WALK_UNTIL = 4.20                  # 걷는 셋이 걷는 시간. 「안 도망가요」가 들리는 동안이다
BLEND = 0.26                       # 걷기 ↔ 제 일 이음새. 0 이면 관절이 순간이동한다

# ── 붉은 덩이 — 위험. 세 번 다가오고, 내내 위아래로 흔들린다 ──
DANGER = 0.42                      # 한 변. 주민 키 0.95 의 절반 — 크면 마을보다 덩이가 주인공이다
BOB_HZ = 1.45
# 🔴 **지붕(1.85) 위에서 시작해 내려오되 머리 위에서 멈춘다.** 처음부터 낮게 두면 멀리 있는
#    동안 지붕에 얹혀 「지붕 위의 빨간 상자」가 되고, 끝까지 내리면 「굴러오는 상자」가 된다.
BOB_FAR, BOB_NEAR = (2.70, 0.18), (1.85, 0.48)      # (한가운데 높이, 흔들림 폭)
assert BOB_NEAR[0] - BOB_NEAR[1] > 0.95, '붉은 덩이가 주민 키 아래로 내려온다'
STOPS = (0.40, 1.60, 2.80)         # 무리 중심에서 **카메라 쪽**으로 이만큼. 매번 앞으로 나온다
CREEP_TO = 3.35                    # 그 뒤로도 계속 온다. 위험은 멈추지 않는다
PULSES, PULSE_END = 3, 4.20


def ease(u):
    u = max(0.0, min(1.0, u))
    return u * u * (3 - 2 * u)


def turn_to(a, b, k):
    """a 에서 b 로 **짧은 쪽으로** 돈다. 안 감아 주면 주민이 반대로 한 바퀴 돈다."""
    return a + ((b - a + math.pi) % (2 * math.pi) - math.pi) * k


bpy.ops.wm.read_factory_settings(use_empty=True)
v = village.build()
arms = unison.place()
fire = village.flame(village.SPOTS['fire'])
bpy.context.view_layer.update()
OBSTACLES = village.obstacles(exclude=(v['ground'],))

# 본문3 이 흩어 놓은 자리에서 이어받는다 — 컷이 아니다.
# 🔑 값을 옮겨 적지 않는다. 본문3 과 **같은 식**으로 다시 센다(`⑦` 이 걸어간 거리 포함).
HOOK_DUR = stage.shot_seconds(EP, 'SH')
HX, HY, _hd, _ph = unison.march(HOOK_DUR, HOOK_DUR / len(unison.HOOK_BEATS))
HOME = [(x + HX, y + HY) for x, y in unison.STANDS]
CX = sum(x for x, _ in HOME) / 6
CY = sum(y for _, y in HOME) / 6
AWAY = unison.SPLIT_FAN                     # 🔴 본문3 ⑦ 과 **같은 상수** — 갈라지면 자리가 튄다
DRIFT = motions.WALK_SPEED * (stage.shot_seconds(EP, 'S3') / 7)      # 본문3 ⑦ 이 걸어간 거리
START = [(x + DRIFT * math.cos(a), y + DRIFT * math.sin(a))
         for (x, y), a in zip(HOME, AWAY)]
SX = sum(x for x, _ in START) / 6
SY = sum(y for _, y in START) / 6

# 🔴 **눈높이**(설계 §6-3 의 0.55 층). 앞 판은 2.15m 에서 내려다봤다.
# 🔑 거리는 안 줄인다 — 실측: 여섯이 선 줄이 4.75m 라 다 담으려면 **6m 이상**이어야 하고,
#    3m 로 붙으면 주민이 46% 로 커지는 대신 **넷만 남는다.** 군무가 아닌 샷이라 여기서는
#    여섯이 겹쳐도 되지만, 「제각기 제 일을 한다」는 여섯이 다 보여야 성립한다.
# 🔑 그래서 얻는 것은 크기가 아니라 **각도**다. 낮게 서면 주민과 붉은 덩이가 바닥이 아니라
#    **하늘을 배경으로** 서서 실루엣이 살고, 다가오는 덩이가 눈앞으로 커진다.
CAM = ((2.90, -7.60, 0.72), (-1.25, 0.45, 0.60))
_cd = (CAM[0][0] - CX, CAM[0][1] - CY)
_cn = math.hypot(*_cd)
CAM_DIR = (_cd[0] / _cn, _cd[1] / _cn)          # 무리 중심에서 카메라 쪽

# ── 누가 무엇을 하는가. 🔴 **자리가 정한다.** 이름표를 손으로 붙이면 밭 밖에서 밭을 갈고
#    불에서 5 m 떨어진 데서 밥을 먹는다 — 그림이 거짓말이 되는 가장 흔한 방식이다.
FIELD, FIRE = village.SPOTS['field'], village.SPOTS['fire']
BUSH = village.SPOTS['bush']
_near = lambda i, s: math.hypot(START[i][0] - s.x, START[i][1] - s.y)
FARMER = min(range(6), key=lambda i: _near(i, FIELD))
# 🔑 덤불이 생겨서 **캘 자리**가 있다. 같은 규칙으로 뽑는다 — 이름표를 손으로 붙이지 않는다
GATHERER = min((i for i in range(6) if i != FARMER), key=lambda i: _near(i, BUSH))
EATERS = sorted((i for i in range(6) if i not in (FARMER, GATHERER)),
                key=lambda i: _near(i, FIRE))[:2]
WALKERS = [i for i in range(6) if i not in (FARMER, GATHERER) and i not in EATERS]
JOB = ['walk'] * 6
JOB[FARMER] = 'farm'
JOB[GATHERER] = 'gather'
for i in EATERS:
    JOB[i] = 'eat'
# 🔑 걷던 셋 중 **불에서 가장 먼 하나는 앉아 쉰다**(게임 `Rest`). 여기서도 자리가 정한다.
#    앉은 사람이 하나 있어야 「도망가지 않는다」가 자세로 읽힌다 — 위험이 다가오는 샷이다.
RESTER = max(WALKERS, key=lambda i: _near(i, FIRE))
JOB[RESTER] = 'rest'
WALKERS = [i for i in WALKERS if i != RESTER]

WALK_D = motions.WALK_SPEED * WALK_UNTIL
GO = {}

# 캐러 가는 사람 — 덤불 **0.55 m 앞**까지 간다(손이 닿는 거리, PROPS.md 에서 판정한 값).
BUSH_DIR = math.atan2(BUSH.y - START[GATHERER][1], BUSH.x - START[GATHERER][0])
BUSH_D = max(0.0, math.hypot(BUSH.x - START[GATHERER][0], BUSH.y - START[GATHERER][1]) - 0.55)
BUSH_AT = BUSH_D / motions.WALK_SPEED          # 도착 시각 — 캐기는 여기서 시작한다
assert BUSH_AT < DUR - 1.0, '덤불까지 걷는 데 %.2f 초 — 캘 시간이 없다' % BUSH_AT


# 🔴 **누가 언제 멈추는가는 한 곳에서만 정한다.** 자리를 미는 시각과 걷기 주기를 멈추는
#    시각을 따로 적었더니 캐러 가는 사람이 4.42 초까지 걸어가는데 주기는 4.20 초에 얼어붙어
#    마지막 0.22 초를 **얼어붙은 다리로 미끄러져** 갔다. 두 값이 갈릴 수 있으면 언젠가 갈린다.
def stop_at(i):
    """i 번이 걸음을 멈추는 시각. 자리와 걷기 주기가 **같은 값**을 쓴다."""
    return BUSH_AT if i == GATHERER else WALK_UNTIL


def place_at(i, t):
    """i 번 주민의 자리. 걷는 둘과 **캐러 가는 하나**가 움직이고 나머지는 제자리에서 일한다."""
    # 🔑 자원이 사람에게 오지 않는다. 사람이 **걸어간다** — 그게 이 게임의 사건이다.
    # 🔴 거리를 시간으로 나눠 밀지 마라. `WALK_SPEED` 로 가고 **멈출 시각에 멈춘다** —
    #    안 그러면 발이 미끄러지고, 그건 이 파이프라인이 여러 번 반려된 실패다.
    ang = BUSH_DIR if i == GATHERER else GO.get(i)
    if ang is None:
        return START[i]
    d = motions.WALK_SPEED * min(t, stop_at(i))
    return START[i][0] + d * math.cos(ang), START[i][1] + d * math.sin(ang)


# ── 걷는 셋이 갈 쪽 ────────────────────────────────
# 🔴 **각을 손으로 정하지 마라.** 세 번 해 봤고 세 번 다 틀렸다: 바깥으로 곧장 보내면
#    프레임을 나가고(45mm 수평 화각 43.6°), 무리 쪽으로 가로지르게 하면 서로를 뚫는다
#    (실측 0.12 m — 주민 폭의 사분의 일이다). 셋을 한꺼번에 만족시키는 각은 눈에 안 보인다.
# 🔑 그래서 **재서 고른다**: 소품 여유 · 서로 간 거리 · 무리에서 벗어나는 정도.
#    한 명씩 이미 정해진 사람들을 피해 가며 고른다(그리디) — 마을이나 자리가 바뀌어도 산다.
CANDIDATES = [math.radians(a) for a in range(0, 360, 10)]
# 🔴 프레임은 **가로가 먼저 터진다.** 45mm 수평 화각 43.6° 라 옆으로 벌어지면 바로 잘리고,
#    앞으로 다가오면 한 명이 화면을 반쯤 채워 마을을 가린다. 그래서 화면 가로(`LATERAL`)와
#    깊이(`TOWARD`)를 따로 잰다 — 무리 한가운데에서의 거리 하나로 재면 둘이 섞여 못 막는다.
LATERAL, TOWARD = 2.55, 2.10
SAFE_PROP, SAFE_BODY = 0.15, 2 * village.BODY_R
PERP = (-CAM_DIR[1], CAM_DIR[0])


def _score(i, ang, taken):
    """i 가 ang 쪽으로 걸을 때의 점수. 하나라도 문턱 아래면 None(못 쓴다)."""
    path = [(START[i][0] + motions.WALK_SPEED * min(DUR * k / 120, WALK_UNTIL) * math.cos(ang),
             START[i][1] + motions.WALK_SPEED * min(DUR * k / 120, WALK_UNTIL) * math.sin(ang))
            for k in range(121)]
    ox_, oy_ = path[-1][0] - SX, path[-1][1] - SY
    if abs(ox_ * PERP[0] + oy_ * PERP[1]) > LATERAL:
        return None
    if ox_ * CAM_DIR[0] + oy_ * CAM_DIR[1] > TOWARD:
        return None
    prop = min(math.hypot(px - ox, py - oy) - r - village.BODY_R
               for px, py in path for ox, oy, r, _nm in OBSTACLES)
    if prop < SAFE_PROP:
        return None
    body = min([math.hypot(px - qx, py - qy)
                for j in range(6) if j != i and (j in taken or j not in WALKERS)
                for k, (px, py) in enumerate(path)
                for qx, qy in (place_at(j, DUR * k / 120),)] or [9.0])
    if body < SAFE_BODY:
        return None
    return min(prop, body)


for _i in WALKERS:
    _best = max(((_score(_i, a, GO), a) for a in CANDIDATES),
                key=lambda p: (p[0] is not None, p[0] or 0))
    assert _best[0] is not None, (
        '%d 번 주민이 걸어갈 쪽이 없다 — `unison.SPLIT_FAN` 이 보낸 자리를 다시 봐라' % _i)
    GO[_i] = _best[1]

_worst = min(math.hypot(p[0] - ox, p[1] - oy) - r - village.BODY_R
             for k in range(201) for i in WALKERS
             for p in (place_at(i, DUR * k / 200),)
             for ox, oy, r, _nm in OBSTACLES)
_near = min(math.hypot(place_at(i, DUR * k / 120)[0] - place_at(j, DUR * k / 120)[0],
                       place_at(i, DUR * k / 120)[1] - place_at(j, DUR * k / 120)[1])
            for k in range(121) for i in range(6) for j in range(i + 1, 6))
assert _worst > SAFE_PROP, '걷는 주민이 소품을 %.2f m 까지 파고든다' % _worst
assert _near > SAFE_BODY, '주민 둘이 서로를 뚫고 지나간다: %.2f m' % _near

# ── 붉은 덩이 ─────────────────────────────────────
bpy.ops.mesh.primitive_cube_add(size=DANGER, location=(SX, SY, BOB_FAR[0]))
danger = bpy.context.object
danger.data.materials.append(stage.meaning_mat('red', strength=1.0, albedo_scale=0.25))

cam = stage.light_camera()
stage.key_from_view(*CAM)

print('[outro] %.2f초 · 하는 일 %s · 걷기 %.2fm · 소품 여유 %.2fm · 주민 최소거리 %.2fm'
      % (DUR, JOB, WALK_D, _worst, _near))
print('[outro] 덤불까지 %.2fm · 도착 %.2f초 · 걷는 셋은 %.2f초까지'
      % (BUSH_D, BUSH_AT, WALK_UNTIL))

def draw(fi):
    t = fi / FPS
    u = t / DUR

    # ── 여섯이 제각기 제 일을 한다 ──
    #    걷는 셋만 `WALK_UNTIL` 에 멈추고, 밭일과 밥은 끝까지 돈다(화면이 안 굳는다).
    into = ease(t / BLEND)                        # 본문3 의 걷기에서 제 일로 넘어오는 이음새
    halt = 1.0 - ease((t - WALK_UNTIL) / BLEND)
    pts = []
    for i, arm in enumerate(arms):
        px, py = place_at(i, t)
        # 🔴 걷기 주기는 **그 사람이 멈추는 시각**에 얼어붙는다 — 자리를 미는 것과 같은 값이다
        walk_spec = motions.walk(min(t, stop_at(i)))
        if JOB[i] == 'walk':
            spec = motions.blend(motions.stop(t), walk_spec, halt)
            face = GO[i] + math.pi                # 규약: 회전 θ 의 정면은 (-cos θ, -sin θ)
        else:
            # 🔑 캐는 사람은 **도착한 뒤에** 캔다. 걸어오는 내내 캐면 허공을 파며 온다
            k_job = ease((t - BUSH_AT) / BLEND) if i == GATHERER else into
            spec = motions.blend(walk_spec, motions.MOTIONS[JOB[i]](t), k_job)
            # 🔴 **하는 일이 보는 쪽을 정한다.** 밭 밖에서 밭을 갈거나 덤불을 등지고 캐면
            #    그림이 거짓말이 된다. 앉아 쉬는 사람은 캘 것도 갈 것도 없으니 불을 본다.
            aim_at = {'farm': FIELD, 'gather': BUSH}.get(JOB[i], FIRE)
            face = math.atan2(py - aim_at.y, px - aim_at.x)
        stage.pose(arm, spec)
        arm.location = (px, py, 0)
        # 🔴 **도는 것은 이음새(`into`)를 따른다 — 하는 일이 시작하는 시각이 아니다.**
        #    캐는 사람만 `k_job` 이 도착(BUSH_AT)에 열리는데 거기에 회전을 매어 뒀더니,
        #    덤불까지 걸어가는 내내 **본문3 에서 보던 쪽을 본 채 옆으로 미끄러져 갔다** —
        #    다리는 앞으로 걷는데 몸은 옆으로 가는 그림이다(사용자가 잡았다).
        #    🔑 캐러 가는 사람의 `face` 는 덤불 쪽이고 그게 곧 **가는 쪽**이라, 이음새에서
        #       한 번 돌고 나면 도착할 때까지 앞을 보고 걷는다.
        arm.rotation_euler = (0, 0, turn_to(AWAY[i] + math.pi, face, into))
        pts.append((px, py))
    gx, gy = sum(p[0] for p in pts) / 6, sum(p[1] for p in pts) / 6

    # ── 붉은 덩이가 세 번 다가오고, 내내 허공에서 위아래로 흔들린다 ──
    #    🔴 물러나지 않는다. 왔다 갔다 하면 그건 파도이지 다가오는 위험이 아니다.
    #    🔴 세 번째 뒤에도 **멈추지 않는다.** 앞 판은 여기서 화면이 3.1 초 굳어 정적 상한을
    #       넘겼는데, 고칠 자리는 카메라가 아니라 이야기였다 — 위험이 멈출 이유가 없다.
    if t <= PULSE_END:
        p = t / (PULSE_END / PULSES)
        step = min(int(p), PULSES - 1)
        d_from = STOPS[step - 1] if step else -1.2
        dist = d_from + (STOPS[step] - d_from) * ease(p - step)
    else:
        dist = STOPS[-1] + (CREEP_TO - STOPS[-1]) * (t - PULSE_END) / (DUR - PULSE_END)
    near = ease(min(t, PULSE_END) / PULSE_END)
    mid = BOB_FAR[0] + (BOB_NEAR[0] - BOB_FAR[0]) * near
    amp = BOB_FAR[1] + (BOB_NEAR[1] - BOB_FAR[1]) * near
    # 🔑 무리의 **지금** 한가운데를 기준으로 앞에 선다 — 여섯이 움직이므로 고정 좌표로 두면
    #    어느 순간 뒤로 돌아간다.
    danger.location = (gx + CAM_DIR[0] * dist, gy + CAM_DIR[1] * dist,
                       mid + amp * math.sin(2 * math.pi * BOB_HZ * t))
    # 흔들릴 때 살짝 기운다 — 축을 세워 두면 위아래로 미는 판때기로 읽힌다
    danger.rotation_euler = (0.18 * math.sin(2 * math.pi * BOB_HZ * t + 0.9),
                             0.14 * math.cos(2 * math.pi * BOB_HZ * t), 0.35 * t)

    # 🔴 불은 **안 끈다.** 앰버가 사라지면 「장면이 끝났다」로 읽힌다 — 앞 판이 그랬다.
    village.flicker(fire, t)

    # 🔴 겨냥이 무리를 따라간다. 붙박이로 두면 걸어 다니는 셋이 프레임을 나간다.
    # 🔴 그렇다고 무리 한가운데를 곧장 겨냥하지 마라 — 승인된 판의 겨냥점은 마을 쪽으로
    #    0.9m 치우쳐 있고, 한가운데로 옮기면 마을이 위쪽 3분의 1로 밀리고 아래가 빈 바닥이
    #    된다(실제로 그랬다). **치우침을 그대로 둔 채** 무리가 움직인 만큼만 같이 민다.
    (sx, sy, sz), (ax, ay, az) = CAM
    dx, dy = gx - CX, gy - CY
    stage.aim(cam, (sx - 0.35 * u + dx, sy - 0.30 * u + dy, sz + 0.10 * u),
              (ax + dx, ay + dy, az))


stage.bake(OUT, NF, FPS, draw, 'outro')
