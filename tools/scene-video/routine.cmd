@echo off
REM Scheduled entry point for the scene-video pipeline. Not meant to be run by hand.
REM
REM ASCII only on purpose: cmd.exe reads batch files in the OEM codepage (949 here),
REM so Korean comments come out as garbage and the parser then tries to execute them.
REM The Korean explanation lives in publish.mjs, which node reads as UTF-8.
REM
REM ---------------------------------------------------------------------------
REM CADENCE: daily + on logon. The beat itself is NOT set here.
REM
REM Why: schedule.json says scriptBy=cloud, so the cloud routine owns the beat
REM (it gates on "0.5 days since the last scene.json in git" - one episode per
REM day since 2026-08-02, user's call; it used to be every other day / 1.5 days).
REM This machine has exactly one rule left - "if there is a script nobody
REM rendered yet, render it". publish.mjs skips the interval gate when cloud.
REM
REM When the hand-written order list runs out, backlog.mjs pulls the next
REM not-yet-filmed published blog post in. If there is none, everything stops
REM quietly and resumes by itself once the blog publishes again.
REM
REM The old registration was DAILY /mo 2 /st 09:00, which fired NINE MINUTES
REM before the cloud routine even started and ~2 hours before it pushed. So the
REM local side always saw yesterday's world and every episode shipped a day late.
REM Running daily at 15:00 puts this comfortably after the cloud run finishes,
REM and clear of the blog routine at 13:03. The logon task catches the days the
REM PC is off at 15:00.
REM
REM Firing with nothing to do is cheap: git pull, then publish.mjs prints
REM "dae-bon dae-gi" and exits 0 without touching TTS or Chromium.
REM
REM The logon task already existed (registered 2026-07-29, fires 3 minutes after
REM logon). Only the daily one moves. /f overwrites in place, so re-running both
REM lines is safe whether or not the task is already there.
REM
REM register:  run register-task.cmd next to this file - it fills in its own
REM            absolute path, so there is nothing to hand-edit. The two lines it
REM            runs are, for reference:
REM              schtasks /create /tn "AI_GOAP scene-video"       /tr "%~f0" /sc DAILY /st 15:00 /f
REM              schtasks /create /tn "AI_GOAP scene-video logon" /tr "%~f0" /sc ONLOGON /delay 0003:00 /f
REM remove:    schtasks /delete /tn "AI_GOAP scene-video" /f
REM            schtasks /delete /tn "AI_GOAP scene-video logon" /f
REM inspect:   schtasks /query  /tn "AI_GOAP scene-video" /v /fo LIST
REM            schtasks /query  /fo TABLE ^| findstr /i scene-video
REM ---------------------------------------------------------------------------
REM
REM This prepares only - it never uploads. See publish.mjs for why.
chcp 65001 >nul
cd /d "%~dp0..\.."
set LOG=tools\scene-video\state\routine.log
set LOCK=tools\scene-video\state\routine.lock

REM Overlap guard. Two triggers (daily + logon) can now collide, and a render
REM takes 10+ minutes - two of them writing the same build/ would corrupt the mp4.
REM mkdir is atomic on NTFS, so it doubles as the lock.
REM A lock older than 6 hours is stale (killed run, reboot mid-render) and is
REM removed - a lock that outlives its process must never wedge the pipeline shut.
powershell -NoProfile -Command "$l='%LOCK%'; if (Test-Path $l) { if (((Get-Date) - (Get-Item $l).CreationTime).TotalHours -gt 6) { Remove-Item -Recurse -Force $l } }" >nul 2>&1
mkdir "%LOCK%" 2>nul
if errorlevel 1 (
  echo.>> "%LOG%"
  echo ===== %DATE% %TIME% skipped - another run is in progress =====>> "%LOG%"
  exit /b 0
)

REM ---------------------------------------------------------------------------
REM DRAIN THE QUEUE, not "one per day"  (user's call, 2026-08-04)
REM
REM publish.mjs makes exactly ONE episode per call and exits 0 when there is
REM nothing left. This used to be called once, so one video a day was the hard
REM ceiling. That broke the moment long posts started being split: the cloud
REM writes one script a day, and a split turns one source post into two
REM episodes, so the video queue gains 1 net per split and never catches up.
REM ep04s and ep05s each became two parts - four episodes were already backed up.
REM
REM So we call it PERRUN times. Extra calls are cheap when the queue is empty:
REM one git pull, a "nothing to make" line, exit 0. A render is ~10 minutes, so
REM PERRUN=3 is worst-case ~30 minutes; routine.lock still guards overlap.
REM
REM PERRUN mirrors state/schedule.json "perRun". cmd.exe cannot read JSON without
REM help, so it is duplicated here on purpose - if you change one, change both.
REM The JSON copy is the one humans and the cloud routine read.
REM ---------------------------------------------------------------------------
set PERRUN=3

REM ---------------------------------------------------------------------------
REM WHY A :label SUBROUTINE AND NOT A for-BLOCK BODY  (fixed 2026-08-05)
REM
REM The loop used to inline the three commands inside the for-block parens.
REM Two things went wrong at once and between them the log went blank:
REM
REM   1. The ")" in `echo (exit %ERRORLEVEL%)` closed the for-block early, so
REM      the trailing `>> "%LOG%"` became a redirect on the WHOLE for command.
REM      That is why the log said "(exit 0" with the paren shorn off.
REM   2. cmd then had the outer for holding one append handle to the log while
REM      the inner echo/node lines opened their own. The outer handle's file
REM      position never advanced past what the inner ones wrote, so every inner
REM      line was overwritten by the next outer line. The 2026-08-05 15:00 run
REM      lost ALL of publish.mjs's output - three "(exit 0" lines were the only
REM      survivors, and the pipeline looked silent when it was merely unlogged.
REM
REM A subroutine has no enclosing parens, so ")" is literal again and each
REM redirect opens, writes, and closes on its own line. That is exactly how
REM this worked before PERRUN was introduced on 2026-08-04.
REM %ERRORLEVEL% also expands per-line here - inside a block it would need
REM delayed expansion and would have read stale on every pass.
REM ---------------------------------------------------------------------------
echo.>> "%LOG%"
echo ===== %DATE% %TIME% (up to %PERRUN% episodes) =====>> "%LOG%"
for /L %%i in (1,1,%PERRUN%) do call :pass %%i
rmdir /s /q "%LOCK%" 2>nul
exit /b 0

:pass
echo --- pass %1 of %PERRUN% >> "%LOG%"
node "tools\scene-video\publish.mjs" --routine >> "%LOG%" 2>&1
echo (exit %ERRORLEVEL%)>> "%LOG%"
exit /b 0
