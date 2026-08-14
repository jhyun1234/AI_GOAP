# Blender 를 MCP 서버와 함께 띄운다 — blender-mcp.cmd 가 부른다.
# 애드온(blender_mcp_addon.py)을 켜고 9876 포트로 소켓 서버를 시작한다.
# Claude 쪽은 `claude mcp add blender -- cmd /c uvx blender-mcp` 로 등록돼 있다.
import bpy

def boot():
    try:
        bpy.ops.preferences.addon_enable(module='blender_mcp_addon')
        bpy.ops.wm.save_userpref()
        bpy.context.scene.blendermcp_port = 9876
        bpy.ops.blendermcp.start_server()
        print('[blender-mcp] server on :9876')
    except Exception as e:
        print('[blender-mcp] boot failed:', e)
    return None

bpy.app.timers.register(boot, first_interval=0.5)
