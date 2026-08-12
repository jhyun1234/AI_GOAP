# 3D 무대 조립 — 회차 샷 스크립트가 공통으로 부른다.
#
# 여기 있는 상수는 전부 **실측으로 맞춘 값**이다. 눈대중으로 고치지 마라 —
# 아래 §함정 넷은 전부 한 번씩 실제로 밟은 것이다.
#
# ── 함정 넷 (2026-08-12 · ep15s-1 훅에서 전부 밟았다) ──────────────
# ① GLB 임포트 객체는 `rotation_mode` 가 QUATERNION 이라 `rotation_euler` 가 **무시된다.**
#    안 바꾸면 인물이 전부 옆을 본 채 렌더된다.
# ② Blender 5.2 는 `Material.use_nodes` 를 아직 본다(6.0 제거 예정 경고만 뜬다).
#    안 켜면 노드 트리를 무시하고 **기본 회색 0.8** 로 렌더된다.
# ③ 바닥을 거의 눕혀서 보면 **스치는 각도의 프레넬 정반사**가 알베도를 압도한다.
#    알베도를 0.035 까지 떨어뜨려도 바닥이 밝다. `Specular IOR Level = 0` 로 끈다.
# ④ `view_transform` 이 Standard(롤오프 없음)라 **광량을 올리면 바로 흰색으로 클리핑된다.**
#    「어두우니 더 밝게」로 올리다 인물이 뭉개지고, 그 상태에서 어두운 바닥이 중간 회색으로
#    올라온다. 광량은 눈이 아니라 **픽셀을 재서** 맞춘다.
import bpy, math, os, sys
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import palette

# 🔴 코드와 산출물을 가른다(설계 §7-2). 모델 원본은 **리포**에 있고, 만들어지는 것은
#    전부 D 드라이브다. 경로를 절대값으로 박지 않는다 — 리포가 옮겨지면 그 자리가 틀린다.
#    OUT_ROOT 는 serve.js 의 SCENE_3D_ROOT 와 같은 환경변수를 본다(한쪽만 바뀌면 어긋난다).
SV_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))   # tools/scene-video
MODEL = os.path.join(SV_ROOT, 'blender', 'Shorts.blend')
MODEL_OBJ = 'geometry_0'
OUT_ROOT = os.environ.get('SCENE_3D_ROOT', r'D:\AI_GOAP-videos\3d')

# 쇼츠 1920 중 자막·HUD 를 뺀 안전 띠(y 420~1266). 엔진은 이 그림을 y=420 에 그대로 놓는다.
BAND_W, BAND_H, BAND_Y = 1080, 846, 420

# 🔑 조정 손잡이 둘.
#    EXPOSURE — 인물의 밝은 면이 sRGB 0.85 근처에 앉는 값. 1.85 에서 실측 218/255, 클리핑 0px.
#    LANE_LEVEL — 길 알베도. EXPOSURE 로 나눠 써서 **노출을 바꿔도 길 밝기는 안 변한다.**
#                 우리 2D 트랙(흰색 12% ≈ sRGB 0.17)과 같은 밝기가 되게 맞춘 값이다.
EXPOSURE = 1.85
LANE_LEVEL = 0.015
DEPTH_STEP = 0.085         # 뒤로 한 칸 갈 때마다 어두워지는 비율(lib.js DEPTH 명도 계단)


def _dimmed(mat, k):
    """같은 텍스처를 k 배로 어둡게 한 사본. 안개 대신 계단으로 준다 — 우리 팔레트가 계단이다."""
    m = mat.copy()
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    link = bsdf.inputs['Base Color'].links[0]
    mix = nt.nodes.new('ShaderNodeMixRGB')
    mix.blend_type = 'MULTIPLY'
    mix.inputs['Fac'].default_value = 1.0
    mix.inputs['Color2'].default_value = (k, k, k, 1)
    nt.links.new(link.from_socket, mix.inputs['Color1'])
    nt.links.new(mix.outputs['Color'], bsdf.inputs['Base Color'])
    return m


