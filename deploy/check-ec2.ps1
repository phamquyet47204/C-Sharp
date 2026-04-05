# Script kiểm tra trạng thái EC2 nhanh
# Chạy: .\deploy\check-ec2.ps1

$EC2_IP = "18.139.184.43"
$PEM    = "C:\Users\phamq\Documents\key\cs.pem"

function SSH { param([string]$cmd)
    & ssh -i $PEM -o StrictHostKeyChecking=no "ubuntu@$EC2_IP" $cmd
}

Write-Host "=== Service status ===" -ForegroundColor Cyan
SSH "sudo systemctl status vinhkhanh --no-pager -l | tail -20"

Write-Host "`n=== Last 30 log lines ===" -ForegroundColor Cyan
SSH "sudo journalctl -u vinhkhanh -n 30 --no-pager"

Write-Host "`n=== Nginx status ===" -ForegroundColor Cyan
SSH "sudo systemctl status nginx --no-pager | head -10"

Write-Host "`n=== Nginx error log ===" -ForegroundColor Cyan
SSH "sudo tail -20 /var/log/nginx/error.log"

Write-Host "`n=== Frontend files ===" -ForegroundColor Cyan
SSH "ls -la ~/vinhkhanh/frontend/ | head -20"

Write-Host "`n=== Backend files ===" -ForegroundColor Cyan
SSH "ls ~/vinhkhanh/backend/*.dll 2>/dev/null | head -5 || echo 'NO DLL FILES FOUND'"

Write-Host "`n=== API health check ===" -ForegroundColor Cyan
SSH "curl -s -w '\nHTTP %{http_code}' http://localhost:5000/api/auth/login -X POST -H 'Content-Type: application/json' -d '{\"email\":\"test\",\"password\":\"test\"}' 2>&1 | tail -3"

Write-Host "`n=== index.html check ===" -ForegroundColor Cyan
SSH "cat ~/vinhkhanh/frontend/index.html 2>/dev/null | head -20 || echo 'index.html NOT FOUND'"
