# run-mobile-clean.ps1
# Script to cleanly uninstall, build, and deploy the Vĩnh Khánh Mobile app on ALL connected Android devices.

$ErrorActionPreference = "Stop"

# Configuration
$projectName = "VinhKhanh.Mobile"
$projectPath = Join-Path $PSScriptRoot "$projectName\$projectName.csproj"
$packageName = "com.companyname.vinhkhanhfoodstreet"
$mainActivity = "$packageName/crc642a3a1cbecfaebd09.MainActivity"
$adbPath = "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"

# 1. FIND ALL DEVICES
Write-Host "--- 1. CHECKING FOR CONNECTED DEVICES ---" -ForegroundColor Cyan
$devices = & $adbPath devices | Select-String "\tdevice$" | ForEach-Object { $_.ToString().Split("`t")[0] }

if ($devices.Count -eq 0) {
    Write-Error "No Android devices or emulators found. Please connect a device or start an emulator."
}

Write-Host "Found $($devices.Count) device(s):" -ForegroundColor Green
foreach ($device in $devices) {
    Write-Host "- $device"
}

# 2. WIPE / UNINSTALL FROM ALL
Write-Host "`n--- 2. WIPING APP DATA (UNINSTALLING) ---" -ForegroundColor Cyan
foreach ($device in $devices) {
    Write-Host "Uninstalling from $device..."
    try {
        & $adbPath -s $device uninstall $packageName 2>$null
    } catch {
        Write-Host "App not found on $device, skipping." -ForegroundColor Gray
    }
}

# 3. BUILD
Write-Host "`n--- 3. BUILDING APP ---" -ForegroundColor Cyan
# Thêm tham số buildPackage để đảm bảo tạo APK
dotnet build $projectPath -c Debug -f net10.0-android /t:PackageForAndroid /p:AndroidBuildApplicationPackage=true

# 4. LOCATE APK
Write-Host "`n--- 4. LOCATING APK ---" -ForegroundColor Cyan
$apkPath = Get-ChildItem -Path "$projectName\bin\Debug\net10.0-android" -Filter "*-Signed.apk" -Recurse | Select-Object -First 1 -ExpandProperty FullName
if (-not $apkPath) {
    $apkPath = Get-ChildItem -Path "$projectName\bin\Debug\net10.0-android" -Filter "*.apk" -Recurse | Select-Object -First 1 -ExpandProperty FullName
}
Write-Host "Found APK: $apkPath" -ForegroundColor Green

# 5. INSTALL & LAUNCH ON ALL
Write-Host "`n--- 5. REINSTALLING AND LAUNCHING ---" -ForegroundColor Cyan
foreach ($device in $devices) {
    Write-Host "Installing on $device..."
    & $adbPath -s $device install -r -d $apkPath

    Write-Host "Granting basic permissions on $device..."
    & $adbPath -s $device shell pm grant $packageName android.permission.ACCESS_FINE_LOCATION 2>$null
    & $adbPath -s $device shell pm grant $packageName android.permission.ACCESS_COARSE_LOCATION 2>$null

    Write-Host "Launching app on $device..."
    & $adbPath -s $device shell am start -n $mainActivity
}

Write-Host "`n--- DEPLOYMENT COMPLETE ---" -ForegroundColor Green
