# Script khởi chạy Localtunnel cho dự án VinhKhanh
# Bạn cần chạy .\run-admin.ps1 trước khi chạy script này.

Write-Host "================================================" -ForegroundColor Yellow
Write-Host "   VINHKHANH LOCALTUNNEL STARTUP SCRIPT" -ForegroundColor Yellow
Write-Host "================================================" -ForegroundColor Yellow

# 1. Lấy Public IP (Cần thiết để vượt qua trang Landing Page của Localtunnel)
Write-Host "--- 1. ĐANG LẤY PUBLIC IP CỦA BẠN ---" -ForegroundColor Cyan
try {
    $publicIp = (Invoke-RestMethod -Uri "https://api.ipify.org").Trim()
    Write-Host "Public IP của bạn là: " -NoNewline; Write-Host $publicIp -ForegroundColor Green
    Write-Host "Ghi chú: Khi mở link .loca.lt, hãy nhập số IP trên vào nếu được hỏi." -ForegroundColor Gray
} catch {
    Write-Host "Không thể lấy IP tự động. Vui lòng truy cập https://whatismyip.com để xem IP của bạn." -ForegroundColor Red
}
Write-Host ""

# 2. Cố định Subdomain để link không bị đổi mỗi khi chạy lại.
# Nếu bị báo lỗi 'Subdomain already taken', hãy đổi 2 tên bên dưới thành tên khác.
$apiSubdomain = "vingkhanh-testproject1-api"
$webSubdomain = "vingkhanh-testproject1"


# 2. Khởi chạy Tunnel cho Backend (Cổng 5000)
Write-Host "--- 2. MỞ TUNNEL BACKEND: https://$apiSubdomain.loca.lt ---" -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "npx localtunnel --port 5000 --local-host 127.0.0.1 --subdomain $apiSubdomain"

# 3. Khởi chạy Tunnel cho Frontend (Cổng 3000 - Vite Preview/Dev)
Write-Host "--- 3. MỞ TUNNEL FRONTEND: https://$webSubdomain.loca.lt ---" -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "npx localtunnel --port 3000 --local-host 127.0.0.1 --subdomain $webSubdomain"

Write-Host ""
Write-Host ">>> XONG! Nếu link không đúng như trên, nghĩa là tên miền đã bị người khác chiếm." -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Yellow
