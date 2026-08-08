@echo off
rem ── 무인 촬영 세션 시작 (롱폼 트랙 W3, ADR-LF-4) ──────────────────────
rem 사용: capture-session.cmd [분]           (기본 30)
rem
rem Unity 를 GUI 로 열어(배치모드 금지 — GameView 캡처가 그래픽스를 요구한다)
rem CaptureSessionRunner.RunCli 가 Play 진입 → Recorder 상시 녹화 → 사건 컷 마킹 →
rem 계획 시간 뒤 스스로 정지·종료까지 한다. 이 창은 바로 닫혀도 된다 —
rem 이후는 Unity 혼자 돌고, 산출물은 D:\AI_GOAP-videos\clips\<날짜-시각>\ 에 쌓인다.
rem ⚠️ Unity Editor 가 이미 열려 있으면 실행되지 않는다(프로젝트 잠금) — 닫고 실행할 것.

setlocal
set MINUTES=%~1
if "%MINUTES%"=="" set MINUTES=30

set PROJ=%~dp0..\..
set UNITY="C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe"
if not exist %UNITY% (
  echo [capture] Unity 6000.4.7f1 를 못 찾았다: %UNITY%
  echo           Unity Hub 설치 경로를 확인해 이 파일의 UNITY 를 고칠 것.
  exit /b 1
)

echo [capture] %MINUTES%분 촬영 세션 시작 — 산출물: D:\AI_GOAP-videos\clips\
start "" %UNITY% -projectPath "%PROJ%" ^
  -executeMethod AIVillage.M0.Capture.Editor.CaptureSessionRunner.RunCli ^
  -captureMinutes %MINUTES%
endlocal
