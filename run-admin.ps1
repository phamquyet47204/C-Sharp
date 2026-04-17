$root = $PSScriptRoot
Write-Host "--- 1. STARTING BACKEND (ASP.NET Core) ---" -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$root\VinhKhanh.Admin'; dotnet run --urls http://localhost:5000"

Write-Host "--- 2. STARTING FRONTEND (React + Vite) ---" -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$root\VinhKhanh.Admin.Ui'; npm run dev"

Write-Host "------------------------------------------------" -ForegroundColor Green
Write-Host "Admin Dashboard is starting at http://localhost:5173" -ForegroundColor Green
Write-Host "API Swagger is starting at http://localhost:5000/swagger" -ForegroundColor Green
Write-Host "API Health is available at http://localhost:5000/health" -ForegroundColor Green
Write-Host "------------------------------------------------" -ForegroundColor Green