def build(n=6, lane_gap=1.30, lane_len=9.0, lane_w=0.70):
    """주민 n 명과 그들이 선 길 n 개. 반환: (figures, lanes, camera).

    인물은 **메시를 공유한다** — 여섯이 같은 몸이라는 것이 이 회차의 뜻이다.
    다른 것은 깊이에 따른 명도뿐이다."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    col = bpy.context.collection

    with bpy.data.libraries.load(MODEL, link=False) as (_src, dst):
        dst.objects = [MODEL_OBJ]
    base = dst.objects[0]
    base.parent = None
    base.matrix_parent_inverse.identity()
    src_mat = base.data.materials[0]

    lane_mat = bpy.data.materials.new('lane')
    lane_mat.use_nodes = True                      # ← 함정 ②
    lb = lane_mat.node_tree.nodes['Principled BSDF']
    lv = LANE_LEVEL / EXPOSURE
    lb.inputs['Base Color'].default_value = (lv, lv * 1.07, lv * 1.27, 1)
    lb.inputs['Roughness'].default_value = 1.0
    for nm in ('Specular IOR Level', 'Specular'):  # ← 함정 ③
        if nm in lb.inputs:
            lb.inputs[nm].default_value = 0.0

    figures, lanes = [], []
    for i in range(n):
        y = i * lane_gap
        bpy.ops.mesh.primitive_plane_add(size=1, location=(0, y, 0))
        lane = bpy.context.object
        lane.scale = (lane_len, lane_w, 1)
        lane.data.materials.append(lane_mat)
        lanes.append(lane)

        o = base.copy()
        o.rotation_mode = 'XYZ'                    # ← 함정 ①
        o.location = (0, y, 0.5)                   # 원점이 몸 한가운데 — 반 키만큼 올려야 발이 바닥
        o.rotation_euler = (0, 0, math.radians(90))  # 정면(-X)을 카메라(-Y) 쪽으로
        col.objects.link(o)
        o.material_slots[0].link = 'OBJECT'
        o.material_slots[0].material = _dimmed(src_mat, 1.0 - DEPTH_STEP * i)
        figures.append(o)

    bpy.data.objects.remove(base, do_unlink=True)

    cam = light_camera(col)
    return figures, lanes, cam


def light_camera(col=None, res=(BAND_W, BAND_H)):
    """빛 셋과 카메라, 그리고 렌더 설정. 이미 열려 있는 .blend 위에도 얹을 수 있게 떼어 뒀다."""
    sc = bpy.context.scene
    col = col or bpy.context.collection

    # ── 빛 — 언제나 화면 왼쪽 위(인계 문서 §3) ────────────
    for name, energy, size, loc, rot in (
        ('key',  2000 * EXPOSURE, 7,  (-6.0, -5.0, 7.0), (40, 0, -40)),
        ('rim',   280 * EXPOSURE, 6,  (5.5, 6.0, 3.4),   (108, 0, 150)),
        ('fill',   80 * EXPOSURE, 10, (4.5, -6.0, 1.8),  (80, 0, 40)),
    ):
        d = bpy.data.lights.new(name, 'AREA'); d.energy = energy; d.size = size
        ob = bpy.data.objects.new(name, d); col.objects.link(ob)
        ob.location = loc
        ob.rotation_euler = tuple(math.radians(a) for a in rot)

    cam_d = bpy.data.cameras.new('cam'); cam_d.lens = 45
    cam = bpy.data.objects.new('cam', cam_d); col.objects.link(cam)
    sc.camera = cam

    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.film_transparent = True              # 엔진이 PALETTE.bg 위에 합성한다
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA'
    sc.view_settings.view_transform = 'Standard'   # ← 함정 ④ 와 한 몸. 필믹이면 팔레트가 밀린다
    sc.render.engine = 'BLENDER_EEVEE'             # 5.2 의 enum 은 EEVEE 하나다
    sc.eevee.taa_render_samples = 64
    return cam


def aim(cam, loc, at):
    cam.location = loc
    cam.rotation_euler = (Vector(at) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()


# ── 팔레트 3층 (설계 §2) ──────────────────────────────
# 4색 규약(bg/ink/accent/sub)은 **어두운 배경 위 평면 도형**을 위해 만든 것이었다. 그 규약이
# 하던 일(요소를 서로 떼어놓기)을 3D 에서는 빛과 형태가 이미 한다. 그래서 색을 늘리되
# **뜻 없는 색은 안 늘린다** — 중구난방은 색이 많아서가 아니라 색에 규칙이 없어서 생긴다.
#
#   세계층  무채색 + earth  — 지형·건물·나무·주민 몸. **예산에 안 들어간다**
#   뜻층    다섯            — **한 프레임에 최대 둘.** 이것이 유일한 방어선이다
#   계기층  instrument      — 마을 위에 뜨는 수치·표 전용. 세계 물체에 절대 안 쓴다
#
# 🔴 색을 이 표 밖에서 만들지 마라. 그리고 **표는 palette.py 하나뿐이다** — 여기서
#    다시 적으면 갈라진다. palette.py 는 bpy 를 안 만져서 테스트·게이트도 같은 표를 읽는다.
PALETTE = palette.LINEAR
MEANING = palette.MEANING

ACCENT_LIN = PALETTE['green']      # 옛 이름 — 기존 샷 스크립트가 아직 쓴다
INK_LIN = (0.62, 0.62, 0.63)       # 인물 알베도와 같은 대역 — 흰 도형이 인물보다 튀지 않게


def _mat(name, base, emit=None, strength=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True                              # ← 함정 ②
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (*base, 1)
    b.inputs['Roughness'].default_value = 1.0
    for nm in ('Specular IOR Level', 'Specular'):   # ← 함정 ③
        if nm in b.inputs:
            b.inputs[nm].default_value = 0.0
    if emit:
        b.inputs['Emission Color'].default_value = (*emit, 1)
        b.inputs['Emission Strength'].default_value = strength
    return m


def ink_mat(k=1.0):
    """흰 도형 — 아직 켜지지 않은 것."""
    return _mat('ink', tuple(c * k for c in INK_LIN))


def world_mat(albedo=(0.06, 0.06, 0.065), earth=False):
    """세계층 — 지형·건물·나무·주민 몸. 유채색 예산에 안 들어간다."""
    return _mat('world', PALETTE['earth'] if earth else albedo)


def meaning_mat(name, strength=1.0, albedo_scale=0.10):
    """뜻층 다섯 중 하나.

    🔴 **strength 는 1.0 을 넘을 수 없다.** 넘기면 클리핑이 밝기가 아니라 **색상 자체를
       파괴한다** — 팔레트 다섯을 2.2 로 찍어 봤더니 앰버는 노랑, 한기는 시안, 보라는
       마젠타로 밀려 probe 가 다섯 중 둘만 알아봤다(초록·적색만 살아남는데, 그 둘은
       한 채널이 0 이라 클리핑돼도 색상각이 안 움직이기 때문이다).
       view_transform 이 Standard 라 **strength 1.0 이면 화면에 정확히 그 16진값이 나온다** —
       더 올릴 이유가 없다.
    🔴 넓은 면에는 1.0 도 쓰지 마라. 화면 3분의 1을 덮는 면을 최대 밝기로 발광시키면 눈이
       아픈 덩어리가 된다(실측). 넓은 면은 albedo_scale 을 0.35 근처로 올리고
       strength 를 0.5 근처로 내려 **빛을 받게** 해라 — 그래야 형태가 산다."""
    assert name in MEANING, '뜻층이 아니다: %s' % name
    assert 0.0 <= strength <= 1.0, 'strength 1.0 초과는 색상을 파괴한다: %s' % strength
    c = PALETTE[name]
    return _mat('meaning_' + name, tuple(v * albedo_scale for v in c), emit=c, strength=strength)


def instrument_mat(strength=1.0):
    """계기층 — 마을 **위에 뜨는** 수치·표 전용. 세계 물체에 붙이지 마라.
    그래야 초록·한기와 안 헷갈린다(계기는 물체에 안 붙고 공중에만 뜬다)."""
    return _mat('instrument', (0.0, 0.05, 0.06), emit=PALETTE['instrument'], strength=strength)


def gate(objs, on):
    """게이트가 0 이면 **렌더에서 뺀다.**
    🔴 스케일 0 이나 발광 0 으로 끄면 안 된다 — 납작해진 채 밑색이 그대로 보여서, 흰색만
       있어야 할 프레임에 강조색이 남는다(샷4 첫 판에서 실제로 그랬다). 규약은
       「흰색과 강조색을 같은 픽셀에 안 겹친다」이고, 그건 안 보이는 것까지 포함이다."""
    for o in objs:
        o.hide_render = not on


RIG_BLEND = os.path.join(OUT_ROOT, 'models', 'villager_rigged.blend')   # 산출물이다 — D 드라이브
_rig_src = None


def rigged(col=None, loc=(0, 0, 0), rot_z=90):
    """리깅된 주민 한 명. 반환 (mesh, armature).

    포즈는 `arm.pose.bones[이름].rotation_euler` — **X 앞뒤 · Z 좌우 · Y 비틀림**(rig.py).
    🔴 여럿을 세울 때도 **아마추어는 한 명당 하나**여야 한다. 메시 여럿을 한 아마추어에
       묶으면 전부 그 아마추어 자리로 끌려간다(변형이 아마추어 공간에서 계산된다).
       메시 데이터는 공유하므로 늘어나는 것은 객체 헤더뿐이다."""
    global _rig_src
    col = col or bpy.context.collection
    if _rig_src is None:
        with bpy.data.libraries.load(RIG_BLEND, link=False) as (_s, dst):
            dst.objects = ['villager', 'villager_rig']
        m0 = next(o for o in dst.objects if o.type == 'MESH')
        a0 = next(o for o in dst.objects if o.type == 'ARMATURE')
        _rig_src = (m0, a0)

    m0, a0 = _rig_src
    arm = a0.copy(); arm.data = a0.data.copy()       # 포즈는 객체별이라 데이터도 복사한다
    mesh = m0.copy()                                 # 메시 데이터는 공유
    col.objects.link(arm); col.objects.link(mesh)
    mesh.parent = arm
    mesh.matrix_parent_inverse.identity()
    mesh.location = (0, 0, 0)
    for md in mesh.modifiers:
        if md.type == 'ARMATURE':
            md.object = arm
    arm.rotation_mode = 'XYZ'
    arm.location = loc
    arm.rotation_euler = (0, 0, math.radians(rot_z))
    for pb in arm.pose.bones:
        pb.rotation_mode = 'XYZ'
    return mesh, arm


def pose(arm, spec):
    """{뼈이름: (x, y, z) 라디안} 을 통째로 적용한다. 안 적힌 뼈는 0 으로 되돌린다."""
    for pb in arm.pose.bones:
        pb.rotation_euler = (0, 0, 0)
    for name, rot in spec.items():
        arm.pose.bones[name].rotation_euler = rot
