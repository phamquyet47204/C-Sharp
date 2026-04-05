#!/bin/bash
# User data script — chạy tự động khi EC2 khởi động lần đầu
# Log: /var/log/vinhkhanh-setup.log

exec > /var/log/vinhkhanh-setup.log 2>&1
set -e

echo "=== [1/6] Cập nhật hệ thống ==="
apt-get update -q
apt-get upgrade -y -q

echo "=== [2/6] Cài .NET 10 ASP.NET Core Runtime ==="
wget -q https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
chmod +x /tmp/dotnet-install.sh
sudo -u ubuntu /tmp/dotnet-install.sh --channel 10.0 --runtime aspnetcore
echo 'export PATH=$PATH:/home/ubuntu/.dotnet' >> /home/ubuntu/.bashrc
echo 'export DOTNET_ROOT=/home/ubuntu/.dotnet' >> /home/ubuntu/.bashrc

echo "=== [3/6] Cài Nginx + Certbot ==="
apt-get install -y nginx certbot python3-certbot-nginx

# Nginx config cho enormitpham.me
cat > /etc/nginx/sites-available/vinhkhanh << 'NGINX_EOF'
server {
    listen 80;
    server_name enormitpham.me www.enormitpham.me;

    root /home/ubuntu/vinhkhanh/frontend;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }

    location /api/ {
        proxy_pass         http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }

    location /media/ {
        proxy_pass http://localhost:5000;
    }

    client_max_body_size 20M;
}
NGINX_EOF

ln -sf /etc/nginx/sites-available/vinhkhanh /etc/nginx/sites-enabled/vinhkhanh
rm -f /etc/nginx/sites-enabled/default
nginx -t && systemctl reload nginx

echo "=== [4/6] Cài Docker + SQL Server 2022 ==="
apt-get install -y docker.io
systemctl enable --now docker
usermod -aG docker ubuntu

docker run \
  -e "ACCEPT_EULA=Y" \
  -e "MSSQL_SA_PASSWORD=VinhKhanh@Ec2Strong2026!" \
  -p 1433:1433 \
  --name sqlserver \
  --restart always \
  -d mcr.microsoft.com/mssql/server:2022-latest

echo "Chờ SQL Server khởi động (45s)..."
sleep 45

echo "=== [5/6] Tạo thư mục app ==="
mkdir -p /home/ubuntu/vinhkhanh/backend
mkdir -p /home/ubuntu/vinhkhanh/frontend
chown -R ubuntu:ubuntu /home/ubuntu/vinhkhanh

echo "=== [6/6] Tạo systemd service ==="
cat > /etc/systemd/system/vinhkhanh.service << 'SERVICE_EOF'
[Unit]
Description=VinhKhanh ASP.NET Core API
After=network.target docker.service
Requires=docker.service

[Service]
WorkingDirectory=/home/ubuntu/vinhkhanh/backend
ExecStart=/home/ubuntu/.dotnet/dotnet VinhKhanh.Admin.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=vinhkhanh
User=ubuntu
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5000
Environment=DOTNET_ROOT=/home/ubuntu/.dotnet
Environment=PATH=/home/ubuntu/.dotnet:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin

[Install]
WantedBy=multi-user.target
SERVICE_EOF

systemctl daemon-reload
systemctl enable vinhkhanh

echo ""
echo "=== SETUP HOÀN TẤT ==="
echo "Server sẵn sàng. Chạy deploy.sh từ máy local để upload app."
