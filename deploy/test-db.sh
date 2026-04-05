#!/bin/bash
# Test SQL Server connection
echo "=== SQL Server Docker status ==="
docker ps | grep sqlserver

echo "=== Test TCP connection to SQL Server ==="
nc -zv localhost 1433 2>&1

echo "=== Test SQL connection ==="
docker exec sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "VinhKhanh@Ec2Strong2026!" \
  -Q "SELECT 'Connected OK'" -C -N 2>&1 | head -5

echo "=== Run app with correct env ==="
export ConnectionStrings__Default="Server=localhost,1433;Database=VinhKhanhCleanDb;User Id=sa;Password=VinhKhanh@Ec2Strong2026!;TrustServerCertificate=True"
export ASPNETCORE_ENVIRONMENT=Production
export ASPNETCORE_URLS=http://localhost:5000
export DOTNET_ROOT=/home/ubuntu/.dotnet
export PATH=$PATH:/home/ubuntu/.dotnet

cd ~/vinhkhanh/backend
timeout 20 /home/ubuntu/.dotnet/dotnet VinhKhanh.Admin.dll 2>&1 | tail -10
