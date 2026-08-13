"""계기층 — 마을 **위에 뜨는** 표. 팔레트 3층의 셋째 층이다(설계 §2).

🔴 **공중 전용이다.** 세계 물체에 시안을 칠하지 마라. 시안을 공중에 가둬 두는 것이
   초록(코드)·한기(추위)와 안 헷갈리게 하는 유일한 장치다 — 색이 셋 다 파란 대역이라
   물체에 붙는 순간 구별이 안 된다.
🔑 표가 마을 **위에** 뜬다는 것 자체가 이 층의 뜻이다. 「개발자가 세계를 표로 본다」를
   도형으로 한 것이라, 격자는 땅에 안 눕고 땅과 나란히 공중에 뜬다.

칸의 수·불 켜지는 칸·마지막 하나는 **`scene.json` 의 샷 spec 이 정본이다**(cols·rows·
litIdx·soleIdx). 여기 다시 적지 않는다 — 두 군데 적으면 언젠가 한쪽만 고쳐진다.
"""
import math

# 칸 크기와 높이. 🔴 높이는 **집 꼭대기(1.85)보다 위**여야 한다. 아래로 내리면 계기가
#    세계와 같은 층에 있는 것처럼 보이고, 그 순간 「위에서 내려다본 표」가 아니게 된다.
PITCH, CELL, Z = 0.95, 0.52, 2.60

# 개발자 시점 — 설계 §6-3. 70mm 로 눌러 일부러 평면처럼 본다. **본문2·3 이 공유한다**
# (설계 §4-1: C 시점은 본문2~3 에 걸쳐 한 번만 쓴다 — 두 파일에 따로 적으면 이음새가 튄다).
# 🔴 설계표의 높이 9.0 으로는 안 된다. 70mm 는 수평 화각 28.7° 뿐이라 표와 밭의 여섯을
#    한 프레임에 넣으려면 17m 는 떨어져야 하고, 그 거리에서 40° 로 내려다보면 13.8 이 된다.
DEV_CAM = ((5.60, -11.90, 13.80), (-1.45, 0.05, 1.45))
DEV_LENS = 70
DEV_ORBIT = math.radians(19)     # 🔴 안 돌리면 그림이 안 변해 8.9 초짜리 홀드가 된다


def dev_view(cam, u, orbit=None, drop=0.55):
    """개발자 시점 카메라를 u(0~1)만큼 궤도에 태운다. 반환 (loc, at).
    🔑 공중의 표와 땅의 마을이 시차로 어긋나는 것이 「표가 세계 위에 떠 있다」를 보이는 길이다.
       ❌ 확대는 안 쓴다(설계 §6-3) — 궤도는 이동이지 확대가 아니다."""
    import stage
    (sx, sy, sz), at = DEV_CAM
    dx, dy = sx - at[0], sy - at[1]
    a = math.atan2(dy, dx) + (DEV_ORBIT if orbit is None else orbit) * u
    r = math.hypot(dx, dy)
    loc = (at[0] + r * math.cos(a), at[1] + r * math.sin(a), sz - drop * u)
    stage.aim(cam, loc, at)
    return loc, at
CENTER = (-0.60, 1.00)


def cells(cols, rows, pitch=None):
    """왼쪽 위부터 행 우선. `scene.json` 의 litIdx 가 세는 순서와 같아야 한다."""
    ox, oy = CENTER
    p = PITCH if pitch is None else pitch
    return [(ox + (c - (cols - 1) / 2) * p, oy + ((rows - 1) / 2 - r) * p)
            for r in range(rows) for c in range(cols)]


