param([string]$UnityEditorPath = $env:UNITY_EDITOR_PATH, [switch]$OpenUnity)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$versionFile = Join-Path $project 'ProjectSettings\ProjectVersion.txt'
$expected = ((Get-Content $versionFile | Select-String '^m_EditorVersion:').Line -split ': ')[1]
if (-not $UnityEditorPath) {
    $UnityEditorPath = Join-Path ${env:ProgramFiles} "Unity\Hub\Editor\$expected\Editor\Unity.exe"
}
if (-not (Test-Path $UnityEditorPath)) {
    throw "Install Unity $expected in Unity Hub, or pass -UnityEditorPath with your actual Unity.exe path."
}
Write-Host "Unity: $UnityEditorPath"
$android = Join-Path (Split-Path $UnityEditorPath) 'Data\PlaybackEngines\AndroidPlayer'
foreach ($component in @('SDK', 'NDK', 'OpenJDK')) {
    $present = Test-Path (Join-Path $android $component)
    Write-Host "Android $component installed: $present"
}
$adb = Join-Path $android 'SDK\platform-tools\adb.exe'
if (Test-Path $adb) {
    & $adb devices -l
    if ($LASTEXITCODE -ne 0) { throw 'ADB device query failed.' }
    Write-Host 'unauthorized: accept USB debugging inside the headset. No device: check USB data cable and Developer Mode.'
    Write-Host "For existing install scripts in this PowerShell session:"
    Write-Host ('$env:Path = "' + (Split-Path $adb) + ';" + $env:Path')
} else { Write-Warning 'ADB missing. Add Android SDK via Unity Hub.' }
Write-Host 'This check does not change your Meta account, app permissions or existing project files.'
if ($OpenUnity) {
    Start-Process -FilePath $UnityEditorPath -ArgumentList @('-projectPath', ('"' + $project + '"'))
}
