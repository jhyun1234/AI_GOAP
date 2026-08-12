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
CENTER = (-0.60, 1.00)


def cells(cols, rows):
    """왼쪽 위부터 행 우선. `scene.json` 의 litIdx 가 세는 순서와 같아야 한다."""
    ox, oy = CENTER
    return [(ox + (c - (cols - 1) / 2) * PITCH,
             oy + ((rows - 1) / 2 - r) * PITCH)
            for r in range(rows) for c in range(cols)]


def build(cols, rows, col=None):
    """빈 칸 cols×rows 개를 공중에 띄운다. 반환: [obj, ...] (cells 와 같은 순서).

    🔴 칸을 **꽉 찬 판으로 만들지 마라.** 30 개를 발광 면으로 채우면 화면 윗쪽이
       눈 아픈 시안 덩어리가 된다(stage.meaning_mat 주석의 실측과 같은 함정).
       테두리만 남긴 빈 칸이라야 「아직 아무것도 안 든 할 일」로도 읽힌다."""
    import bpy
    import stage
    col = col or bpy.context.collection
    mat = stage.instrument_mat(strength=1.0)
    bar = CELL / 2
    out = []
    for x, y in cells(cols, rows):
        bpy.ops.object.empty_add(location=(x, y, Z))     # 칸 하나 = 막대 넷의 부모
        cell = bpy.context.object
        for dx, dy, sx, sy in ((0, bar, CELL, 0.045), (0, -bar, CELL, 0.045),
                               (bar, 0, 0.045, CELL), (-bar, 0, 0.045, CELL)):
            bpy.ops.mesh.primitive_cube_add(size=1, location=(x + dx, y + dy, Z))
            b = bpy.context.object
            b.scale = (sx, sy, 0.045)
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


# 주민 여섯이 일하는 자리 — 밭 위 3×2 덩어리.
# 🔑 흩어 놓지 않는다. 위에서 내려다볼 때 **똑같은 여섯이 각 잡고 늘어선 것**이
#    이 편의 사건 ①이고, 개발자 시점에서도 그게 먼저 읽혀야 한다.
def work_block(spot, gap=(0.95, 1.05)):
    return [(spot[0] + (i % 3 - 1) * gap[0], spot[1] + (i // 3 - 0.5) * gap[1])
            for i in range(6)]
