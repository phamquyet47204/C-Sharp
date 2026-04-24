# 01 — Xác thực & Phân quyền

**Controller:** `AuthController` — Route: `api/auth`  
**Không yêu cầu xác thực** (public endpoints)

---

## Danh sách chức năng

| # | Endpoint | Hàm | Mô tả ngắn |
|---|---|---|---|
| 1 | `POST /api/auth/login` | `Login(LoginRequest)` | Đăng nhập, nhận JWT 24h |
| 2 | `POST /api/auth/register-shop` | `RegisterShop(RegisterShopRequest)` | Đăng ký ShopOwner, chờ Admin duyệt |
| 3 | `POST /api/auth/register-visitor` | `RegisterVisitor(RegisterVisitorRequest)` | Đăng ký Visitor, tự động kích hoạt |

---

## 1. Login — `POST /api/auth/login`

### Logic chi tiết

```
1. userManager.FindByEmailAsync(request.Email)
   → Nếu null hoặc CheckPasswordAsync trả false → return Unauthorized("Tài khoản hoặc mật khẩu không chính xác.")

2. Kiểm tra user.IsApproved
   → Nếu false → return StatusCode(403, "Tài khoản của bạn đang chờ Admin duyệt.")
   → Lý do: ShopOwner cần Admin duyệt trước khi đăng nhập được

3. userManager.GetRolesAsync(user)
   → Lấy danh sách role: ["Admin"] | ["ShopOwner"] | ["Visitor"]

4. Tạo JWT Claims:
   - ClaimTypes.Name = user.UserName
   - ClaimTypes.NameIdentifier = user.Id  ← dùng để lấy CurrentUserId trong các controller khác
   - ClaimTypes.Role = mỗi role một claim riêng
   - "ActivationDate" = user.ActivationDate.ToString("o")  ← dùng cho logic tính phí Visitor

5. Tạo JwtSecurityToken:
   - Key: configuration["Jwt:Key"] (HMAC-SHA256)
   - Issuer/Audience: từ appsettings
   - Expires: DateTime.Now.AddHours(24)

6. Return: { token, expiration, roles }
```

### Request / Response

```json
// Request
{ "email": "owner@example.com", "password": "Password123!" }

// Response 200
{ "token": "eyJ...", "expiration": "2026-04-25T10:00:00Z", "roles": ["ShopOwner"] }

// Response 401
"Tài khoản hoặc mật khẩu không chính xác."

// Response 403
"Tài khoản của bạn đang chờ Admin duyệt."
```

### Sequence Diagram

```mermaid
sequenceDiagram
  actor User
  participant AuthController
  participant UserManager
  participant DB as Database

  User->>AuthController: POST /api/auth/login {email, password}
  AuthController->>UserManager: FindByEmailAsync(email)
  UserManager->>DB: SELECT AspNetUsers WHERE Email=email
  DB-->>UserManager: ApplicationUser hoặc null
  UserManager-->>AuthController: user

  alt user == null
    AuthController-->>User: 401 Unauthorized
  else
    AuthController->>UserManager: CheckPasswordAsync(user, password)
    UserManager-->>AuthController: bool

    alt password sai
      AuthController-->>User: 401 Unauthorized
    else
      alt user.IsApproved == false
        AuthController-->>User: 403 Chờ Admin duyệt
      else
        AuthController->>UserManager: GetRolesAsync(user)
        UserManager->>DB: SELECT AspNetRoles JOIN AspNetUserRoles WHERE UserId=user.Id
        DB-->>UserManager: List roles
        UserManager-->>AuthController: roles

        AuthController->>AuthController: Tạo Claims List
        Note over AuthController: NameIdentifier=user.Id, Role=each role, ActivationDate
        AuthController->>AuthController: new JwtSecurityToken(issuer, audience, expires=+24h, claims, HMAC-SHA256)
        AuthController-->>User: 200 {token, expiration, roles}
      end
    end
  end
```

---

## 2. RegisterShop — `POST /api/auth/register-shop`

### Logic chi tiết

```
1. userManager.FindByEmailAsync(request.Email)
   → Nếu đã tồn tại → return StatusCode(500, "Tài khoản với Email này đã tồn tại!")

2. Tạo ApplicationUser mới:
   - Email = request.Email
   - UserName = request.Email  ← Identity dùng UserName để login
   - FullName = request.FullName
   - PhoneNumber = request.PhoneNumber
   - SecurityStamp = Guid.NewGuid().ToString()  ← bắt buộc của ASP.NET Identity
   - IsApproved = false  ← QUAN TRỌNG: ShopOwner phải chờ Admin duyệt

3. userManager.CreateAsync(user, request.Password)
   → Nếu thất bại (password không đủ mạnh, v.v.) → return StatusCode(500, "Kiểm tra lại quy tắc mật khẩu.")

4. userManager.AddToRoleAsync(user, "ShopOwner")
   → Ghi vào bảng AspNetUserRoles

5. Return: { success: true, message: "Đăng ký thành công! Vui lòng chờ Admin duyệt." }
   → Không tự động đăng nhập được vì IsApproved=false
```

### Sequence Diagram

