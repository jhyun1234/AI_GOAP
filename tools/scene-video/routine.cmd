@echo off
REM Scheduled entry point for the scene-video pipeline. Not meant to be run by hand.
REM
REM ASCII only on purpose: cmd.exe reads batch files in the OEM codepage (949 here),
REM so Korean comments come out as garbage and the parser then tries to execute them.
REM The Korean explanation lives in publish.mjs, which node reads as UTF-8.
REM
REM register:  schtasks /create /tn "AI_GOAP scene-video" /tr "%~f0" /sc DAILY /mo 2 /st 09:00 /sd 2026/07/29 /f
REM remove:    schtasks /delete /tn "AI_GOAP scene-video" /f
REM inspect:   schtasks /query  /tn "AI_GOAP scene-video" /v /fo LIST
REM
REM This prepares only - it never uploads. See publish.mjs for why.
chcp 65001 >nul
cd /d "%~dp0..\.."
set LOG=tools\scene-video\state\routine.log
echo.>> "%LOG%"
echo ===== %DATE% %TIME% =====>> "%LOG%"
node "tools\scene-video\publish.mjs" --routine >> "%LOG%" 2>&1
echo (exit %ERRORLEVEL%)>> "%LOG%"
