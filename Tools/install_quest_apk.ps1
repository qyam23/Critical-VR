param([string]$ApkPath, [string]$Serial)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
if (-not $ApkPath) { $ApkPath = Join-Path $project 'Builds\Quest\Development\CriticalVR-Quest-Development.apk' }
if (-not (Test-Path -LiteralPath $ApkPath)) { throw "APK not found: $ApkPath" }
$toolchain = $env:CRITICAL_VR_ANDROID_TOOLS
$config = Join-Path $project 'UserSettings\CriticalVRAndroidToolchain.txt'
if (-not $toolchain -and (Test-Path $config)) { $toolchain = (Get-Content $config -Raw).Trim() }
if (-not $toolchain) {
    $version = ((Get-Content (Join-Path $project 'ProjectSettings\ProjectVersion.txt') | Select-String '^m_EditorVersion:').Line -split ': ')[1]
    $toolchain = Join-Path ${env:ProgramFiles} "Unity\Hub\Editor\$version\Editor\Data\PlaybackEngines\AndroidPlayer"
}
$adb = Join-Path $toolchain 'SDK\platform-tools\adb.exe'
if (-not (Test-Path -LiteralPath $adb)) { throw 'ADB not found. Configure the Android toolchain first.' }
$devices = @(& $adb devices | Where-Object { $_ -match '^\S+\s+device$' } | ForEach-Object { ($_ -split '\s+')[0] })
if (-not $Serial) {
    if ($devices.Count -ne 1) { throw 'Connect one Quest 3/3S by USB and accept USB debugging inside the headset, or select -Serial.' }
    $Serial = $devices[0]
}
if ($Serial -notin $devices) { throw 'Selected headset is not connected and authorized.' }
$model = (& $adb -s $Serial shell getprop ro.product.model).Trim()
$osBuild = (& $adb -s $Serial shell getprop ro.build.display.id).Trim()
if ($model -notmatch '^(Meta )?Quest 3S?$') { throw "Unsupported model: $model. This camera build targets Quest 3/3S." }
$logs = Join-Path $project 'Builds\Quest\Logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
"Model: $model`nOS build: $osBuild`nAPK: $ApkPath" | Set-Content (Join-Path $logs 'device-info.txt')
Write-Output "Model: $model; OS build: $osBuild. Camera API compatibility is also checked by the SDK at runtime."
& $adb -s $Serial install -r $ApkPath
if ($LASTEXITCODE -ne 0) { throw 'APK installation failed.' }
$activity = (& $adb -s $Serial shell cmd package resolve-activity --brief com.qyam23.criticalvr | Select-Object -Last 1).Trim()
if ($activity -notmatch '^com\.qyam23\.criticalvr/') { throw 'Launch activity was not resolved.' }
& $adb -s $Serial shell am start -W -n $activity
if ($LASTEXITCODE -ne 0) { throw 'App launch failed.' }
& $adb -s $Serial logcat -d -s Unity AndroidRuntime > (Join-Path $logs 'device-launch.log')
Write-Output 'Installed and launched. Respond to the camera permission prompt inside the headset. B/Y stops; A/X resumes.'
Write-Output 'This command installs an existing APK. It does not claim visual, stereo or performance verification.'
