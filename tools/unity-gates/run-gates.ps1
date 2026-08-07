<#
.SYNOPSIS
  EditMode 게이트 러너. Editor 락을 먼저 판정하고, 결과는 XML 로만 판정한다.

.DESCRIPTION
  이 스크립트가 존재하는 이유는 두 가지 함정 때문이다.

  ① `-quit` 함정 — 예전 실행법에 `-quit` 을 붙이면 Unity 가 테스트를 돌리기 전에 종료하는데
     exit code 는 0 이라 "게이트 green" 으로 읽힌다. 그래서 이 스크립트는 `-quit` 을 붙이지
     않고, **성공/실패 판정을 exit code 가 아니라 결과 XML 의 집계로만** 한다.
     (Unity 가 비정상 종료해도 XML 이 없으면 실패로 본다.)

  ② Editor 락 — Editor 가 열려 있으면 batchmode 가 프로젝트를 못 열어 exit 1 이 난다.
     MCP for Unity 를 쓰는 동안에는 Editor 가 켜져 있는 게 정상이므로, 이 스크립트는
     락을 먼저 감지해 무슨 일이 일어날지 알려주고, -CloseEditor 를 줬을 때만 닫는다.
     -ReopenEditor 를 함께 주면 테스트 후 Editor 를 다시 띄운다 (MCP 세션 복귀용).

  Windows PowerShell 5.1 호환으로 작성했다 (?? / ?: / ?. 연산자 사용 금지).

.EXAMPLE
  # Editor 가 닫혀 있을 때 (기본)
  powershell -File tools\unity-gates\run-gates.ps1

.EXAMPLE
  # Editor 를 켜 둔 채 작업 중일 때 — 닫고 돌리고 다시 켠다
  powershell -File tools\unity-gates\run-gates.ps1 -CloseEditor -ReopenEditor

.EXAMPLE
  # 특정 게이트만
  powershell -File tools\unity-gates\run-gates.ps1 -Filter M21_T7
