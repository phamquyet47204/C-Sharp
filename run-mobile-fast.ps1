# run-mobile-fast.ps1
# Script to build, install, grant permissions, and launch the Vĩnh Khánh Mobile app on an Android emulator.

$ErrorActionPreference = "Stop"

# Configuration
$projectName = "VinhKhanh.Mobile"
$projectPath = Join-Path $PSScriptRoot "$projectName\$projectName.csproj"
$packageName = "com.companyname.vinhkhanhfoodstreet"
$mainActivity = "$packageName/crc642a3a1cbecfaebd09.MainActivity"
$adbPath = "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"

# 1. FIND DEVICE
Write-Host "--- 1. CHECKING FOR EMULATOR ---" -ForegroundColor Cyan
$devices = & $adbPath devices | Select-String "\tdevice$"
if ($devices.Count -eq 0) {
    Write-Error "No Android devices or emulators found. Please start an emulator first."
}
$targetDevice = $devices[0].ToString().Split("`t")[0]
Write-Host "Targeting device: $targetDevice" -ForegroundColor Green

# 2. BUILD
Write-Host "--- 2. BUILDING APP ---" -ForegroundColor Cyan
dotnet build $projectPath -c Debug -f net10.0-android --no-incremental

# 3. LOCATE APK
Write-Host "--- 3. LOCATING APK ---" -ForegroundColor Cyan
$apkPath = Get-ChildItem -Path "$projectName\bin\Debug\net10.0-android" -Filter "*-Signed.apk" -Recurse | Select-Object -First 1 -ExpandProperty FullName
if (-not $apkPath) {
    $apkPath = Get-ChildItem -Path "$projectName\bin\Debug\net10.0-android" -Filter "*.apk" -Recurse | Select-Object -First 1 -ExpandProperty FullName
}
Write-Host "Found APK: $apkPath" -ForegroundColor Green

# 4. CLEAR CACHE
Write-Host "--- 4. CLEARING OLD APP CACHE ---" -ForegroundColor Cyan
try {
    & $adbPath -s $targetDevice shell pm clear $packageName 2>$null
} catch {
    Write-Host "App not installed yet, skipping clear." -ForegroundColor Gray
}

# 5. INSTALL
Write-Host "--- 5. INSTALLING APK ---" -ForegroundColor Cyan
& $adbPath -s $targetDevice install -r $apkPath

# 6. GRANT PERMISSIONS
Write-Host "--- 6. GRANTING LOCATION PERMISSIONS ---" -ForegroundColor Cyan
$permissions = @(
    "android.permission.ACCESS_FINE_LOCATION",
    "android.permission.ACCESS_COARSE_LOCATION"
)

foreach ($perm in $permissions) {
    Write-Host "Granting $perm..."
    & $adbPath -s $targetDevice shell pm grant $packageName $perm
}

# 7. LAUNCH
Write-Host "--- 7. LAUNCHING APP ---" -ForegroundColor Cyan
& $adbPath -s $targetDevice shell am start -n $mainActivity
Write-Host "--- DONE ---" -ForegroundColor Green