def build(cols, rows, col=None, pitch=None, cell=None):
    """빈 칸 cols×rows 개를 공중에 띄운다. 반환: [obj, ...] (cells 와 같은 순서).

    🔴 칸을 **꽉 찬 판으로 만들지 마라.** 30 개를 발광 면으로 채우면 화면 윗쪽이
       눈 아픈 시안 덩어리가 된다(stage.meaning_mat 주석의 실측과 같은 함정).
       테두리만 남긴 빈 칸이라야 「아직 아무것도 안 든 할 일」로도 읽힌다."""
    import bpy
    import stage
    col = col or bpy.context.collection
    mat = stage.instrument_mat(strength=1.0)
    # 🔴 간격을 샷이 정할 수 있어야 한다. 180 칸을 기본 간격(0.95)으로 깔면 28 m 라
    #    어떤 카메라에도 안 들어간다 — 칸이 많아지면 간격이 줄어야 한다.
    p = PITCH if pitch is None else pitch
    c_ = CELL if cell is None else cell
    bar = c_ / 2
    # 🔴 테두리 굵기를 **칸 크기에 비례**시킨다. 고정 0.045 로 두고 180 칸을 0.17 로 깔았더니
    #    테두리끼리 붙어서 칸 백여든이 아니라 **격자무늬 한 장**으로 보였다.
    w = c_ * 0.088
    out = []
    for x, y in cells(cols, rows, p):
        bpy.ops.object.empty_add(location=(x, y, Z))     # 칸 하나 = 막대 넷의 부모
        cell = bpy.context.object
        for dx, dy, sx, sy in ((0, bar, CELL, 0.045), (0, -bar, CELL, 0.045),
                               (bar, 0, 0.045, CELL), (-bar, 0, 0.045, CELL)):
            bpy.ops.mesh.primitive_cube_add(size=1, location=(x + dx, y + dy, Z))
            b = bpy.context.object
            b.scale = (sx, sy, w)
            b.data.materials.append(mat)
            b.parent = cell
            b.matrix_parent_inverse = cell.matrix_basis.inverted()
        out.append(cell)
    return out


def paint(cell, mat):
    """칸 하나의 색을 바꾼다. 성격(보라)이 닿은 칸에 쓴다."""
    for b in cell.children:
        b.data.materials[0] = mat


def show(cell, on):
    """🔴 스케일 0 이나 발광 0 으로 끄지 마라 — 밑색이 남는다(README 함정 ⑥)."""
    for b in cell.children:
        b.hide_render = not on


def thread(a, b, mat, w=0.020):
    """두 점을 잇는 실. 계기가 세계의 무엇을 가리키는지 보일 때 쓴다.

    🔑 실은 **공중 물체**다. 주민 몸에 색을 칠하는 것이 아니라 위에서 내려와 가리킬 뿐이라
       「계기층은 세계 물체에 안 쓴다」를 안 깬다."""
    import bpy
    from mathutils import Vector
    a, b = Vector(a), Vector(b)
    d = b - a
    bpy.ops.mesh.primitive_cube_add(size=1, location=tuple((a + b) / 2))
    o = bpy.context.object
    o.rotation_mode = 'QUATERNION'
    o.rotation_quaternion = d.to_track_quat('Z', 'Y')
    o.scale = (w, w, d.length)
    o.data.materials.append(mat)
    return o


# ── 수치를 띄우는 것들 — 막대와 원판 (2026-08-13 사용자 지시) ───────────────
# 🔴 **「수치가 하나도 안 보여서 밋밋하다」**가 이 층이 생긴 이유다. 이 세계에는 글자가
#    없으므로(블렌더 쪽에 폰트가 없다) 수는 **길이와 채움**으로 보여야 한다.
# 🔑 그래도 규칙은 그대로다 — 이건 **계기층**이고 계기층은 공중에 뜬다. 세계 물체에
#    칠하지 않으므로 뜻층 예산(설계 §2 · 한 프레임 두 색)을 안 먹는다.
# 🔴 **2D 도형을 옮겨 그리는 것이 아니다**(runbook §3-0). 막대가 값을 말하고 그 값이
#    사람의 행동과 **같은 화면에서 함께 움직이는 것**이 3D 에서 새로 되는 일이다.
# 🔴 크기는 **재서 줄였다.** 0.80 m 는 주민 키(0.95)의 84% 라 여섯이 달면 화면이
#    막대 숲이 된다(프레임 시트로 잡았다). 0.42 는 머리 위에 얹혀 사람을 안 가린다.
GAUGE_W, GAUGE_H, GAUGE_GAP = 0.078, 0.42, 0.175
GAUGE_LO, GAUGE_HI = 85.0, 112.0        # 이 회차 수치대(92·95·100·105)를 담는 눈금 범위


def gauge_k(v, lo=GAUGE_LO, hi=GAUGE_HI):
    """수치 → 채움 비율(0~1). 🔑 **순수 함수라 블렌더 없이 검사된다.**
    🔴 0~100 을 그대로 쓰지 마라 — 92 와 105 의 차이가 13% 라 화면에서 안 보인다.
       이 편이 말하는 수는 92·95·100·105 뿐이므로 그 대역만 눈금에 담는다."""
    return max(0.0, min(1.0, (float(v) - lo) / (hi - lo)))


