@echo off
setlocal

set "ROOT=%~dp0"
set "ADB=%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe"
set "PROJECT=%ROOT%VinhKhanhFoodStreet.csproj"
set "APK=%ROOT%bin\Debug\net10.0-android\com.companyname.vinhkhanhfoodstreet-Signed.apk"
set "PKG=com.companyname.vinhkhanhfoodstreet"
set "DEVICE=emulator-5554"

if not exist "%ADB%" (
    echo [ERROR] Khong tim thay adb tai: %ADB%
    echo Hay cai Android SDK hoac kiem tra lai duong dan.
    exit /b 1
)

echo [0/6] Kiem tra emulator %DEVICE%...
"%ADB%" -s %DEVICE% get-state >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Emulator %DEVICE% khong san sang.
    echo Mo Android Studio ^> Device Manager ^> Start emulator, sau do chay lai script.
    exit /b 1
)
echo Emulator san sang.

echo [1/6] Build Android Debug...
dotnet build "%PROJECT%" -f net10.0-android -c Debug
if errorlevel 1 (
    echo [ERROR] Build that bai. Kiem tra lai loi o tren.
    exit /b 1
)

if not exist "%APK%" (
    echo [ERROR] Khong tim thay APK: %APK%
    exit /b 1
)

echo [2/6] Force stop app cu...
"%ADB%" -s %DEVICE% shell am force-stop %PKG%

echo [3/6] Xoa data/cache app cu...
"%ADB%" -s %DEVICE% shell pm clear %PKG%

echo [4/6] Goi bo app cu...
"%ADB%" -s %DEVICE% uninstall %PKG% >nul 2>&1

echo [5/6] Cai APK moi...
"%ADB%" -s %DEVICE% install "%APK%"
if errorlevel 1 (
    echo [ERROR] Cai APK that bai.
    exit /b 1
)

echo [6/6] Mo app...
"%ADB%" -s %DEVICE% shell monkey -p %PKG% -c android.intent.category.LAUNCHER 1

echo.
echo === DONE ===
echo Da build + clean + cai moi thanh cong.
echo App dang chay tren emulator %DEVICE%.
echo API Backend: http://10.0.2.2:5000 (tu emulator)
endlocal
