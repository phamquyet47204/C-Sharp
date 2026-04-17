param(
    [string]$WorkspacePath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$AndroidSdkDirectory = (Join-Path $env:LOCALAPPDATA 'Android\Sdk'),
    [string]$AvdName = 'Pixel_6'
)

$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $WorkspacePath 'VinhKhanh.Mobile\VinhKhanh.Mobile.csproj'
$mobileRoot = Join-Path $WorkspacePath 'VinhKhanh.Mobile'
$sharedRoot = Join-Path $WorkspacePath 'VinhKhanh.Shared'
$tempNuget = Join-Path $WorkspacePath '.nuget-temp-mobile'
$adb = Join-Path $AndroidSdkDirectory 'platform-tools\adb.exe'
$emulator = Join-Path $AndroidSdkDirectory 'emulator\emulator.exe'
$packageName = 'com.vinhkhanh.mobile'
$watchExtensions = @('*.cs', '*.xaml', '*.csproj', '*.resx')

$script:PendingBuild = $false
$script:LastChange = Get-Date

function Write-Stage {
    param([string]$Message)
    Write-Host $Message
}

function Clear-MobileCache {
    Write-Stage '==> CLEAR CACHE'

    $pathsToRemove = @(
        (Join-Path $mobileRoot 'bin'),
        (Join-Path $mobileRoot 'obj'),
        $tempNuget
    )

    foreach ($path in $pathsToRemove) {
        if (Test-Path $path) {
            Remove-Item -Recurse -Force $path -ErrorAction SilentlyContinue
        }
    }
}

function Ensure-Emulator {
    $deviceLine = & $adb devices | Select-String 'emulator-'
    if (-not $deviceLine) {
        Write-Stage "==> START EMULATOR $AvdName"
        Start-Process -FilePath $emulator -ArgumentList @('-avd', $AvdName) | Out-Null
        & $adb wait-for-device | Out-Null

        while ((& $adb shell getprop sys.boot_completed).Trim() -ne '1') {
            Start-Sleep -Seconds 2
        }

        Start-Sleep -Seconds 3
    }
}

function Build-And-Install {
    Write-Stage '==> BUILD START'

    if (-not (Test-Path $tempNuget)) {
        New-Item -ItemType Directory -Path $tempNuget | Out-Null
    }

    $env:NUGET_PACKAGES = $tempNuget

    & dotnet build $projectPath `
        -f net9.0-android `
        -p:AndroidSdkDirectory=$AndroidSdkDirectory `
        -p:AndroidPackageFormats=apk

    if ($LASTEXITCODE -ne 0) {
        Write-Stage '==> BUILD FAILED'
        return
    }

    $apk = Get-ChildItem -Path (Join-Path $mobileRoot 'bin\Debug\net9.0-android') -Filter '*.apk' |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if (-not $apk) {
        Write-Stage '==> APK NOT FOUND'
        return
    }

    & $adb install -r $apk.FullName
    if ($LASTEXITCODE -ne 0) {
        Write-Stage '==> INSTALL FAILED'
        return
    }

    & $adb shell monkey -p $packageName -c android.intent.category.LAUNCHER 1 | Out-Null
    Write-Stage '==> BUILD DONE'
}

function Register-Watcher {
    param([string]$Path)

    foreach ($extension in $watchExtensions) {
        $watcher = New-Object System.IO.FileSystemWatcher
        $watcher.Path = $Path
        $watcher.Filter = $extension
        $watcher.IncludeSubdirectories = $true
        $watcher.NotifyFilter = [System.IO.NotifyFilters]'FileName, LastWrite, CreationTime, Size'
        $watcher.EnableRaisingEvents = $true

        Register-ObjectEvent -InputObject $watcher -EventName Changed -Action {
            $script:PendingBuild = $true
            $script:LastChange = Get-Date
        } | Out-Null

        Register-ObjectEvent -InputObject $watcher -EventName Created -Action {
            $script:PendingBuild = $true
            $script:LastChange = Get-Date
        } | Out-Null

        Register-ObjectEvent -InputObject $watcher -EventName Renamed -Action {
            $script:PendingBuild = $true
            $script:LastChange = Get-Date
        } | Out-Null

        Register-ObjectEvent -InputObject $watcher -EventName Deleted -Action {
            $script:PendingBuild = $true
            $script:LastChange = Get-Date
        } | Out-Null
    }
}

Clear-MobileCache
Ensure-Emulator
Build-And-Install

Register-Watcher -Path $mobileRoot
Register-Watcher -Path $sharedRoot

Write-Stage '==> WATCHER READY'

while ($true) {
    Wait-Event -Timeout 1 | Out-Null

    if ($script:PendingBuild -and ((Get-Date) - $script:LastChange).TotalSeconds -ge 2) {
        $script:PendingBuild = $false
        Build-And-Install
    }
}