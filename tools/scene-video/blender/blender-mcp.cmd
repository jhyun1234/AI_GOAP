@echo off
rem Blender + MCP 서버 기동. Claude 세션에서 mcp__blender__* 도구가 이 창에 붙는다.
start "" "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --python "%~dp0start-mcp.py"
