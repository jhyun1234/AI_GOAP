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
REM episode shipped a day late. Daily, and no interval gate here: the beat
REM belongs to the cloud routine, and a second beat here only re-creates the
REM drift. See routine.cmd and routine-prompt.md for the full story.
REM
REM 2026-08-03: the cloud routine now writes TWO scripts a day, not one.
REM This task deliberately stays at ONCE a day - do NOT add a second time.
REM publish.mjs --routine renders every pending script in one run (up to 4),
REM so one trigger already drains the backlog. Adding a second trigger would
REM give local and cloud two places to disagree about timing, which is exactly
REM the bug that shipped every episode a day late. See ADR-V-5 in the
REM retention spec under Docs/ (grep for ADR-V-5).
echo [1/2] daily 15:00
schtasks /create /tn "AI_GOAP scene-video" /tr "\"%TASKCMD%\"" /sc DAILY /st 15:00 /f
if errorlevel 1 goto :fail

REM Catches the days the PC is off at 15:00. 3-minute delay so it does not
REM fight the desktop for CPU during logon.
REM
REM *** NOT schtasks. *** (2026-08-05 - this line used to be
REM    `schtasks /create ... /sc ONLOGON /delay 0003:00 /f` and it failed with
REM    "Access is denied" from an ordinary shell.)
REM
REM    /sc ONLOGON is the ONLY mode here that demands elevation: schtasks
REM    registers a logon trigger for ANY user, which is a machine-wide change.
REM    /sc DAILY above goes through un-elevated in the very same shell, so the
REM    old :fail advice ("run this from an Administrator cmd window") sent
REM    people hunting for admin rights they never needed.
REM
REM    Register-ScheduledTask can scope the trigger to THIS account
REM    (-User <domain>\<user>), which is not a machine-wide change and needs no
REM    elevation. It also takes the exe path as its own field instead of a
REM    command-line string, so a repo path containing spaces cannot mis-quote.
REM
REM    The extra settings are deliberate and this task has them while the daily
REM    one does not: this trigger exists precisely for "the PC was off or asleep
REM    at 15:00", which on a laptop means it fires right after a battery boot.
REM    Task Scheduler's default is to refuse to start on battery and to stop if
REM    the machine switches to it - that default would gut the one task whose
REM    whole job is catching up.
echo.
echo [2/2] on logon (+3 min)
powershell -NoProfile -ExecutionPolicy Bypass -Command "try { $a = New-ScheduledTaskAction -Execute '%TASKCMD%'; $t = New-ScheduledTaskTrigger -AtLogOn -User ($env:USERDOMAIN + '\' + $env:USERNAME); $t.Delay = 'PT3M'; $s = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable; Register-ScheduledTask -TaskName 'AI_GOAP scene-video logon' -Action $a -Trigger $t -Settings $s -Force -ErrorAction Stop | Out-Null; Write-Host 'SUCCESS: registered AI_GOAP scene-video logon'; exit 0 } catch { Write-Host ('ERROR: ' + $_.Exception.Message); exit 1 }"
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
echo   - "Access is denied" on [1/2]: rare - /sc DAILY does not need elevation.
echo     Check whether a policy blocks Task Scheduler for this account, or
echo     whether the task is owned by a different user.
echo   - The task already exists and is locked by another user account.
echo   - [2/2] cannot find Register-ScheduledTask: the ScheduledTasks module is
echo     missing (very old Windows). Then, and only then, elevate and run:
echo       schtasks /create /tn "AI_GOAP scene-video logon" /tr "\"%TASKCMD%\"" /sc ONLOGON /delay 0003:00 /f
echo.
echo NOTE: do NOT reach for an Administrator window as the first move. Both
echo       steps here are designed to work from an ordinary shell - [2/2] scopes
echo       its trigger to this account precisely so it does not need elevation.
echo       If it is asking for admin, something else is wrong; read the message.
echo.
echo Inspect with:
echo   schtasks /query /fo LIST /v /tn "AI_GOAP scene-video"
echo   powershell -NoProfile -Command "Get-ScheduledTask | Where-Object TaskName -like '*scene-video*'"
echo.
pause
exit /b 1
