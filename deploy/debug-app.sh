#!/bin/bash
sudo systemctl stop vinhkhanh 2>/dev/null
cd ~/vinhkhanh/backend
export ASPNETCORE_ENVIRONMENT=Production
export DOTNET_ROOT=/home/ubuntu/.dotnet
export PATH=/home/ubuntu/.dotnet:$PATH
timeout 12 /home/ubuntu/.dotnet/dotnet VinhKhanh.Admin.dll > /tmp/app-debug.log 2>&1
cat /tmp/app-debug.log
