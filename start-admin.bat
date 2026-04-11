@echo off
setlocal

set "ROOT=%~dp0"
set "ADMIN_DIR=%ROOT%VinhKhanh.Admin"
set "UI_DIR=%ROOT%VinhKhanh.Admin.Ui"

echo [1/2] Khoi dong Admin API (http://localhost:5000)...
start "VinhKhanh Admin API" cmd /k "set ASPNETCORE_ENVIRONMENT=Development && pushd "%ADMIN_DIR%" && dotnet run --launch-profile Development"

echo [2/2] Khoi dong Admin UI (http://localhost:3000)...
start "VinhKhanh Admin UI" cmd /k "pushd "%UI_DIR%" && npm run dev"

echo.
echo === DA KHOI DONG ===
echo Admin API : http://localhost:5000
echo Admin UI  : http://localhost:3000
echo Swagger   : http://localhost:5000/swagger
endlocal
