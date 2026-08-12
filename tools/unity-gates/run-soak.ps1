<#
.SYNOPSIS
  Play 소크 러너 — 배치 모드로 M28PlaySoak.Run 을 돌린다 (run-gates.ps1 의 락 규약 승계).
  -quit 금지 · Editor 락은 배타 열기로 판정 · 판정은 로그의 [Soak] 줄로 (exit code 는 보조).
  ⚠️ -nographics 를 주지 않는다 — PlayMode 는 렌더 컴포넌트가 살아야 한다 (EditMode 게이트와 다름).
#>
[CmdletBinding()]
param(
    [int] $CloseTimeoutSec = 60,
    [int] $RunTimeoutSec   = 3600
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' +
            [Environment]::GetEnvironmentVariable('Path', 'User')

$versionFile = Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt'
$editorVersion = ((Get-Content $versionFile | Select-String '^m_EditorVersion:') -split ':\s*')[1].Trim()
$unityExe = "C:\Program Files\Unity\Hub\Editor\$editorVersion\Editor\Unity.exe"
if (-not (Test-Path $unityExe)) { throw "Unity $editorVersion not found" }

$lockFile = Join-Path $projectRoot 'Temp\UnityLockfile'
function Test-ProjectLocked {
    if (Test-Path $lockFile) {
        try { $fs = [System.IO.File]::Open($lockFile, 'Open', 'ReadWrite', 'None'); $fs.Close() }
        catch { return $true }
    }
    $procs = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue |
             Where-Object { $_.CommandLine -and $_.CommandLine -like "*$projectRoot*" }
    return [bool]$procs
}

$editor = Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like '*AI_GOAP*' }
if (-not $editor -and (Test-ProjectLocked)) {
    $editor = Get-Process Unity -ErrorAction SilentlyContinue | Select-Object -First 1
}
if ($editor) {
    Write-Host "Closing editor (PID $($editor.Id))..."
    try { $editor.CloseMainWindow() | Out-Null } catch { }
    $deadline = (Get-Date).AddSeconds($CloseTimeoutSec)
    while ((Test-ProjectLocked) -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 2 }
    if (Test-ProjectLocked) { throw "Editor lock not released" }
    Start-Sleep -Seconds 3
}

$outDir = Join-Path $projectRoot 'Logs\soak'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$stamp   = Get-Date -Format 'yyyyMMdd-HHmmss'
$logFile = Join-Path $outDir "soak-$stamp.log"

Write-Host "Soak running... (log: $logFile)"
$proc = Start-Process -FilePath $unityExe -ArgumentList @(
    '-batchmode', '-projectPath', $projectRoot,
    '-executeMethod', 'AIVillage.EditorTools.M28PlaySoak.Run',
    '-logFile', $logFile
) -PassThru -NoNewWindow
if (-not $proc.WaitForExit($RunTimeoutSec * 1000)) {
    try { $proc.Kill() } catch { }
    throw "Soak timed out after ${RunTimeoutSec}s. Log: $logFile"
}

Write-Host "Unity exit=$($proc.ExitCode)"
Write-Host '--- [Soak] summary ---'
Select-String -Path $logFile -Pattern '\[Soak\]' | ForEach-Object { $_.Line }
Write-Host '--- health ---'
# ASCII-only source (PS5.1 + BOM-less UTF-8). Korean patterns are built from code points.
$wipePat = [string][char]0xC804 + [char]0xBA78   # '전멸'
$noSol  = (Select-String -Path $logFile -Pattern 'NoSolutionFound' | Measure-Object).Count
$wipe   = (Select-String -Path $logFile -SimpleMatch $wipePat | Measure-Object).Count
$errCnt = (Select-String -Path $logFile -Pattern 'Exception|error CS' | Measure-Object).Count
Write-Host "NoSolutionFound=$noSol wipe=$wipe exceptions=$errCnt"
exit $proc.ExitCode
