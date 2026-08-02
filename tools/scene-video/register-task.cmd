@echo off
REM Registers (or re-registers) the two scheduled tasks that drive the local
REM render half of the scene-video pipeline. Run this by double-clicking it,
REM or from any cmd window - it does not care where you are.
REM
REM ASCII only, same reason as routine.cmd: cmd.exe reads batch files in the
REM OEM codepage (949 here) and would choke on Korean comments.
REM
REM Why this file exists: the register commands used to live only in a comment,
REM so using them meant hand-editing an absolute path into them. That is exactly
REM the step people get wrong. %~dp0 already knows where the repo is.
setlocal

set "TASKCMD=%~dp0routine.cmd"

if not exist "%TASKCMD%" (
  echo ERROR: routine.cmd not found next to this script.
  echo        Expected: %TASKCMD%
  goto :fail
)

echo Target script:
echo   %TASKCMD%
echo.

REM Daily at 15:00. Must land AFTER the cloud routine pushes the script
REM (it starts 09:09 and pushes around 11:04). The old registration was
REM DAILY /mo 2 /st 09:00 and fired 9 minutes BEFORE the cloud run, so every
REM episode shipped a day late. Daily, not every-other-day: the two-day beat
REM belongs to the cloud routine now, and a second beat here only re-creates
REM the drift. See routine.cmd and routine-prompt.md for the full story.
echo [1/2] daily 15:00
schtasks /create /tn "AI_GOAP scene-video" /tr "\"%TASKCMD%\"" /sc DAILY /st 15:00 /f
if errorlevel 1 goto :fail

REM Catches the days the PC is off at 15:00. 3-minute delay so it does not
REM fight the desktop for CPU during logon.
echo.
echo [2/2] on logon (+3 min)
schtasks /create /tn "AI_GOAP scene-video logon" /tr "\"%TASKCMD%\"" /sc ONLOGON /delay 0003:00 /f
if errorlevel 1 goto :fail

echo.
echo Registered. Current state:
schtasks /query /fo TABLE /tn "AI_GOAP scene-video"
schtasks /query /fo TABLE /tn "AI_GOAP scene-video logon"
echo.
echo Done. Nothing renders until the next trigger - to render right now, run:
echo   node "%~dp0publish.mjs" --routine
echo.
pause
exit /b 0

:fail
echo.
echo FAILED. Common causes:
echo   - "Access is denied": run this from an Administrator cmd window.
echo   - The task already exists and is locked by another user account.
echo Inspect with: schtasks /query /fo LIST /v /tn "AI_GOAP scene-video"
echo.
pause
exit /b 1
