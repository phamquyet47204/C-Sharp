# ============================================================
# SEED DEMO: Tạo ShopOwner accounts + POI mẫu cho demo duyệt POI
# Chạy: .\seed_demo_approval.ps1
# Yêu cầu: Backend đang chạy tại http://localhost:5062
# ============================================================

$baseUrl = "http://localhost:5000"
$sqlServer = "localhost\SQLEXPRESS"
$database = "VinhKhanhCleanDb"

Write-Host "=== BƯỚC 1: Tạo tài khoản ShopOwner qua API ===" -ForegroundColor Cyan

# Tạo ShopOwner 1
$body1 = @{ email = "shopowner1@vinhkhanh.vn"; password = "ShopOwner@123"; fullName = "Chủ Quán Ốc Bà Năm" } | ConvertTo-Json -EscapeHandling EscapeNonAscii
try {
    $r1 = Invoke-RestMethod -Uri "$baseUrl/api/auth/register-shop" -Method POST -Body $body1 -ContentType "application/json; charset=utf-8"
    Write-Host "  [OK] Tạo shopowner1@vinhkhanh.vn thành công" -ForegroundColor Green
} catch {
    Write-Host "  [SKIP] shopowner1@vinhkhanh.vn đã tồn tại hoặc lỗi: $($_.Exception.Message)" -ForegroundColor Yellow
}

