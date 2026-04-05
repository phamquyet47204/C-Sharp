#!/bin/bash
BACKEND_DIR=~/vinhkhanh/backend
CONFIG=$BACKEND_DIR/appsettings.json

echo "=== Patch connection string ==="
python3 -c "
import json
with open('$CONFIG', 'r') as f:
    config = json.load(f)

# Thêm Encrypt=False để tránh SSL issue với SQL Server Docker
config['ConnectionStrings']['Default'] = 'Server=127.0.0.1,1433;Database=VinhKhanhCleanDb;User Id=sa;Password=VinhKhanh@Ec2Strong2026!;TrustServerCertificate=True;Encrypt=False'

with open('$CONFIG', 'w') as f:
    json.dump(config, f, indent=2)

print('Patched OK')
print(config['ConnectionStrings']['Default'])
"

echo "=== Restart service ==="
sudo systemctl restart vinhkhanh
sleep 35
sudo journalctl -u vinhkhanh -n 10 --no-pager 2>&1 | grep -v 'warn:\|No store type'
echo "=== Test API ==="
curl -s http://localhost:5000/api/admin/pois 2>&1 | head -c 200