#>
[CmdletBinding()]
param(
    # 테스트 이름 필터 (NUnit --testFilter). 비우면 EditMode 전체.
    [string] $Filter,

    # Editor 가 열려 있으면 닫고 진행한다. 주지 않으면 안내만 하고 중단.
    [switch] $CloseEditor,

    # 테스트가 끝난 뒤 Editor 를 다시 띄운다 (MCP for Unity 세션 복귀).
    [switch] $ReopenEditor,

    # Unity 종료 대기 상한 (초)
    [int] $CloseTimeoutSec = 60,

    # 테스트 실행 대기 상한 (초)
    [int] $RunTimeoutSec = 900
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

# ⚠️ PATH 를 레지스트리에서 새로 읽는다. 자식 프로세스는 부모의 환경을 물려받으므로, 이 스크립트를
# 오래된 셸(=uv 설치 전에 뜬 셸)에서 돌리면 여기서 띄우는 Unity 도 uvx 를 못 찾는다. 그러면
# MCP for Unity 의 Auto-Start 가 "'uvx' 은(는) 내부 또는 외부 명령이 아닙니다" 로 조용히 죽는다
# (2026-08-07 실측). 게이트 자체는 멀쩡히 통과하므로 증상이 한참 뒤에야 보인다.
$env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' +
            [Environment]::GetEnvironmentVariable('Path', 'User')

# ── Unity 실행 파일 찾기 ────────────────────────────────────────────────
# 버전은 ProjectSettings 가 유일한 출처다 (하드코딩 금지 — Editor 업그레이드 때 조용히 틀어진다).
$versionFile = Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt'
if (-not (Test-Path $versionFile)) { throw "ProjectVersion.txt 없음: $versionFile" }
$editorVersion = ((Get-Content $versionFile | Select-String '^m_EditorVersion:') -split ':\s*')[1].Trim()

$unityExe = "C:\Program Files\Unity\Hub\Editor\$editorVersion\Editor\Unity.exe"
if (-not (Test-Path $unityExe)) {
    $found = Get-ChildItem 'C:\Program Files\Unity\Hub\Editor' -Directory -ErrorAction SilentlyContinue |
             Where-Object { Test-Path (Join-Path $_.FullName 'Editor\Unity.exe') }
    throw "Unity $editorVersion 없음. 설치된 버전: $($found.Name -join ', ')"
}

# ── Editor 락 판정 ──────────────────────────────────────────────────────
# ⚠️ 창 제목으로 판정하면 안 된다 — Unity 는 종료 중에 창을 먼저 없애고 프로세스는 살아
# 있는 동안 락을 계속 쥔다. 그 틈에 batchmode 를 띄우면 "another Unity instance is running"
# 으로 죽는다 (2026-08-07 실측). 진짜 판정은 Temp/UnityLockfile 을 배타로 열어보는 것이다.
$lockFile = Join-Path $projectRoot 'Temp\UnityLockfile'

function Test-ProjectLocked {
    if (Test-Path $lockFile) {
        try {
            $fs = [System.IO.File]::Open($lockFile, 'Open', 'ReadWrite', 'None')
            $fs.Close()
        } catch { return $true }   # 열 수 없다 = 살아 있는 Unity 가 쥐고 있다
    }
    # 락파일이 없거나 free 여도, 이 프로젝트를 연 Unity 프로세스가 남아 있으면 아직이다
    $procs = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue |
             Where-Object { $_.CommandLine -and $_.CommandLine -like "*$projectRoot*" }
    return [bool]$procs
}

function Get-ProjectEditor {
    Get-Process Unity -ErrorAction SilentlyContinue |
        Where-Object { $_.MainWindowTitle -like '*AI_GOAP*' }
}

$editor = Get-ProjectEditor
if (-not $editor -and (Test-ProjectLocked)) {
    # 창은 없는데 락은 살아 있는 상태 (종료 중이거나 batchmode 가 돌고 있다)
    $editor = Get-Process Unity -ErrorAction SilentlyContinue | Select-Object -First 1
}
if ($editor) {
    if (-not $CloseEditor) {
        Write-Host ''
        Write-Host '  Unity Editor 가 이 프로젝트를 열고 있어 batchmode 가 실패합니다 (락).' -ForegroundColor Yellow
        Write-Host '  선택지:' -ForegroundColor Yellow
        Write-Host '    · Editor 를 켜 둔 채 돌리려면  → MCP 의 run_tests 툴을 쓰세요'
        Write-Host '    · 이 스크립트로 돌리려면       → -CloseEditor [-ReopenEditor] 를 주세요'
        Write-Host ''
        exit 2
    }
    Write-Host "Editor 종료 중 (PID $($editor.Id))..." -ForegroundColor Cyan
    try { $editor.CloseMainWindow() | Out-Null } catch { }
    # 창이 아니라 락이 풀릴 때까지 기다린다 (위 주석 참조)
    $deadline = (Get-Date).AddSeconds($CloseTimeoutSec)
    while ((Test-ProjectLocked) -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 2 }
    if (Test-ProjectLocked) {
        throw "Editor 락이 ${CloseTimeoutSec}초 안에 풀리지 않았습니다 (저장 대화상자가 떠 있는지 확인하세요)."
    }
    Start-Sleep -Seconds 3   # 파일 핸들 정리 여유
    Write-Host 'Editor 종료됨 (락 해제 확인).' -ForegroundColor Cyan
}

# ── 실행 ────────────────────────────────────────────────────────────────
$outDir = Join-Path $projectRoot 'Logs\gates'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$stamp     = Get-Date -Format 'yyyyMMdd-HHmmss'
$resultXml = Join-Path $outDir "results-$stamp.xml"
$logFile   = Join-Path $outDir "unity-$stamp.log"

# ⚠️ -quit 을 붙이지 않는다 (위 ① 참조). -runTests 는 끝나면 스스로 종료한다.
$unityArgs = @(
    '-batchmode', '-nographics'
    '-projectPath', $projectRoot
    '-runTests', '-testPlatform', 'EditMode'
    '-testResults', $resultXml
    '-logFile', $logFile
)
if ($Filter) { $unityArgs += @('-testFilter', $Filter) }

Write-Host "게이트 실행 중... (로그: $logFile)" -ForegroundColor Cyan
$proc = Start-Process -FilePath $unityExe -ArgumentList $unityArgs -PassThru -NoNewWindow
if (-not $proc.WaitForExit($RunTimeoutSec * 1000)) {
    try { $proc.Kill() } catch { }
    throw "테스트가 ${RunTimeoutSec}초를 넘겨 중단했습니다. 로그: $logFile"
}
$unityExit = $proc.ExitCode

# ── 판정: exit code 가 아니라 XML 이 진실이다 ───────────────────────────
if (-not (Test-Path $resultXml)) {
    Write-Host ''
    Write-Host "  결과 XML 이 없습니다 — 테스트가 돌지 않았습니다 (Unity exit $unityExit)." -ForegroundColor Red
    Write-Host "  로그를 보세요: $logFile" -ForegroundColor Red
    if ($ReopenEditor) { Start-Process $unityExe -ArgumentList '-projectPath', $projectRoot }
    exit 1
}

[xml]$xml = Get-Content $resultXml
$run = $xml.'test-run'
$total   = [int]$run.total
$passed  = [int]$run.passed
$failed  = [int]$run.failed
$skipped = [int]$run.skipped
$inconcl = [int]$run.inconclusive

Write-Host ''
Write-Host "  게이트 결과: $passed/$total 통과" -NoNewline
if ($failed -gt 0)  { Write-Host "  ·  실패 $failed" -ForegroundColor Red -NoNewline }
if ($skipped -gt 0) { Write-Host "  ·  건너뜀 $skipped" -ForegroundColor Yellow -NoNewline }
Write-Host ''
Write-Host "  XML: $resultXml"

if ($failed -gt 0) {
    Write-Host ''
    Write-Host '  실패 목록:' -ForegroundColor Red
    # ⚠️ Windows PowerShell 5.1 호환 — ?? / ?: / ?. 연산자 없음
    $xml.SelectNodes('//test-case[@result="Failed"]') | ForEach-Object {
        Write-Host "    · $($_.fullname)" -ForegroundColor Red
        $msg = $null
        if ($_.failure -and $_.failure.message) {
            $msg = $_.failure.message.'#cdata-section'
            if (-not $msg) { $msg = [string]$_.failure.message }
        }
        if ($msg) { Write-Host "      $($msg.Trim())" }
    }
}

if ($ReopenEditor) {
    Write-Host ''
    Write-Host 'Editor 재실행 중 (MCP 세션 복귀는 Window > MCP for Unity > Start Server)...' -ForegroundColor Cyan
    Start-Process $unityExe -ArgumentList '-projectPath', $projectRoot
}

# 실패 0 이고 실제로 테스트가 돌았을 때만 0
if ($failed -eq 0 -and $inconcl -eq 0 -and $total -gt 0) { exit 0 } else { exit 1 }
