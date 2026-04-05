#!/bin/bash
# Deploy VinhKhanh lên EC2
# Chạy: ./deploy/deploy.sh <EC2_IP>
# Ví dụ: ./deploy/deploy.sh 54.123.45.67
# PEM file: cs.pem phải nằm cùng thư mục hoặc chỉ định qua PEM=~/.ssh/cs.pem

set -e

EC2_IP=$1
PEM="${PEM:-C:/Users/phamq/Documents/key/cs.pem}"
SSH="ssh -i $PEM -o StrictHostKeyChecking=no ubuntu@$EC2_IP"
SCP="scp -i $PEM -o StrictHostKeyChecking=no"

if [ -z "$EC2_IP" ]; then
  echo "Usage: ./deploy/deploy.sh <EC2_IP>"
  echo "Lấy IP từ: cd terraform && terraform output public_ip"
  exit 1
fi

echo "=== [1/4] Build backend (Release) ==="
dotnet publish VinhKhanh.Admin/VinhKhanh.Admin.csproj \
  -c Release -o ./publish/backend --nologo -q

echo "=== [2/4] Build frontend ==="
cd VinhKhanh.Admin.Ui
npm run build
cd ..

echo "=== [3/4] Upload lên EC2: $EC2_IP ==="
$SSH "mkdir -p ~/vinhkhanh/backend ~/vinhkhanh/frontend"

# Upload backend
rsync -az --delete -e "ssh -i $PEM -o StrictHostKeyChecking=no" \
  ./publish/backend/ ubuntu@$EC2_IP:~/vinhkhanh/backend/

# Upload frontend
rsync -az --delete -e "ssh -i $PEM -o StrictHostKeyChecking=no" \
  ./VinhKhanh.Admin.Ui/dist/ ubuntu@$EC2_IP:~/vinhkhanh/frontend/

# Upload production config
$SCP VinhKhanh.Admin/appsettings.Production.json \
  ubuntu@$EC2_IP:~/vinhkhanh/backend/appsettings.Production.json

echo "=== [4/4] Chạy migrations + Restart service ==="
$SSH "
  export PATH=\$PATH:/home/ubuntu/.dotnet
  export DOTNET_ROOT=/home/ubuntu/.dotnet
  export ASPNETCORE_ENVIRONMENT=Production

  # Chạy migrations
  cd ~/vinhkhanh/backend
  ~/.dotnet/dotnet VinhKhanh.Admin.dll &
  sleep 8
  kill %1 2>/dev/null || true

  # Restart service
  sudo systemctl restart vinhkhanh
  sleep 3
  sudo systemctl status vinhkhanh --no-pager
"

echo ""
echo "=== DEPLOY XONG ==="
echo "Admin UI: https://enormitpham.me"
echo "API:      https://enormitpham.me/api"
echo ""
echo "Nếu chưa có SSL, chạy trên EC2:"
echo "  sudo certbot --nginx -d enormitpham.me -d www.enormitpham.me"