def _bar_mat(color, strength):
    """막대 재질 하나. 🔴 계기색이면 계기층, **뜻층 다섯 중 하나면 뜻층**이다.

    🔑 왜 뜻색을 허용하나 — 「굶주림이 바닥나 죽는다」 같은 수치는 **수치가 곧 사건**이라
       계기 시안으로 그리면 「무언가를 재고 있다」로만 읽힌다. 그때는 사건의 색(`red` =
       이미 벌어진 실패·죽음)이라야 자막을 가려도 무슨 일인지 보인다.
    🔴 대신 그 막대는 **뜻층 예산(한 프레임 두 색)을 먹는다.** 붉은 막대를 켜는 샷에서는
       다른 뜻색을 하나까지만 같이 켤 수 있다 — 모닥불을 프레임에서 빼는 이유가 그것이다."""
    import palette
    import stage
    if color in palette.MEANING:
        return stage.meaning_mat(color, strength=strength)
    return stage.need_mat(color, strength=strength)


def gauge(loc, w=GAUGE_W, h=GAUGE_H, color='instrument', col=None, dim=0.06):
    """막대 하나(슬라이더). 반환 dict: 홈(어두운 통)과 **채움**(밝은 막대).

    🔑 채움은 **바닥에서 자란다** — 가운데서 커지면 값이 아니라 숨쉬기로 보인다.
    🔴 홈을 빼지 마라. 채움만 있으면 「얼마나 찼나」가 아니라 「막대가 있다」로만 읽힌다."""
    import bpy
    import stage
    col = col or bpy.context.collection
    # 🔴 홈은 **그 색을 어둡게** 쓴다. 홈까지 시안으로 두면 막대 넷이 뜰 때 홈들이
    #    한 덩어리 시안 띠로 뭉쳐서 정작 색 구분이 안 된다.
    # 🔴 홈 밝기는 **색마다 다르게 필요하다.** 연두(#92FF5C) 처럼 밝은 색은 0.06 이어도
    #    빛을 받아 밝게 나와서, 비어 있는 막대가 **차 있는 것으로 읽힌다**(ep15s-4 에서 잡았다).
    dim_m = _bar_mat(color, dim)                # 홈은 더 어둡게 — 채움과 대비가 커진다
    lit = _bar_mat(color, 1.0)
    x, y, z = loc
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z + h / 2))
    track = bpy.context.object
    track.scale = (w, w * 0.55, h)
    track.data.materials.append(dim_m)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z))
    fill = bpy.context.object
    fill.scale = (w * 1.15, w * 0.75, 1e-4)
    fill.data.materials.append(lit)
    return {'track': track, 'fill': fill, 'base': z, 'h': h, 'w': w, 'k': 0.0, 'm': 1.0,
            'color': color}


def gauge_emph(g, m):
    """강조 배율. 🔑 **통째로 키운다** — 홈과 채움이 같은 비율로 커지므로 「얼마나 찼나」는
    안 바뀌고 크기만 커진다. 채움만 키우면 그건 값이 올라간 것으로 읽혀서 거짓말이 된다.
    🔴 대본이 그 수치를 부르는 동안에만 켠다(줄 시각은 timed.json 이 정본이다)."""
    g['m'] = max(1.0, m)


def gauge_mark(g, k, col=None, color=None):
    """막대에 **문턱 표시**를 단다(비율 k). 🔑 「얼마나 찼나」만으로는 못 말하는 것이 있다 —
    ep15s-3 의 6타일처럼 **넘으면 안 되는 선**이 있는 수치가 그렇다. 채움이 이 선을 넘는
    순간이 곧 사건이다.

    🔴 `color` 를 따로 줘라. 막대와 **같은 색**으로 두면 채움이 그 선을 지나는 순간
       선이 채움에 먹혀 사라진다 — 정작 봐야 할 그 순간에 안 보인다(ep15s-2 에서 잡았다).
       선은 값이 아니라 **규칙**이므로 계기 시안이 맞다."""
    import bpy
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0))
    m = bpy.context.object
    m.data.materials.append(_bar_mat(color or g['color'], 1.0))
    g['mark'], g['mark_k'] = m, max(0.0, min(1.0, k))
    return m


def gauge_at(g, x, y, z):
    """막대를 **매 프레임 여기에 놓는다.** 🔴 주민에게 묶지 마라 — 사람이 돌면 막대도 같이
    돌아서 화면에서 눕는다(실측으로 배웠다). 계기는 사람을 따라가되 **늘 화면을 본다.**"""
    g['base'] = z
    m, h, w = g['m'], g['h'] * g['m'], g['w'] * g['m']
    g['track'].scale = (w, w * 0.55, h)
    g['track'].location = (x, y, z + h / 2)
    g['fill'].scale = (w * 1.15, w * 0.75, max(h * g['k'], 1e-4))
    g['fill'].location = (x, y, z + h * g['k'] / 2)
    if g.get('mark') is not None:
        # 🔴 얇게 두면 **선으로 안 읽힌다.** 막대보다 확실히 넓고, 두께도 좀 있어야 한다
        g['mark'].scale = (w * 2.4, w * 1.1, w * 0.40)
        g['mark'].location = (x, y, z + h * g['mark_k'])


