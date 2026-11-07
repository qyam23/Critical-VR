param([string]$UnityEditorPath = $env:UNITY_EDITOR_PATH, [switch]$Install, [string]$Serial)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$version = ((Get-Content (Join-Path $project 'ProjectSettings\ProjectVersion.txt') | Select-String '^m_EditorVersion:').Line -split ': ')[1]
if (-not $UnityEditorPath) { $UnityEditorPath = Join-Path ${env:ProgramFiles} "Unity\Hub\Editor\$version\Editor\Unity.exe" }
if (-not (Test-Path $UnityEditorPath)) { throw "Unity $version not found." }
$running = Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'Unity.exe' -and $_.CommandLine -like "*$project*" }
if ($running) { throw 'Close the Unity Editor for this project before running batch checks. Save your scenes first.' }
$logs = Join-Path $project 'Builds\Quest\Logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$env:CRITICAL_VR_SKIP_VALIDATION = $null

function Invoke-UnityCheck([string]$Method, [string]$LogName, [switch]$Room) {
    $arguments = "-batchmode -projectPath `"$project`" -executeMethod $Method -logFile `"$(Join-Path $logs $LogName)`""
    if (-not $Room) { $arguments += ' -quit -buildTarget Android' }
    # A graphics device is required for the rendered synthetic-room check.
    $process = Start-Process -FilePath $UnityEditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Unity check failed ($($process.ExitCode)): $Method. See $logs\$LogName" }
}

Push-Location $project
try {
    $started = Get-Date
    Invoke-UnityCheck 'CriticalVR.EditorTools.CriticalVRRoomCheck.RunBatch' 'synthetic-batch.log' -Room
    $roomReport = Join-Path $logs 'synthetic-check.md'
    if (-not (Test-Path $roomReport) -or (Get-Item $roomReport).LastWriteTime -lt $started -or
        -not (Select-String -LiteralPath $roomReport -Pattern '^PASS:')) { throw 'No fresh passing Play Mode report.' }
    Invoke-UnityCheck 'CriticalVR.EditorTools.CriticalVRProjectValidator.RunQuestValidationBatch' 'validation-batch.log'
    Invoke-UnityCheck 'CriticalVR.EditorTools.CriticalVRBuildAutomation.BuildQuestDevelopmentBatch' 'build-quest.log'
    $apk = Join-Path $project 'Builds\Quest\Development\CriticalVR-Quest-Development.apk'
    if (-not (Test-Path $apk) -or (Get-Item $apk).LastWriteTime -lt $started) { throw 'No fresh APK was produced.' }
    Write-Output "APK: $apk"
    if ($Install) {
        & (Join-Path $PSScriptRoot 'install_quest_apk.ps1') -ApkPath $apk -Serial $Serial
    }
} finally { Pop-Location }
