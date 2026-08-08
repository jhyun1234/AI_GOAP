@echo off
rem == Unattended capture session (longform track W3, ADR-LF-4) ==
rem Usage: capture-session.cmd [minutes]   (default 30)
rem
rem ASCII only in this file - cmd.exe parses batch files in the OEM codepage
rem (CP949 here), so UTF-8 multibyte text corrupts the parser (proven twice
rem on 2026-08-09). Korean docs: Docs/longform spec, section W3.
rem
rem Opens Unity with GUI (NO -batchmode: GameView capture needs graphics),
rem CaptureSessionRunner.RunCli enters Play, records continuously, marks cuts,
rem stops and quits by itself. This window may be closed right away.
rem Output: D:\AI_GOAP-videos\clips\<yymmdd-hhmm>\ (raw.mp4 + cuts.json)
rem NOTE: fails if Unity Editor already has this project open (lock).

setlocal
set MINUTES=%~1
if "%MINUTES%"=="" set MINUTES=30

set PROJ=%~dp0..\..
set UNITY="C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe"
if not exist %UNITY% (
  echo [capture] Unity 6000.4.7f1 not found: %UNITY%
  echo           Fix the UNITY path in this file.
  exit /b 1
)

echo [capture] starting %MINUTES% min session - output: D:\AI_GOAP-videos\clips\
start "" %UNITY% -projectPath "%PROJ%" ^
  -executeMethod AIVillage.M0.Capture.Editor.CaptureSessionRunner.RunCli ^
  -captureMinutes %MINUTES%
endlocal