def gauge_set(g, k):
    """막대를 k(0~1)만큼 채운다. 🔑 실제 자리·크기는 `gauge_at` 이 매기므로 여기서는 값만 둔다."""
    g['k'] = max(0.0, min(1.0, k))


# ❌ **원판(다이얼)은 걷어냈다** (2026-08-13 사용자 판정: 「원으로 표현하는 데는 한계가
#    있는 것 같으니 막대기만 사용하자 — 네모로 원을 표현하니 둘의 차이가 안 보인다」).
#    조각 열넷으로 원을 흉내 내면 「얼마나 찼나」가 각도로만 남는데, 그 각도가 작은 화면에서
#    길이만큼 안 읽힌다. 값을 견주는 일은 **길이**가 이긴다.


# ── 판정 반경 — 땅에 그린 원 ────────────────────────────────────────
# 🔴 「6타일 안에서 마주쳐야 걸린다」는 **거리 조건**이다. 막대는 「지금 얼마나 먼가」를
#    말하지만 **어디까지가 안인가**는 못 말한다 — 그건 자리이고, 자리는 땅에 그려야 보인다.
# 🔑 조각으로 원을 그리는 것이 다이얼 때와 다른 이유: 다이얼은 「얼마나 찼나」를 **각도**로
#    말하려다 실패해서 걷어냈다. 이건 각도를 안 읽는다 — 안이냐 밖이냐만 읽는다.
# 🔴 계기층 규약은 그대로다. 바닥 물체에 **칠하지 않고** 2.2cm 위에 띄운다.
RING_SEG, RING_W, RING_Z = 44, 0.055, 0.022


def ring(r, color='instrument', seg=RING_SEG, w=RING_W):
    """반지름 r(m)짜리 판정 반경. 반환 dict: 조각들과 그 지역 좌표."""
    import bpy
    import stage
    mat = _bar_mat(color, 1.0)
    segs, local = [], []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        dx, dy = r * math.cos(a), r * math.sin(a)
        bpy.ops.mesh.primitive_cube_add(size=1, location=(dx, dy, RING_Z))
        o = bpy.context.object
        o.rotation_mode = 'XYZ'
        o.rotation_euler = (0, 0, a)
        # 🔑 조각을 **접선 방향으로 길게** 둔다. 정사각형이면 원이 아니라 점선 구슬 목걸이다
        o.scale = (w, 2 * math.pi * r / seg * 0.62, w * 0.5)
        o.data.materials.append(mat)
        segs.append(o)
        local.append((dx, dy))
    return {'segs': segs, 'local': local, 'r': r}


def ring_at(g, x, y):
    """매 프레임 여기에 놓는다 — 목수가 걸어가면 반경도 같이 간다(반경은 사람에게 붙는다)."""
    for o, (dx, dy) in zip(g['segs'], g['local']):
        o.location = (x + dx, y + dy, RING_Z)


def ring_show(g, on):
    for o in g['segs']:
        o.hide_render = not on


def attach(objs, arm, dz=0.0):
    """계기를 주민에게 **묶는다.** 안 묶으면 사람이 걸어가도 계기는 제자리에 남는다.
    🔴 역행렬은 `matrix_world` 가 아니라 **`matrix_basis`** 에서 뽑는다(personality_marks 와
       같은 함정 — 방금 만든 객체의 world 행렬은 아직 단위행렬이다)."""
    for o in objs:
        o.parent = arm
        o.matrix_parent_inverse = arm.matrix_basis.inverted()
        o.location.z += dz


# 주민 여섯이 일하는 자리 — 밭 위 3×2 덩어리.
# 🔑 흩어 놓지 않는다. 위에서 내려다볼 때 **똑같은 여섯이 각 잡고 늘어선 것**이
#    이 편의 사건 ①이고, 개발자 시점에서도 그게 먼저 읽혀야 한다.
def work_block(spot, gap=(0.95, 1.05)):
    return [(spot[0] + (i % 3 - 1) * gap[0], spot[1] + (i // 3 - 0.5) * gap[1])
            for i in range(6)]
