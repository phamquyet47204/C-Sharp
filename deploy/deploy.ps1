# Deploy VinhKhanh lên EC2 18.139.184.43
# Chạy: .\deploy\deploy.ps1

param(
    [string]$EC2_IP   = "18.139.184.43",
    [string]$PEM      = "C:\Users\phamq\Documents\key\cs.pem"
)

$SSH_USER = "ubuntu"
$ROOT     = Split-Path -Parent $PSScriptRoot

Set-Location $ROOT

function Invoke-SSH {
    param([string]$Command)
    & ssh -i $PEM -o StrictHostKeyChecking=no "${SSH_USER}@${EC2_IP}" $Command
}

function Invoke-SCP {
    param([string]$Source, [string]$Dest)
    & scp -i $PEM -o StrictHostKeyChecking=no -r $Source "${SSH_USER}@${EC2_IP}:${Dest}"
}

# ── [1/5] Build backend ───────────────────────────────────────
Write-Host "=== [1/5] Build backend (Release) ===" -ForegroundColor Cyan
if (Test-Path "./publish/backend") { Remove-Item -Recurse -Force "./publish/backend" }
dotnet publish "VinhKhanh.Admin/VinhKhanh.Admin.csproj" `
    -c Release -o "./publish/backend" --nologo -q
if ($LASTEXITCODE -ne 0) { Write-Error "Backend build failed"; exit 1 }

# ── [2/5] Build frontend ──────────────────────────────────────
Write-Host "=== [2/5] Build frontend ===" -ForegroundColor Cyan
Set-Location "VinhKhanh.Admin.Ui"
npm run build
if ($LASTEXITCODE -ne 0) { Write-Error "Frontend build failed"; exit 1 }
Set-Location $ROOT

# ── [3/5] Upload lên EC2 ─────────────────────────────────────
Write-Host "=== [3/5] Upload lên EC2 $EC2_IP ===" -ForegroundColor Cyan

# Tạo thư mục
Invoke-SSH "mkdir -p ~/vinhkhanh/backend ~/vinhkhanh/frontend"

# Upload backend — dùng tar để tránh wildcard issue
Write-Host "  Uploading backend..." -ForegroundColor Gray
$tarBackend = "./publish/backend.tar.gz"
& tar -czf $tarBackend -C "./publish/backend" .
Invoke-SCP $tarBackend "~/vinhkhanh/backend.tar.gz"
Invoke-SSH "cd ~/vinhkhanh/backend && tar -xzf ~/vinhkhanh/backend.tar.gz && rm ~/vinhkhanh/backend.tar.gz"
Remove-Item $tarBackend -ErrorAction SilentlyContinue

# Upload frontend — dùng tar
Write-Host "  Uploading frontend..." -ForegroundColor Gray
$tarFrontend = "./VinhKhanh.Admin.Ui/dist.tar.gz"
& tar -czf $tarFrontend -C "./VinhKhanh.Admin.Ui/dist" .
Invoke-SCP $tarFrontend "~/vinhkhanh/dist.tar.gz"
Invoke-SSH "rm -rf ~/vinhkhanh/frontend/* && cd ~/vinhkhanh/frontend && tar -xzf ~/vinhkhanh/dist.tar.gz && rm ~/vinhkhanh/dist.tar.gz"
Remove-Item $tarFrontend -ErrorAction SilentlyContinue

# Upload production config
Write-Host "  Uploading config..." -ForegroundColor Gray
Invoke-SCP "VinhKhanh.Admin/appsettings.Production.json" "~/vinhkhanh/backend/appsettings.Production.json"

# Upload app.env
Invoke-SCP "deploy/app.env" "~/vinhkhanh/app.env"

# Upload nginx config
Invoke-SCP "deploy/nginx/vinhkhanh.conf" "~/vinhkhanh/vinhkhanh.conf"
Invoke-SSH "sudo cp ~/vinhkhanh/vinhkhanh.conf /etc/nginx/sites-available/vinhkhanh && sudo ln -sf /etc/nginx/sites-available/vinhkhanh /etc/nginx/sites-enabled/vinhkhanh && sudo rm -f /etc/nginx/sites-enabled/default"

# Upload service file
Invoke-SCP "deploy/vinhkhanh.service" "~/vinhkhanh/vinhkhanh.service"
Invoke-SSH "sudo cp ~/vinhkhanh/vinhkhanh.service /etc/systemd/system/vinhkhanh.service && sudo systemctl daemon-reload"

# ── [4/5] Fix permissions + Restart ──────────────────────────
Write-Host "=== [4/5] Fix permissions + Restart ===" -ForegroundColor Cyan
Invoke-SSH @"
chmod -R o+rx ~/vinhkhanh/frontend
sudo nginx -t && sudo systemctl reload nginx
sudo systemctl enable vinhkhanh
sudo systemctl restart vinhkhanh
sleep 5
sudo systemctl status vinhkhanh --no-pager -l
"@

# ── [5/5] Health check ────────────────────────────────────────
Write-Host "=== [5/5] Health check ===" -ForegroundColor Cyan
Start-Sleep -Seconds 3
Invoke-SSH "curl -s -o /dev/null -w 'HTTP %{http_code}' http://localhost:5000/api/health 2>/dev/null || curl -s -o /dev/null -w 'HTTP %{http_code}' http://localhost:5000/api/auth/login -X POST -H 'Content-Type: application/json' -d '{\"email\":\"x\",\"password\":\"x\"}'"

Write-Host ""
Write-Host "=== DEPLOY XONG ===" -ForegroundColor Green
Write-Host "Site: https://enormitpham.me"
Write-Host "API:  https://enormitpham.me/api"
Write-Host ""
Write-Host "Nếu vẫn white screen, chạy lệnh debug:"
Write-Host "  ssh -i `"$PEM`" ubuntu@$EC2_IP 'sudo journalctl -u vinhkhanh -n 30 --no-pager'"
