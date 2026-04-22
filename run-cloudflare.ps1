# Script khởi chạy VinhKhanh với Cloudflare Tunnel (Cổng 3000 & 5000)
$root = $PSScriptRoot

# --- 0. DỌN DẸP TIẾN TRÌNH CŨ ---
Write-Host "--- 0. DỌN DẸP TIẾN TRÌNH CŨ (Cổng 3000, 5000) ---" -ForegroundColor Yellow
$ports = 3000, 5000
foreach ($port in $ports) {
    $pids = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess | Sort-Object -Unique
    foreach ($id in $pids) { if ($id -gt 0) { Stop-Process -Id $id -Force -ErrorAction SilentlyContinue } }
}

# --- 1. START BACKEND (Port 5000) ---
Write-Host "--- 1. START BACKEND (api.enormitpham.me) ---" -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$root\VinhKhanh.Admin'; `$env:ASPNETCORE_URLS='http://0.0.0.0:5000'; dotnet run"

# --- 2. START FRONTEND (Port 3000) ---
Write-Host "--- 2. START FRONTEND (web.enormitpham.me) ---" -ForegroundColor Green
# Sử dụng 'npm run preview' để ổn định nhất với Tunnel
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$root\VinhKhanh.Admin.Ui'; npm run preview"

Write-Host "------------------------------------------------" -ForegroundColor White
Write-Host "Hệ thống đang khởi chạy:" -ForegroundColor Cyan
Write-Host "Web:   https://web.enormitpham.me" -ForegroundColor Cyan
Write-Host "API:   https://api.enormitpham.me" -ForegroundColor Cyan
Write-Host "------------------------------------------------" -ForegroundColor White
Write-Host "LƯU Ý: Hãy đảm bảo bạn đã chạy 'cloudflared' trên máy này." -ForegroundColor Yellow
