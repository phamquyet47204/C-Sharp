# Export data từ SQL Server local → Import lên EC2
# Chạy: .\deploy\migrate-data.ps1

$PEM    = "C:\Users\phamq\Documents\key\cs.pem"
$EC2    = "ubuntu@18.139.184.43"
$LOCAL  = "localhost\SQLEXPRESS"
$DB     = "VinhKhanhCleanDb"
$OUTDIR = "$PSScriptRoot\data-export"

New-Item -ItemType Directory -Force -Path $OUTDIR | Out-Null

Write-Host "=== [1/4] Export từ SQL Server local ===" -ForegroundColor Cyan

# Hàm chạy SQL qua .NET SqlClient (không cần module SqlServer)
function Invoke-LocalSql {
    param([string]$Query)
    $connStr = "Server=$LOCAL;Database=$DB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
    $conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $Query
    $cmd.CommandTimeout = 60
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $table = New-Object System.Data.DataTable
    $adapter.Fill($table) | Out-Null
    $conn.Close()
    return $table.Rows
}

# Export Pois
Write-Host "  Exporting Pois..." -ForegroundColor Gray
$pois = Invoke-LocalSql "SELECT Id, BasePoiId, Latitude, Longitude, Priority, IsApproved, CreatedAt, UpdatedAt, OwnerSecretInfo, Radius, ImageUrl, CategoryCode, Status, OwnerId, IsPremium, RejectionReason FROM Pois"
Write-Host "  -> $($pois.Count) POIs"

# Export PoiLocalizations
Write-Host "  Exporting PoiLocalizations..." -ForegroundColor Gray
$locs = Invoke-LocalSql "SELECT Id, PoiId, LanguageCode, Name, Description, AudioUrl FROM PoiLocalizations"
Write-Host "  -> $($locs.Count) localizations"

# Export AspNetUsers (chỉ ShopOwner, không export Admin để tránh conflict)
Write-Host "  Exporting ShopOwner users..." -ForegroundColor Gray
$users = Invoke-LocalSql @"
SELECT u.Id, u.UserName, u.NormalizedUserName, u.Email, u.NormalizedEmail,
       u.EmailConfirmed, u.PasswordHash, u.SecurityStamp, u.ConcurrencyStamp,
       u.PhoneNumberConfirmed, u.TwoFactorEnabled,
       u.LockoutEnabled, u.AccessFailedCount,
       u.FullName, u.IsApproved, u.ActivationDate
FROM AspNetUsers u
INNER JOIN AspNetUserRoles ur ON u.Id = ur.UserId
INNER JOIN AspNetRoles r ON ur.RoleId = r.Id
WHERE r.Name = 'ShopOwner'
"@
Write-Host "  -> $($users.Count) ShopOwner users"

# Export AspNetUserRoles cho ShopOwners
$userRoles = Invoke-LocalSql @"
SELECT ur.UserId, ur.RoleId
FROM AspNetUserRoles ur
INNER JOIN AspNetUsers u ON ur.UserId = u.Id
INNER JOIN AspNetUserRoles ur2 ON u.Id = ur2.UserId
INNER JOIN AspNetRoles r ON ur2.RoleId = r.Id
WHERE r.Name = 'ShopOwner'
"@

Write-Host "=== [2/4] Tạo SQL import script ===" -ForegroundColor Cyan

$sql = @"
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
USE VinhKhanhCleanDb;

-- Import ShopOwner users TRƯỚC (để FK OwnerId không bị lỗi)
"@

# Import users trước
if ($users.Count -gt 0) {
    foreach ($u in $users) {
        $email    = $u.Email        -replace "'","''"
        $normEmail= $u.NormalizedEmail -replace "'","''"
        $uname    = $u.UserName     -replace "'","''"
        $normName = $u.NormalizedUserName -replace "'","''"
        $pwHash   = $u.PasswordHash -replace "'","''"
        $secStamp = $u.SecurityStamp -replace "'","''"
        $concStamp= $u.ConcurrencyStamp -replace "'","''"
        $fullName = $u.FullName     -replace "'","''"
        $actDate  = if ($u.ActivationDate -and $u.ActivationDate -ne [DBNull]::Value) { "'$($u.ActivationDate.ToString("yyyy-MM-dd HH:mm:ss"))'"} else { "NULL" }

        $sql += @"
IF NOT EXISTS (SELECT 1 FROM AspNetUsers WHERE Id='$($u.Id)')
INSERT INTO AspNetUsers (Id,UserName,NormalizedUserName,Email,NormalizedEmail,EmailConfirmed,PasswordHash,SecurityStamp,ConcurrencyStamp,PhoneNumberConfirmed,TwoFactorEnabled,LockoutEnabled,AccessFailedCount,FullName,IsApproved,ActivationDate)
VALUES ('$($u.Id)','$uname','$normName','$email','$normEmail',$([int]$u.EmailConfirmed),'$pwHash','$secStamp','$concStamp',0,0,0,0,'$fullName',$([int]$u.IsApproved),$actDate);

"@
    }
    $sql += "DECLARE @shopRoleId NVARCHAR(450) = (SELECT Id FROM AspNetRoles WHERE Name='ShopOwner');`n"
    foreach ($ur in $userRoles) {
        $sql += "IF NOT EXISTS (SELECT 1 FROM AspNetUserRoles WHERE UserId='$($ur.UserId)' AND RoleId=@shopRoleId) INSERT INTO AspNetUserRoles VALUES ('$($ur.UserId)',@shopRoleId);`n"
    }
}

