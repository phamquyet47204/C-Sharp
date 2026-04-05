#!/bin/bash
# Chạy 1 lần trên EC2 mới để cài môi trường
# ssh vào EC2 rồi: bash setup-ec2.sh

set -e

echo "=== Cài .NET 10 ==="
wget -q https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10.0 --runtime aspnetcore
echo 'export PATH=$PATH:$HOME/.dotnet' >> ~/.bashrc
export PATH=$PATH:$HOME/.dotnet

echo "=== Cài Nginx ==="
sudo apt-get update -q
sudo apt-get install -y nginx certbot python3-certbot-nginx

echo "=== Cài Docker (cho SQL Server) ==="
sudo apt-get install -y docker.io
sudo systemctl enable --now docker
sudo usermod -aG docker ubuntu

echo "=== Chạy SQL Server 2022 ==="
sudo docker run -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=VinhKhanh@Ec2Strong2026!" \
  -p 1433:1433 \
  --name sqlserver \
  --restart always \
  -d mcr.microsoft.com/mssql/server:2022-latest

echo "Chờ SQL Server khởi động (30s)..."
sleep 30

echo "=== Tạo thư mục app ==="
mkdir -p ~/vinhkhanh/backend ~/vinhkhanh/frontend

echo "=== Cài systemd service ==="
sudo cp ~/vinhkhanh.service /etc/systemd/system/vinhkhanh.service
sudo systemctl daemon-reload
sudo systemctl enable vinhkhanh

echo "=== Cấu hình Nginx ==="
sudo cp ~/vinhkhanh.conf /etc/nginx/sites-available/vinhkhanh
sudo ln -sf /etc/nginx/sites-available/vinhkhanh /etc/nginx/sites-enabled/vinhkhanh
sudo rm -f /etc/nginx/sites-enabled/default
sudo nginx -t && sudo systemctl reload nginx

echo "=== Cấp SSL với Let's Encrypt ==="
sudo certbot --nginx -d enormitpham.me -d www.enormitpham.me \
  --non-interactive --agree-tos -m admin@enormitpham.me

echo ""
echo "=== SETUP XONG ==="
echo "Bước tiếp theo:"
echo "1. Upload backend + frontend từ máy local: ./deploy/deploy.sh <EC2_IP> <pem>"
echo "2. Chạy migrations: ssh vào EC2 → cd ~/vinhkhanh/backend → ~/.dotnet/dotnet VinhKhanh.Admin.dll"
echo "3. Trỏ DNS enormitpham.me → $(curl -s ifconfig.me)"
