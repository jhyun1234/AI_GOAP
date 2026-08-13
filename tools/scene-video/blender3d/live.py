"""GUI 블렌더에 회차 샷을 띄운다 — 디스크에 굽지 않는다.

BlenderMCP 로 붙은 블렌더 창에서 회차를 눈으로 보고 고치기 위한 로더다.
렌더 스크립트를 **그대로 실행한다**. 스크립트 끝의 `stage.bake()` 가 `LIVE` 일 때는
굽지 않고 프레임 핸들러만 걸어 주므로, 타임라인 재생·스크럽이 **렌더와 같은 코드**를 돈다.

    import live
    ns = live.show('render_body1.py')     # 띄우고 첫 프레임
    live.frame(ns, 3.4)                   # 초 단위로 아무 데나
    live.frame(ns, 90, fps=False)         # 프레임 번호로

그리고 블렌더에서 그냥 재생(Space)하면 돈다.

🔴 `bpy.ops.wm.read_factory_settings()` 를 GUI 에서 부르면 안 된다.
   환경설정을 공장 초기화하면서 **BlenderMCP 애드온을 꺼서 연결이 끊긴다.**
   헤드리스는 `--factory-startup` 으로 돌기 때문에 이 함정이 안 드러난다.
   그래서 아래에서 그 호출을 `_wipe()` 로 갈아 끼운다.

🔴 렌더 스크립트는 `bpy.context.object` 를 쓴다(village·unison·stage).
   MCP 애드온이 코드를 부르는 컨텍스트에는 그 속성이 아예 없다 —
   3D 뷰포트 컨텍스트를 씌워서 실행한다.
"""
import bpy, os, sys

DIR = os.path.dirname(os.path.abspath(__file__))
if DIR not in sys.path:
    sys.path.insert(0, DIR)

WIPED = 'bpy.ops.wm.read_factory_settings(use_empty=True)'


def _wipe():
    """씬을 비운다. read_factory_settings 를 대신하되 애드온은 안 건드린다."""
    # 죽은 씬을 가리키는 프레임 핸들러부터 뗀다
    for h in list(bpy.app.handlers.frame_change_pre):
        if getattr(h, '_stage_draw', False):
            bpy.app.handlers.frame_change_pre.remove(h)

    # ponytail: 고아 데이터(메시·재질)는 안 치운다. 안 보이고 안 렌더된다.
    # 한 세션에서 회차를 여러 번 갈아 끼워 메모리가 문제되면 orphans_purge 를 붙여라.
    #
    # 🔴 `list(...)` 로 찍어 두고 지우면 안 된다. 하나를 지우는 것이 다른 것을 딸려 지워서
    #    찍어 둔 목록에 죽은 포인터가 남는다("StructRNA of type Object has been removed").
    while bpy.data.objects:
        bpy.data.objects.remove(bpy.data.objects[0], do_unlink=True)
    while bpy.data.collections:
        bpy.data.collections.remove(bpy.data.collections[0])

    # 🔴 모듈 전역 캐시를 같이 비운다. `stage._rig_src` 는 리그 원본 **객체**를 들고 있는데
    #    위에서 그것도 지웠다 — 안 비우면 다음 판이 죽은 객체를 복사하려다 터진다.
    #    헤드리스는 매번 새 프로세스라 캐시가 늘 비어 있어서 이 함정이 안 드러난다.
    #    `village._MATS` 는 재질이라 여기서 안 지운다 — 위 정리가 재질은 안 건드린다.
    import stage
    stage._rig_src = None


def _view3d():
    win = bpy.context.window_manager.windows[0]
    area = next(a for a in win.screen.areas if a.type == 'VIEW_3D')
    region = next(r for r in area.regions if r.type == 'WINDOW')
    return win, area, region


def _frame_only(space, overlays):
    """카메라 프레임 밖을 까맣게 덮는다.

    🔴 뷰포트는 렌더보다 넓다(창 1574×888 vs 프레임 970×846). 덮지 않으면 프레임 **밖**
       까지 보이는 채로 구도를 판정하게 된다 — 실제 화면에 없는 것을 보고 고치는 셈이다.
    🔑 가리는 것(passepartout)은 오버레이라서 오버레이를 끄면 같이 꺼진다. 그래서 오버레이는
       켜 두고 격자·축·기즈모만 끈다.
    """
    ov = space.overlay
    ov.show_overlays = True
    space.show_gizmo = False
    for attr in ('show_floor', 'show_axis_x', 'show_axis_y', 'show_axis_z', 'show_cursor',
                 'show_object_origins', 'show_text', 'show_stats', 'show_relationship_lines',
                 'show_outline_selected', 'show_bones', 'show_motion_paths'):
        if hasattr(ov, attr):
            setattr(ov, attr, False)
    ov.show_extras = overlays          # 카메라·조명 아이콘. 프레임 밖이라 어차피 덮인다
    cam = bpy.context.scene.camera.data
    cam.show_passepartout = True
    cam.passepartout_alpha = 1.0


def frame(ns, at, fps=True):
    """프레임 하나를 세운다. `at` 은 초, `fps=False` 면 프레임 번호."""
    fi = round(at * ns['FPS']) if fps else int(at)
    fi = max(0, min(fi, ns['NF'] - 1))
    bpy.context.scene.frame_set(fi)
    _view3d()[1].tag_redraw()      # 🔴 안 하면 스크린샷이 **직전에 그려진 화면**을 준다
    return fi


def show(script, at=0.0, shading='RENDERED', overlays=False):
    """`script` 를 굽지 않고 실행해 씬을 세우고 뷰포트를 카메라 시점으로 맞춘다.

    반환값은 스크립트의 전역 이름공간이다 — `ns['arms']`, `ns['cam']`, `ns['draw']` 로
    스크립트가 만든 것을 그대로 집어서 이어 만질 수 있다.
    """
    import stage
    src = open(os.path.join(DIR, script), encoding='utf-8').read()
    if WIPED not in src:
        raise RuntimeError('%s 가 `%s` 로 시작하지 않는다' % (script, WIPED))
    src = src.replace(WIPED, '_live_wipe()')

    ns = {'__name__': '__live__', '__file__': os.path.join(DIR, script),
          '_live_wipe': _wipe}
    win, area, region = _view3d()
    stage.LIVE = True                          # bake() 가 굽지 않고 핸들러만 건다
    try:
        with bpy.context.temp_override(window=win, area=area, region=region):
            exec(compile(src, script, 'exec'), ns)
    finally:
        stage.LIVE = False

    fi = frame(ns, at)
    space = area.spaces[0]
    space.region_3d.view_perspective = 'CAMERA'
    # 🔴 shading 은 'RENDERED' 여야 한다. 'MATERIAL' 은 씬 조명 대신 스튜디오 HDRI 를 쓰는데,
    #    이 파이프라인의 알베도는 SUN 키라이트 + EXPOSURE 를 재서 맞춘 값이라(README 손잡이 둘)
    #    스튜디오 조명 아래에서는 거의 검게 보인다.
    space.shading.type = shading
    _frame_only(space, overlays)
    for o in bpy.context.scene.objects:
        o.select_set(False)
    area.tag_redraw()
    print('[live] %s  frame %d/%d  (%.2f초 / %.2f초)'
          % (script, fi, ns['NF'] - 1, fi / ns['FPS'], ns['DUR']))
    return ns