$sql += @"

SET IDENTITY_INSERT Pois ON;

-- Xóa data cũ (giữ lại structure)
DELETE FROM PoiLocalizations;
DELETE FROM Pois;

-- Import Pois
"@

foreach ($p in $pois) {
    $basePoiId  = $p.BasePoiId -replace "'","''"
    $ownerInfo  = if ($p.OwnerSecretInfo -and $p.OwnerSecretInfo.ToString().Trim() -ne '') { "'$($p.OwnerSecretInfo -replace "'","''")'"}  else { "NULL" }
    $imageUrl   = if ($p.ImageUrl -and $p.ImageUrl.ToString().Trim() -ne '')               { "'$($p.ImageUrl -replace "'","''")'"}          else { "NULL" }
    $catCode    = if ($p.CategoryCode -and $p.CategoryCode.ToString().Trim() -ne '')        { "'$($p.CategoryCode -replace "'","''")'"}      else { "'FOOD_STREET'" }
    $ownerId    = if ($p.OwnerId -and $p.OwnerId.ToString().Trim() -ne '')                  { "'$($p.OwnerId -replace "'","''")'"}           else { "NULL" }
    $rejReason  = if ($p.RejectionReason -and $p.RejectionReason.ToString().Trim() -ne '')  { "'$($p.RejectionReason -replace "'","''")'"}  else { "NULL" }
    $createdAt  = $p.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
    $updatedAt  = $p.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss")

    $sql += @"

INSERT INTO Pois (Id,BasePoiId,Latitude,Longitude,Priority,IsApproved,CreatedAt,UpdatedAt,OwnerSecretInfo,Radius,ImageUrl,CategoryCode,Status,OwnerId,IsPremium,RejectionReason)
VALUES ($($p.Id),'$basePoiId',$($p.Latitude),$($p.Longitude),$($p.Priority),$([int]$p.IsApproved),'$createdAt','$updatedAt',$ownerInfo,$($p.Radius),$imageUrl,$catCode,$($p.Status),$ownerId,$([int]$p.IsPremium),$rejReason);
"@
}

$sql += "`nSET IDENTITY_INSERT Pois OFF;`n"
$sql += "SET IDENTITY_INSERT PoiLocalizations ON;`n"

foreach ($l in $locs) {
    $name   = $l.Name        -replace "'","''"
    $desc   = $l.Description -replace "'","''"
    $audio  = if ($l.AudioUrl) { "'$($l.AudioUrl -replace "'","''")'"}  else { "NULL" }
    $sql += "INSERT INTO PoiLocalizations (Id,PoiId,LanguageCode,Name,Description,AudioUrl) VALUES ($($l.Id),$($l.PoiId),'$($l.LanguageCode)','$name','$desc',$audio);`n"
}

$sql += "SET IDENTITY_INSERT PoiLocalizations OFF;`n"

$sql += "`nPRINT 'Import complete';`n"

# Lưu file
$sqlFile = "$OUTDIR\import.sql"
$sql | Out-File -FilePath $sqlFile -Encoding UTF8
Write-Host "  SQL file: $sqlFile ($([int]((Get-Item $sqlFile).Length/1KB)) KB)"

Write-Host "=== [3/4] Upload lên EC2 ===" -ForegroundColor Cyan
scp -i $PEM -o StrictHostKeyChecking=no $sqlFile "${EC2}:/home/ubuntu/import.sql"
Write-Host "  Uploaded"

Write-Host "=== [4/4] Chạy import trên EC2 ===" -ForegroundColor Cyan
ssh -i $PEM -o StrictHostKeyChecking=no $EC2 "docker cp /home/ubuntu/import.sql sqlserver:/import.sql && docker exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'VinhKhanh@Ec2Strong2026!' -C -i /import.sql 2>&1 | tail -5"

Write-Host ""
Write-Host "=== XONG ===" -ForegroundColor Green
Write-Host "Kiểm tra: https://enormitpham.me/api/admin/pois (sau khi login)"
