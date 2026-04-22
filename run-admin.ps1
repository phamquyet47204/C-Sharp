$root = $PSScriptRoot

# --- 0. CLEANUP STALE PROCESSES ---
Write-Host "--- 0. DỌN DẸP CÁC TIẾN TRÌNH CŨ (Cổng 3000, 5000) ---" -ForegroundColor Yellow
$ports = 3000, 5000
foreach ($port in $ports) {
    $pids = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess | Sort-Object -Unique
    foreach ($id in $pids) { if ($id -gt 0) { Stop-Process -Id $id -Force -ErrorAction SilentlyContinue } }
}

Write-Host "--- 1. STARTING BACKEND (ASP.NET Core) ---" -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$root\VinhKhanh.Admin'; `$env:ASPNETCORE_URLS='http://0.0.0.0:5000'; dotnet run"

Write-Host "--- 2. STARTING FRONTEND (React + Vite) ---" -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$root\VinhKhanh.Admin.Ui'; npm run dev -- --host"

Write-Host "------------------------------------------------" -ForegroundColor Green
Write-Host "Admin Dashboard is starting at http://localhost:3000" -ForegroundColor Green
Write-Host "API Swagger is starting at http://localhost:5000/swagger" -ForegroundColor Green
Write-Host "API Health is available at http://localhost:5000/health" -ForegroundColor Green
Write-Host "------------------------------------------------" -ForegroundColor Green
