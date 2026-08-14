"""마을만(주민 없이) 한 장. test_village 가 **렌더된 픽셀**로 판정한다.
   blender --background --factory-startup --python render_village_probe.py

🔴 16진값의 채도로 판정하면 안 된다. 감마 곡선이 어두운 쪽에서 채도를 키워서, 채도 0.21
   짜리 흙색이 그림자 진 지붕에서 0.25 로 올라온다 — 표에서는 세계층인데 화면에서는
   뜻층으로 잡힌다. 그래서 **찍어서** 본다."""
import bpy, sys, os

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stage, village

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'fixtures', 'village_probe.png')
os.makedirs(os.path.dirname(OUT), exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
village.build()
cam = stage.light_camera()
stage.aim(cam, (7.0, -8.6, 4.4), (0, 1.2, 0.6))     # 지붕이 키라이트를 정면으로 받는 자리
bpy.context.scene.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print('[village-probe] wrote', OUT)