```mermaid
sequenceDiagram
  actor ShopOwner
  participant AuthController
  participant UserManager
  participant DB as Database

  ShopOwner->>AuthController: POST /api/auth/register-shop {email, password, fullName, phoneNumber}
  AuthController->>UserManager: FindByEmailAsync(email)
  UserManager->>DB: SELECT AspNetUsers WHERE Email=email
  DB-->>UserManager: null hoặc user

  alt Email đã tồn tại
    AuthController-->>ShopOwner: 500 Email đã tồn tại
  else
    AuthController->>AuthController: Tạo ApplicationUser
    Note over AuthController: IsApproved=false, SecurityStamp=NewGuid
    AuthController->>UserManager: CreateAsync(user, password)
    UserManager->>DB: INSERT AspNetUsers
    DB-->>UserManager: IdentityResult

    alt Tạo thất bại
      AuthController-->>ShopOwner: 500 Kiểm tra quy tắc mật khẩu
    else
      AuthController->>UserManager: AddToRoleAsync(user, "ShopOwner")
      UserManager->>DB: INSERT AspNetUserRoles (userId, roleId="ShopOwner")
      DB-->>UserManager: OK
      AuthController-->>ShopOwner: 200 Đăng ký thành công - Chờ Admin duyệt
    end
  end
```

---

## 3. RegisterVisitor — `POST /api/auth/register-visitor`

### Logic chi tiết

```
1. Validate input:
   - Email, Password, FullName không được rỗng → BadRequest
   - Password.Length < 6 → BadRequest "Mật khẩu phải có ít nhất 6 ký tự."

2. userManager.FindByEmailAsync(request.Email)
   → Nếu đã tồn tại → return Conflict({ error: "Email đã được sử dụng." })
   → Dùng Conflict(409) thay vì 500 để FE phân biệt được lỗi

3. Tạo ApplicationUser mới:
   - IsApproved = true  ← Visitor tự động kích hoạt, không cần duyệt
   - ActivationDate = DateTime.UtcNow  ← Mốc tính 7 ngày dùng thử

4. userManager.CreateAsync(user, request.Password)

5. userManager.AddToRoleAsync(user, "Visitor")

6. Return: { success: true, message: "Đăng ký thành công!" }
   → Visitor có thể đăng nhập ngay lập tức
```

### Sự khác biệt giữa ShopOwner và Visitor

| Thuộc tính | ShopOwner | Visitor |
|---|---|---|
| `IsApproved` | `false` (chờ Admin) | `true` (tự động) |
| `ActivationDate` | `DateTime.UtcNow` (mặc định) | `DateTime.UtcNow` (ghi rõ) |
| Role | `ShopOwner` | `Visitor` |
| Đăng nhập ngay | ❌ | ✅ |
| Validation | Không validate password length | Validate >= 6 ký tự |

### Sequence Diagram

```mermaid
sequenceDiagram
  actor Visitor
  participant AuthController
  participant UserManager
  participant DB as Database

  Visitor->>AuthController: POST /api/auth/register-visitor {email, password, fullName}

  AuthController->>AuthController: Validate input
  Note over AuthController: Email/Password/FullName không rỗng, Password >= 6 ký tự

  alt Validation thất bại
    AuthController-->>Visitor: 400 BadRequest
  else
    AuthController->>UserManager: FindByEmailAsync(email)
    UserManager->>DB: SELECT AspNetUsers WHERE Email=email
    DB-->>UserManager: null hoặc user

    alt Email đã tồn tại
      AuthController-->>Visitor: 409 Conflict - Email đã được sử dụng
    else
      AuthController->>AuthController: Tạo ApplicationUser
      Note over AuthController: IsApproved=true, ActivationDate=UtcNow
      AuthController->>UserManager: CreateAsync(user, password)
      UserManager->>DB: INSERT AspNetUsers
      DB-->>UserManager: IdentityResult

      alt Tạo thất bại
        AuthController-->>Visitor: 500 Kiểm tra quy tắc mật khẩu
      else
        AuthController->>UserManager: AddToRoleAsync(user, "Visitor")
        UserManager->>DB: INSERT AspNetUserRoles
        DB-->>UserManager: OK
        AuthController-->>Visitor: 200 Đăng ký thành công
      end
    end
  end
```

---

## Activity Diagram — Luồng xác thực tổng quát

```mermaid
flowchart TD
  Start([Người dùng muốn truy cập hệ thống]) --> HasAccount{Đã có tài khoản?}
  HasAccount -->|Chưa có - ShopOwner| RegShop["RegisterShop\nIsApproved=false\nRole=ShopOwner"]
  HasAccount -->|Chưa có - Visitor| RegVisitor["RegisterVisitor\nIsApproved=true\nActivationDate=now\nRole=Visitor"]
  HasAccount -->|Đã có| Login["Login\nFindByEmailAsync + CheckPasswordAsync"]

  RegShop --> WaitApproval["Chờ Admin duyệt\nApproveOwner - IsApproved=true"]
  WaitApproval --> Login
  RegVisitor --> Login

  Login --> CheckApproved{IsApproved?}
  CheckApproved -->|false| Block["403 Forbidden\nChờ Admin duyệt"]
  CheckApproved -->|true| GenJWT["Tạo JWT 24h\nClaims: NameIdentifier, Role, ActivationDate"]
  GenJWT --> UseSystem["Truy cập hệ thống theo Role"]

  UseSystem --> RoleCheck{Role?}
  RoleCheck -->|Admin| AdminAccess["Toàn quyền\n/api/admin/*"]
  RoleCheck -->|ShopOwner| ShopAccess["Quản lý POI của mình\n/api/shop/*"]
  RoleCheck -->|Visitor| VisitorAccess["Sync POI, Narration\n/api/pois/*, /api/access/*"]
```