# Tạo ShopOwner 2
$body2 = @{ email = "shopowner2@vinhkhanh.vn"; password = "ShopOwner@123"; fullName = "Chủ Quán Bún Bò Cô Tư" } | ConvertTo-Json -EscapeHandling EscapeNonAscii
try {
    $r2 = Invoke-RestMethod -Uri "$baseUrl/api/auth/register-shop" -Method POST -Body $body2 -ContentType "application/json; charset=utf-8"
    Write-Host "  [OK] Tạo shopowner2@vinhkhanh.vn thành công" -ForegroundColor Green
} catch {
    Write-Host "  [SKIP] shopowner2@vinhkhanh.vn đã tồn tại hoặc lỗi: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== BƯỚC 2: Approve accounts + Insert POI mẫu qua SQL ===" -ForegroundColor Cyan

$sql = @"
USE [$database];

-- Approve cả 2 ShopOwner (bypass quy trình Admin duyệt cho demo)
UPDATE [AspNetUsers]
SET IsApproved = 1
WHERE Email IN ('shopowner1@vinhkhanh.vn', 'shopowner2@vinhkhanh.vn');

DECLARE @owner1Id NVARCHAR(450) = (SELECT Id FROM [AspNetUsers] WHERE Email = 'shopowner1@vinhkhanh.vn')
DECLARE @owner2Id NVARCHAR(450) = (SELECT Id FROM [AspNetUsers] WHERE Email = 'shopowner2@vinhkhanh.vn')

-- Xóa POI demo cũ nếu chạy lại
DELETE FROM [Pois]
WHERE BasePoiId IN (
    'demo-pending-001','demo-pending-002','demo-pending-003',
    'demo-draft-001','demo-rejected-001','demo-approved-001'
);

-- 3 POI đang chờ duyệt (Pending_Approval = 1)
INSERT INTO [Pois] (BasePoiId, CategoryCode, Latitude, Longitude, Radius, ImageUrl, Priority, IsApproved, Status, IsPremium, OwnerId, RejectionReason, CreatedAt, UpdatedAt)
VALUES
    ('demo-pending-001','FOOD_STREET', 10.76312, 106.70198, 50, 'https://placehold.co/400x300?text=Oc+Ba+Nam',   1, 0, 1, 0, @owner1Id, NULL, DATEADD(HOUR,-3,GETUTCDATE()), DATEADD(HOUR,-3,GETUTCDATE())),
    ('demo-pending-002','FOOD_STREET', 10.76289, 106.70221, 50, 'https://placehold.co/400x300?text=Bun+Bo+Co+Tu',2, 0, 1, 1, @owner2Id, NULL, DATEADD(HOUR,-2,GETUTCDATE()), DATEADD(HOUR,-2,GETUTCDATE())),
    ('demo-pending-003','FOOD_STREET', 10.76334, 106.70175, 50, 'https://placehold.co/400x300?text=Banh+Mi+Thanh',3, 0, 1, 0, @owner1Id, NULL, DATEADD(HOUR,-1,GETUTCDATE()), DATEADD(HOUR,-1,GETUTCDATE()));

-- 1 POI Draft
INSERT INTO [Pois] (BasePoiId, CategoryCode, Latitude, Longitude, Radius, ImageUrl, Priority, IsApproved, Status, IsPremium, OwnerId, RejectionReason, CreatedAt, UpdatedAt)
VALUES ('demo-draft-001','FOOD_STREET', 10.76301, 106.70210, 50, NULL, 4, 0, 0, 0, @owner2Id, NULL, GETUTCDATE(), GETUTCDATE());

-- 1 POI Rejected
INSERT INTO [Pois] (BasePoiId, CategoryCode, Latitude, Longitude, Radius, ImageUrl, Priority, IsApproved, Status, IsPremium, OwnerId, RejectionReason, CreatedAt, UpdatedAt)
VALUES ('demo-rejected-001','FOOD_STREET', 10.76278, 106.70245, 50, 'https://placehold.co/400x300?text=Che+Ba+Muoi', 5, 0, 3, 0, @owner1Id, N'Ảnh không rõ ràng, mô tả quá ngắn. Vui lòng cập nhật lại.', DATEADD(DAY,-1,GETUTCDATE()), DATEADD(DAY,-1,GETUTCDATE()));

-- 1 POI Approved
INSERT INTO [Pois] (BasePoiId, CategoryCode, Latitude, Longitude, Radius, ImageUrl, Priority, IsApproved, Status, IsPremium, OwnerId, RejectionReason, CreatedAt, UpdatedAt)
VALUES ('demo-approved-001','FOOD_STREET', 10.76355, 106.70162, 50, 'https://placehold.co/400x300?text=Hu+Tieu+Nam+Vang', 6, 1, 2, 1, @owner2Id, NULL, DATEADD(DAY,-2,GETUTCDATE()), DATEADD(DAY,-2,GETUTCDATE()));

-- Localizations
DECLARE @p1 INT = (SELECT Id FROM [Pois] WHERE BasePoiId = 'demo-pending-001')
DECLARE @p2 INT = (SELECT Id FROM [Pois] WHERE BasePoiId = 'demo-pending-002')
DECLARE @p3 INT = (SELECT Id FROM [Pois] WHERE BasePoiId = 'demo-pending-003')
DECLARE @p4 INT = (SELECT Id FROM [Pois] WHERE BasePoiId = 'demo-draft-001')
DECLARE @p5 INT = (SELECT Id FROM [Pois] WHERE BasePoiId = 'demo-rejected-001')
DECLARE @p6 INT = (SELECT Id FROM [Pois] WHERE BasePoiId = 'demo-approved-001')

DELETE FROM [PoiLocalizations] WHERE PoiId IN (@p1,@p2,@p3,@p4,@p5,@p6);

INSERT INTO [PoiLocalizations] (PoiId, LanguageCode, Name, Description) VALUES
    (@p1,'vi',N'Quán Ốc Bà Năm',       N'Quán ốc nổi tiếng trên Phố Vĩnh Khánh, chuyên các món ốc tươi ngon, giá bình dân. Mở cửa 16:00–23:00.'),
    (@p1,'en','Ba Nam Snail Restaurant', 'Famous snail restaurant on Vinh Khanh Street with fresh seafood at affordable prices.'),
    (@p2,'vi',N'Quán Bún Bò Cô Tư',     N'Bún bò Huế đặc sản, nước dùng đậm đà, thịt bò tươi. Hơn 20 năm trên phố Vĩnh Khánh.'),
    (@p2,'en','Co Tu Beef Noodle Soup',  'Authentic Hue-style beef noodle soup. Over 20 years of tradition on Vinh Khanh Street.'),
    (@p3,'vi',N'Bánh Mì Thanh',          N'Bánh mì Sài Gòn truyền thống với pate, chả lụa, rau thơm. Giá từ 20.000đ.'),
    (@p3,'en','Thanh Banh Mi',           'Traditional Saigon-style banh mi with pate, Vietnamese sausage, fresh herbs.'),
    (@p4,'vi',N'Quán Chưa Đặt Tên',     N'Đang soạn thảo nội dung...'),
    (@p5,'vi',N'Chè Bà Mười',           N'Chè.'),
    (@p5,'en','Ba Muoi Dessert',         'Dessert.'),
    (@p6,'vi',N'Hủ Tiếu Nam Vang',      N'Hủ tiếu Nam Vang chuẩn vị, nước dùng trong vắt từ xương heo hầm 8 tiếng. Topping: tôm, mực, thịt bằm.'),
    (@p6,'en','Nam Vang Noodle Soup',    'Authentic Phnom Penh-style noodle soup with pork bone broth simmered for 8 hours.');

-- Kết quả
SELECT
    p.BasePoiId,
    CASE p.Status WHEN 0 THEN 'Draft' WHEN 1 THEN 'Pending_Approval' WHEN 2 THEN 'Approved' WHEN 3 THEN 'Rejected' WHEN 4 THEN 'Hidden' END AS [Status],
    p.IsPremium,
    l.Name AS [TênPOI],
    u.FullName AS [ShopOwner]
FROM [Pois] p
LEFT JOIN [PoiLocalizations] l ON l.PoiId = p.Id AND l.LanguageCode = 'vi'
LEFT JOIN [AspNetUsers] u ON u.Id = p.OwnerId
WHERE p.BasePoiId IN ('demo-pending-001','demo-pending-002','demo-pending-003','demo-draft-001','demo-rejected-001','demo-approved-001')
ORDER BY p.CreatedAt;
"@

# Chạy SQL qua sqlcmd
$sqlFile = [System.IO.Path]::GetTempFileName() + ".sql"
[System.IO.File]::WriteAllText($sqlFile, $sql, [System.Text.Encoding]::UTF8)

$result = sqlcmd -S $sqlServer -d $database -E -i $sqlFile -W 2>&1
Remove-Item $sqlFile -Force

if ($LASTEXITCODE -eq 0) {
    Write-Host "  [OK] SQL chạy thành công" -ForegroundColor Green
    Write-Host $result
} else {
    Write-Host "  [ERROR] SQL thất bại:" -ForegroundColor Red
    Write-Host $result
    exit 1
}

Write-Host ""
Write-Host "=== HOÀN TẤT ===" -ForegroundColor Green
Write-Host "Tài khoản demo:" -ForegroundColor White
Write-Host "  shopowner1@vinhkhanh.vn  /  ShopOwner@123  (Chủ Quán Ốc Bà Năm)"
Write-Host "  shopowner2@vinhkhanh.vn  /  ShopOwner@123  (Chủ Quán Bún Bò Cô Tư)"
Write-Host ""
Write-Host "POI đã tạo:"
Write-Host "  3x Pending_Approval  →  vào /admin/approvals để duyệt"
Write-Host "  1x Draft             →  ShopOwner chưa gửi duyệt"
Write-Host "  1x Rejected          →  đã bị từ chối kèm lý do"
Write-Host "  1x Approved          →  đã được duyệt"
