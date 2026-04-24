# VinhKhanh — Product Requirements Document (PRD)

> **Phiên bản:** 2.0 | **Ngày:** 24/04/2026
> **Nền tảng:** ASP.NET Core 10 · React + Vite · .NET MAUI Android
> **Tài liệu chi tiết từng nhóm:** xem thư mục [`docs/`](./docs/)

---

## Mục lục

1. [Tổng quan sản phẩm](#1-tổng-quan-sản-phẩm)
2. [Actors & Phân quyền](#2-actors--phân-quyền)
3. [Danh sách Feature](#3-danh-sách-feature)
4. [Use Case Diagram tổng quan](#4-use-case-diagram-tổng-quan)
5. [Class Diagram tổng quan](#5-class-diagram-tổng-quan)
6. [ERD tổng quan](#6-erd-tổng-quan)
7. [Sequence Diagrams](#7-sequence-diagrams)
8. [Activity Diagrams](#8-activity-diagrams)

---

## 1. Tổng quan sản phẩm

**VinhKhanh** là nền tảng du lịch ẩm thực thông minh. Khi du khách đi bộ qua một quán ăn đã đăng ký, điện thoại tự động phát thuyết minh giới thiệu quán bằng ngôn ngữ của họ — không cần mở app, không cần thao tác.

### Kiến trúc hệ thống

```
┌──────────────────────────────────────────────────────────────┐
│  Presentation   │ Admin Web UI (React)  │ Mobile (.NET MAUI) │
├──────────────────────────────────────────────────────────────┤
│  Application    │ AdminApproveUseCase · AnalyticsVisitUseCase · PoiSyncUseCase │
├──────────────────────────────────────────────────────────────┤
│  Domain         │ Poi · ApplicationUser · Payment · AnalyticsEvent · ...       │
├──────────────────────────────────────────────────────────────┤
│  Infrastructure │ AppDbContext (EF Core) · GeminiAiService · EncryptionUtility │
└──────────────────────────────────────────────────────────────┘
```

---

## 2. Actors & Phân quyền

| Actor | Mô tả | Điều kiện truy cập |
|---|---|---|
| **Admin** | Quản trị viên hệ thống | JWT với role `Admin` |
| **ShopOwner** | Chủ quán đăng ký trên hệ thống | JWT với role `ShopOwner` + `IsApproved=true` |
| **Visitor** | Du khách dùng app mobile | Không cần đăng nhập (hoặc JWT role `Visitor`) |

**Luồng phân quyền quan trọng:**
- `ShopOwner` đăng ký → `IsApproved=false` → Admin duyệt → `IsApproved=true` → mới đăng nhập được
- `Visitor` đăng ký → `IsApproved=true` ngay lập tức, `ActivationDate=UtcNow`
- Mọi request của `ShopOwner` đều gọi `IsApprovedAsync()` kiểm tra DB thực tế (không chỉ dựa vào JWT)

---

## 3. Danh sách Feature

Hệ thống có **18 feature** chia thành 6 nhóm:

### Nhóm A — Xác thực & Tài khoản (3 feature)

| # | Feature | Actor | Mô tả |
|---|---|---|---|
| F01 | **Đăng nhập** | Tất cả | Xác thực email/password, nhận JWT 24h. Chặn ShopOwner chưa được duyệt (403). |
| F02 | **Đăng ký tài khoản ShopOwner** | ShopOwner | Tạo tài khoản chờ Admin duyệt. `IsApproved=false`, role `ShopOwner`. |
| F03 | **Đăng ký tài khoản Visitor** | Visitor | Tạo tài khoản tự động kích hoạt. `IsApproved=true`, `ActivationDate=now`. |

### Nhóm B — Quản lý POI (5 feature)

| # | Feature | Actor | Mô tả |
|---|---|---|---|
| F04 | **Admin quản lý POI toàn hệ thống** | Admin | Xem, tạo, sửa, xóa bất kỳ POI nào. Admin tạo POI → `Status=Approved` ngay. Sinh `QrToken` tự động. |
| F05 | **Duyệt / Từ chối / Ẩn POI** | Admin | Chuyển trạng thái POI: `Pending_Approval → Approved/Rejected/Hidden`. Từ chối phải có lý do ≥ 10 ký tự. |
| F06 | **ShopOwner quản lý POI của mình** | ShopOwner | Tạo POI ở `Status=Draft`. Chỉ sửa/xóa khi `Draft` hoặc `Rejected`. Không xóa khi `Pending_Approval`. |
| F07 | **Gửi POI để Admin duyệt** | ShopOwner | Chuyển `Draft → Pending_Approval`. Chỉ được gửi khi đang ở `Draft`. |
| F08 | **AI dịch thuật nội dung POI** | Admin, ShopOwner | Nhập tên + mô tả tiếng Việt → Gemini API dịch sang tiếng Anh và tiếng Nhật. Fallback qua 4 model: `gemini-2.5-flash → gemini-1.5-flash → gemini-2.0-flash → gemini-2.5-flash-lite`. |

### Nhóm C — Trải nghiệm Mobile (4 feature)

| # | Feature | Actor | Mô tả |
|---|---|---|---|
| F09 | **Đồng bộ POI offline** | Visitor | Delta sync: chỉ tải POI thay đổi sau `lastSync`. Lưu SQLite local. Hoạt động offline khi mất mạng. Pruning tự động xóa POI đã bị xóa trên server. |
| F10 | **Thuyết minh tự động khi đến gần quán** | Visitor | GPS → Haversine distance → Debounce 2 lần → Cooldown 10 phút → chọn POI Priority cao nhất → phát MP3 hoặc TTS. Audio ducking giảm nhạc nền khi phát. |
| F11 | **Quét QR code để xem thông tin quán** | Visitor | Quét QR → resolve token → lấy thông tin POI + deep link `vinhkhanh://poi/{id}`. Redirect sang web nếu chưa cài app. |
| F12 | **Đánh giá POI bằng sao** | Visitor | Upsert rating 1–5 sao theo `DeviceId`. Mỗi thiết bị chỉ có 1 rating/POI. Hiển thị điểm trung bình và số lượt đánh giá. |

### Nhóm D — Kiểm soát truy cập & Thanh toán (3 feature)

| # | Feature | Actor | Mô tả |
|---|---|---|---|
| F13 | **Dùng thử miễn phí 3 POI** | Visitor | 3 POI đầu tiên nghe miễn phí. Ghi `FreeTrialRecord` sau mỗi lần nghe. Unique index `(DeviceId, PoiId)`. |
| F14 | **Dùng thử thiết bị 7 ngày** | Visitor | Thiết bị mới tự động kích hoạt `DeviceTrial` 7 ngày. Nghe không giới hạn trong thời gian thử. |
| F15 | **Mua Access Pass** | Visitor | Thanh toán → `Payment(Status=Pending)` → callback → `Status=Completed, ExpiryDate=+7 ngày`. Nghe không giới hạn. |

### Nhóm E — Analytics & Dashboard (2 feature)

| # | Feature | Actor | Mô tả |
|---|---|---|---|
| F16 | **Dashboard tổng quan Admin** | Admin | Số POI, lượt visit, lượt narration, người online (5 phút), biểu đồ hoạt động 8 giờ, số ShopOwner chờ duyệt. |
| F17 | **Heatmap & Content Performance** | Admin | Heatmap tọa độ theo ngày/khoảng ngày. Top POI theo lượt nghe. Realtime dashboard qua SignalR (throttle 1s). Mật độ = `uniqueDevices × 100 / 121m²`. |

### Nhóm F — Quản trị hệ thống (1 feature)

| # | Feature | Actor | Mô tả |
|---|---|---|---|
| F18 | **Cài đặt hệ thống** | Admin | Quản lý key-value config: URL frontend, link tải app Android/iOS. Dùng cho QR redirect và deep link. |

---
## 4. Use Case Diagram tổng quan

```mermaid
flowchart LR
  Admin["👤 Admin"]
  Shop["👤 ShopOwner"]
  Visitor["👤 Visitor"]

  subgraph Auth["A — Xác thực & Tài khoản"]
    F01["F01 Đăng nhập"]
    F02["F02 Đăng ký ShopOwner"]
    F03["F03 Đăng ký Visitor"]
  end

  subgraph POIMgmt["B — Quản lý POI"]
    F04["F04 Admin quản lý POI toàn hệ thống"]
    F05["F05 Duyệt / Từ chối / Ẩn POI"]
    F06["F06 ShopOwner quản lý POI của mình"]
    F07["F07 Gửi POI để Admin duyệt"]
    F08["F08 AI dịch thuật nội dung POI"]
  end

  subgraph Mobile["C — Trải nghiệm Mobile"]
    F09["F09 Đồng bộ POI offline"]
    F10["F10 Thuyết minh tự động khi đến gần quán"]
    F11["F11 Quét QR code"]
    F12["F12 Đánh giá POI bằng sao"]
  end

  subgraph Access["D — Kiểm soát truy cập & Thanh toán"]
    F13["F13 Dùng thử miễn phí 3 POI"]
    F14["F14 Dùng thử thiết bị 7 ngày"]
    F15["F15 Mua Access Pass"]
  end

  subgraph Analytics["E — Analytics & Dashboard"]
    F16["F16 Dashboard tổng quan Admin"]
    F17["F17 Heatmap & Content Performance"]
  end

  subgraph System["F — Quản trị hệ thống"]
    F18["F18 Cài đặt hệ thống"]
  end

  Admin --> F01
  Admin --> F04
  Admin --> F05
  Admin --> F08
  Admin --> F16
  Admin --> F17
  Admin --> F18

  Shop --> F01
  Shop --> F02
  Shop --> F06
  Shop --> F07
  Shop --> F08

  Visitor --> F01
  Visitor --> F03
  Visitor --> F09
  Visitor --> F10
  Visitor --> F11
  Visitor --> F12
  Visitor --> F13
  Visitor --> F14
  Visitor --> F15
```

---

## 5. Class Diagram tổng quan

```mermaid
classDiagram
  class Poi {
    +int Id
    +string BasePoiId
    +string CategoryCode
    +double Latitude
    +double Longitude
    +double Radius
    +string QrToken
    +int Priority
    +bool IsApproved
    +PoiStatus Status
    +bool IsPremium
    +string OwnerId
    +string RejectionReason
    +DateTime UpdatedAt
  }
  class PoiLocalization {
    +int PoiId
    +string LanguageCode
    +string Name
    +string Description
    +string AudioUrl
  }
  class PoiRating {
    +int PoiId
    +string DeviceId
    +int Stars
    +DateTime RatedAt
  }
  class ApplicationUser {
    +string Id
    +string FullName
    +bool IsApproved
    +DateTime ActivationDate
  }
  class AnalyticsEvent {
    +int Id
    +string DeviceId
    +int PoiId
    +string EventType
    +double Latitude
    +double Longitude
    +DateTime Timestamp
  }
  class Payment {
    +string TransactionId
    +string UserId
    +PaymentType Type
    +PaymentStatus Status
    +DateTime ExpiryDate
  }
  class FreeTrialRecord {
    +string DeviceId
    +int PoiId
    +DateTime FirstHeardAt
  }
  class DeviceTrial {
    +string DeviceId
    +DateTime ExpiryDate
    +DateTime LastCheckedAt
  }
  class SystemSetting {
    +string Key
    +string Value
  }
  class IPoiRepository {
    <<interface>>
    +GetSyncPoisAsync(lastSyncAt, ct)
    +ApprovePoiAsync(id, ct)
    +GetAllActiveBaseIdsAsync(ct)
  }
  class IAnalyticsRepository {
    <<interface>>
    +AddVisitEventAsync(evt, ct)
  }
  class PoiSyncUseCase {
    +ExecuteAsync(SyncRequest, ct) SyncResponse
    -NormalizeCategoryCode()
    -InferCategory()
  }
  class AnalyticsVisitUseCase {
    +ExecuteAsync(AnalyticsVisitCommand, ct)
    -BuildAnonymousDeviceId() string
  }
  class AdminApproveUseCase {
    +ExecuteAsync(poiId, ct) bool
  }
  class GeminiAiService {
    +GenerateTranslationsAsync(name, desc, ct)
    -ExtractJsonPayload()
  }
  class GeofenceEngine {
    -EnterDebounceThreshold = 2
    -DefaultCooldown = 10min
    +StartAsync(languageCode)
    +MarkPoiAsPlayed(poiId, cooldown)
    -ProcessLocationAsync(location)
    -HandleInsidePoisWithPriorityAndDebounce()
    -CalculateDistance() double
  }
  class NarrationService {
    +PlayAudioAsync(filePath)
    +SpeakAsync(text, lang)
    +StopAll()
    -RunExclusiveNarrationAsync()
    -BeginAudioDuckingAsync()
    -SanitizeTtsText()
  }
  class DatabaseService {
    +SyncPoisFromServerAsync(ct) bool
    +GetLocalizedPoisAsync(langCode)
    -ApplyServerChangesAsync()
    -SelectByFallback()
    -NormalizeLanguageCode()
  }
  class AccessService {
    +DeviceId string
    +HasActivePass() bool
    +SyncTrialStatusAsync()
    -GetPersistentDeviceId()
  }

  Poi "1" --> "*" PoiLocalization : Cascade
  Poi "1" --> "*" PoiRating : Cascade
  Poi "1" --> "*" AnalyticsEvent
  Poi "1" --> "*" FreeTrialRecord
  ApplicationUser "1" --> "*" Poi : OwnerId
  ApplicationUser "1" --> "*" Payment : UserId

  PoiSyncUseCase --> IPoiRepository
  AdminApproveUseCase --> IPoiRepository
  AnalyticsVisitUseCase --> IAnalyticsRepository

  GeofenceEngine --> DatabaseService
  GeofenceEngine --> NarrationService : OnPoiEntered
  NarrationService --> AccessService
```

---

## 6. ERD tổng quan

```mermaid
erDiagram
  ApplicationUser {
    string Id PK
    string UserName
    string Email
    string FullName
    int PoiId "nullable FK"
    bool IsApproved
    datetime ActivationDate
  }
  Poi {
    int Id PK
    string BasePoiId
    string CategoryCode
    double Latitude
    double Longitude
    double Radius
    string ImageUrl "nullable"
    string QrToken "nullable"
    int Priority
    bool IsApproved
    int Status "enum PoiStatus"
    bool IsPremium
    datetime PremiumExpiryDate "nullable"
    string OwnerId "nullable FK"
    string RejectionReason "nullable"
    datetime CreatedAt
    datetime UpdatedAt
  }
  PoiLocalization {
    int Id PK
    int PoiId FK
    string LanguageCode
    string Name
    string Description
    string AudioUrl "nullable"
  }
  PoiRating {
    int Id PK
    int PoiId FK
    string DeviceId
    int Stars
    datetime RatedAt
    double Latitude "nullable"
    double Longitude "nullable"
  }
  AnalyticsEvent {
    int Id PK
    double Latitude
    double Longitude
    datetime Timestamp
    string DeviceId
    int PoiId "nullable FK"
    string EventType "nullable"
  }
  Payment {
    int Id PK
    string TransactionId "unique"
    string UserId FK
    decimal Amount
    int Type "enum PaymentType"
    int PoiId "nullable FK"
    int Status "enum PaymentStatus"
    datetime ExpiryDate "nullable"
    datetime CreatedAt
  }
  FreeTrialRecord {
    int Id PK
    string UserId "nullable"
    string DeviceId "nullable"
    int PoiId FK
    datetime FirstHeardAt
  }
  DeviceTrial {
    string DeviceId PK
    datetime TrialStartDate
    datetime ExpiryDate
    datetime LastCheckedAt "nullable"
  }
  SystemSetting {
    string Key PK
    string Value
  }

  ApplicationUser ||--o{ Poi : "OwnerId NoAction"
  ApplicationUser ||--o{ Payment : "UserId Cascade"
  Poi ||--|{ PoiLocalization : "PoiId Cascade"
  Poi ||--o{ PoiRating : "PoiId Cascade"
  Poi ||--o{ AnalyticsEvent : "PoiId nullable"
  Poi ||--o{ FreeTrialRecord : "PoiId"
```

**Ràng buộc DB:**
- `PoiRating`: Unique `(DeviceId, PoiId)` · Check `Stars BETWEEN 1 AND 5`
- `FreeTrialRecord`: Unique `(UserId, PoiId)` khi `UserId IS NOT NULL` · Unique `(DeviceId, PoiId)` khi `DeviceId IS NOT NULL`
- `Payment.TransactionId`: Unique index
- `PoiStatus`: `Draft=0, Pending_Approval=1, Approved=2, Rejected=3, Hidden=4`
- `PaymentType`: `AccessPass=0, PremiumUpgrade=1`
- `PaymentStatus`: `Pending=0, Completed=1, Failed=2, Refunded=3`

---
## 7. Sequence Diagrams

### F01 — Đăng nhập

**Logic:** `FindByEmailAsync` → `CheckPasswordAsync` → kiểm tra `IsApproved` → tạo JWT 24h với claims `NameIdentifier`, `Role`, `ActivationDate`.

```mermaid
sequenceDiagram
  actor User
  participant AuthController
  participant UserManager
  participant DB

  User->>AuthController: POST /api/auth/login {email, password}
  AuthController->>UserManager: FindByEmailAsync(email)
  UserManager->>DB: SELECT AspNetUsers WHERE Email=email
  DB-->>AuthController: user hoặc null

  alt user null hoặc password sai
    AuthController-->>User: 401 Tài khoản hoặc mật khẩu không chính xác
  else IsApproved == false
    AuthController-->>User: 403 Tài khoản đang chờ Admin duyệt
  else
    AuthController->>UserManager: GetRolesAsync(user)
    DB-->>AuthController: roles
    AuthController->>AuthController: Tạo JWT 24h - Claims: NameIdentifier, Role, ActivationDate
    AuthController-->>User: 200 {token, expiration, roles}
  end
```

---

### F04 + F05 — Admin tạo POI và duyệt POI của ShopOwner

**Logic tạo (Admin):** `Status=Approved` ngay, sinh `BasePoiId=Guid[..10]`, `QrToken=poi-Guid[..20]`, upload ảnh vào `wwwroot/media/`, tạo 3 `PoiLocalization` (vi/en/ja).

**Logic duyệt:** set `Status=Approved, IsApproved=true, UpdatedAt=UtcNow` → Mobile sẽ nhận POI này trong lần sync tiếp theo.

**Logic từ chối:** validate `reason.Length >= 10` → set `Status=Rejected, RejectionReason=reason`.

```mermaid
sequenceDiagram
  actor Admin
  participant AdminController
  participant DB
  participant FileSystem

  Admin->>AdminController: POST /api/admin/pois (multipart/form-data)
  AdminController->>AdminController: NormalizeCategoryCode - kiểm tra set hợp lệ hoặc InferCategory từ text
  AdminController->>DB: INSERT Poi (Status=Approved, IsApproved=true, QrToken=poi-Guid20)
  DB-->>AdminController: poiId
  AdminController->>FileSystem: UploadFileAsync(image, "img") - lưu wwwroot/media/img_{guid}.ext
  FileSystem-->>AdminController: imageUrl
  AdminController->>DB: INSERT PoiLocalization x3 (vi, en, ja)
  AdminController-->>Admin: 200 {poiId, qrToken, qrLink}

  Admin->>AdminController: GET /api/admin/pois/pending
  AdminController->>DB: SELECT Poi WHERE Status=Pending_Approval ORDER BY CreatedAt ASC
  DB-->>AdminController: List POI
  AdminController-->>Admin: Danh sách chờ duyệt

  alt Duyệt
    Admin->>AdminController: POST /api/admin/pois/{id}/approve
    AdminController->>DB: UPDATE Poi SET Status=Approved, IsApproved=true, UpdatedAt=now
    AdminController-->>Admin: 200 success
  else Từ chối
    Admin->>AdminController: POST /api/admin/pois/{id}/reject {reason}
    AdminController->>AdminController: Validate reason.Length >= 10
    AdminController->>DB: UPDATE Poi SET Status=Rejected, RejectionReason=reason
    AdminController-->>Admin: 200 success
  end
```

---

### F06 + F07 — ShopOwner tạo và gửi duyệt POI

**Logic tạo:** `IsApprovedAsync()` kiểm tra DB thực tế → `Status=Draft, IsApproved=false, OwnerId=CurrentUserId`.

**Logic sửa:** chỉ cho phép khi `Status == Draft || Status == Rejected`. Chặn khi `Pending_Approval` hoặc `Approved`.

**Logic gửi duyệt:** chỉ cho phép khi `Status == Draft` → chuyển sang `Pending_Approval`.

```mermaid
sequenceDiagram
  actor ShopOwner
  participant ShopController
  participant DB

  ShopOwner->>ShopController: POST /api/shop/pois (multipart/form-data)
  ShopController->>ShopController: IsApprovedAsync() - query DB kiểm tra user.IsApproved
  alt IsApproved == false
    ShopController-->>ShopOwner: 403 Tài khoản chưa được duyệt
  else
    ShopController->>DB: INSERT Poi (Status=Draft, OwnerId=CurrentUserId, IsApproved=false)
    ShopController->>DB: INSERT PoiLocalization x3 (vi, en, ja)
    ShopController-->>ShopOwner: 200 {poiId}
  end

  ShopOwner->>ShopController: POST /api/shop/pois/{id}/submit
  ShopController->>DB: SELECT Poi WHERE Id=id
  DB-->>ShopController: poi
  ShopController->>ShopController: Kiểm tra OwnerId == CurrentUserId
  ShopController->>ShopController: Kiểm tra Status == Draft
  ShopController->>DB: UPDATE Poi SET Status=Pending_Approval, UpdatedAt=now
  ShopController-->>ShopOwner: 200 success
```

---

### F08 — AI dịch thuật

**Logic:** Thử lần lượt 4 model Gemini. Mỗi model retry tối đa 2 lần với delay tăng dần (2s → 4s). Parse JSON từ `candidates[0].content.parts[0].text`, loại bỏ markdown wrapper bằng `ExtractJsonPayload`.

```mermaid
sequenceDiagram
  actor User as Admin hoặc ShopOwner
  participant Controller
  participant GeminiAiService
  participant GeminiAPI

  User->>Controller: POST /api/admin/ai/generate {name, description}
  Controller->>GeminiAiService: GenerateTranslationsAsync(name, description, ct)

  loop Fallback: gemini-2.5-flash → gemini-1.5-flash → gemini-2.0-flash → gemini-2.5-flash-lite
    loop Retry tối đa 2 lần
      GeminiAiService->>GeminiAPI: POST generateContent?key={apiKey} - temperature=0.4
      alt HTTP 200
        GeminiAPI-->>GeminiAiService: JSON response
        GeminiAiService->>GeminiAiService: ExtractJsonPayload - loại bỏ markdown wrapper
        GeminiAiService-->>Controller: GeminiTranslationResult {En, Ja}
        Controller-->>User: 200 {en:{name,desc}, ja:{name,desc}}
      else HTTP 503 hoặc 429 - còn retry
        GeminiAiService->>GeminiAiService: Task.Delay(2000ms * 2^attempt)
      else HTTP 503 hoặc 429 - hết retry
        GeminiAiService->>GeminiAiService: Break - chuyển model tiếp theo
      end
    end
  end
```

---

### F09 — Đồng bộ POI offline

**Logic:** `GetLastSyncTime()` từ `Preferences` → GET `/api/pois/updates?lastSync={ISO8601}` → `PoiSyncUseCase.ExecuteAsync` query `WHERE UpdatedAt > lastSyncAt AND Status=Approved` → `ApplyServerChangesAsync`: upsert theo `(BasePoiId, LanguageCode)` → Pruning xóa POI không trong `ActiveBasePoiIds` → lưu `ServerTime` làm mốc mới.

```mermaid
sequenceDiagram
  participant Mobile as Mobile App
  participant DatabaseService
  participant PoisController
  participant PoiSyncUseCase
  participant DB as SQL Server
  participant SQLite

  Mobile->>DatabaseService: SyncPoisFromServerAsync()
  DatabaseService->>DatabaseService: GetLastSyncTime() - Preferences.Get("root_last_sync_utc")
  DatabaseService->>PoisController: GET /api/pois/updates?lastSync={lastSync:O}
  PoisController->>PoiSyncUseCase: ExecuteAsync(SyncRequest)
  PoiSyncUseCase->>DB: SELECT Poi WHERE UpdatedAt > lastSyncAt AND Status=Approved INCLUDE Localizations
  DB-->>PoiSyncUseCase: entities
  PoiSyncUseCase->>PoiSyncUseCase: Map → Shared.Models.Poi - NormalizeCategoryCode
  PoiSyncUseCase->>DB: SELECT DISTINCT BasePoiId WHERE Status=Approved
  DB-->>PoiSyncUseCase: activeBasePoiIds
  PoiSyncUseCase-->>DatabaseService: SyncResponse {UpdatedPois, ActiveBasePoiIds, ServerTime}

  DatabaseService->>SQLite: ApplyServerChangesAsync
  Note over DatabaseService,SQLite: Upsert theo (BasePoiId, LanguageCode)
  DatabaseService->>SQLite: Pruning - DELETE WHERE BasePoiId NOT IN ActiveBasePoiIds
  DatabaseService->>DatabaseService: SaveLastSyncTime(ServerTime)
  DatabaseService-->>Mobile: true
```

---

### F10 — Thuyết minh tự động khi đến gần quán

**Logic GPS:** `ListenLoopAsync` → `GetLocationAsync(Best, 15s)` → `ShouldEmitLocationChanged` (distance ≥ 1m hoặc heartbeat 15s) → `LocationChanged.Invoke`.

**Logic Geofence:** `ProcessLocationAsync` → `CalculateDistance(Haversine)` → phân loại inside/outside → `HandleInsidePoisWithPriorityAndDebounce`: tăng counter, lọc `counter >= 2 && !active && !cooldown` → chọn `OrderByDescending(Priority).ThenBy(Id).First()` → `OnPoiEntered.Invoke`.

**Logic Narration:** `RunExclusiveNarrationAsync` → `BeginAudioDuckingAsync(AudioFocusRequest.MayDuck)` → nếu có `AudioPath`: `PlayWithMediaElementAsync(timeout=12s)`, nếu không: `SanitizeTtsText` → `TextToSpeech.SpeakAsync(Rate=0.92)` → `EndAudioDucking` → ghi `AnalyticsEvent(narration)` → `MarkPoiAsPlayed(cooldown=10min)`.

```mermaid
sequenceDiagram
  participant GPS
  participant LocationService
  participant GeofenceEngine
  participant NarrationService
  participant Backend

  GPS->>LocationService: Tọa độ mới
  LocationService->>LocationService: ShouldEmitLocationChanged - distance >= 1m hoặc heartbeat 15s
  LocationService->>GeofenceEngine: LocationChanged.Invoke(location)

  GeofenceEngine->>GeofenceEngine: ProcessLocationAsync - _processLock.WaitAsync
  GeofenceEngine->>GeofenceEngine: CalculateDistance Haversine cho từng POI trong _cachedPois
  GeofenceEngine->>GeofenceEngine: HandleExitedPois - reset _insideStableCounters
  GeofenceEngine->>GeofenceEngine: HandleInsidePoisWithPriorityAndDebounce
  Note over GeofenceEngine: counter++ - lọc counter>=2, !active, !cooldown
  Note over GeofenceEngine: selectedPoi = OrderByDescending(Priority).ThenBy(Id).First()
  GeofenceEngine->>NarrationService: OnPoiEntered.Invoke(selectedPoi)

  NarrationService->>NarrationService: RunExclusiveNarrationAsync - AudioQueueManager.CancelCurrent
  NarrationService->>NarrationService: BeginAudioDuckingAsync - AudioFocusRequest.MayDuck
  alt poi.AudioPath != null
    NarrationService->>NarrationService: PlayWithMediaElementAsync - timeout 12s
  else
    NarrationService->>NarrationService: SanitizeTtsText - loại emoji
    NarrationService->>NarrationService: TextToSpeech.SpeakAsync - Rate=0.92
  end
  NarrationService->>NarrationService: EndAudioDucking
  NarrationService->>Backend: POST /api/analytics/visit {eventType: narration, poiId}
  GeofenceEngine->>GeofenceEngine: MarkPoiAsPlayed - _cooldownUntilUtc[poiId] = now + 10min
```

---

### F13 + F14 + F15 — Kiểm soát truy cập

**Logic check:** đếm `FreeTrialRecord` distinct PoiId theo `DeviceId` → kiểm tra `DeviceTrial.ExpiryDate` → kiểm tra `Payment(Status=Completed, ExpiryDate > now)`.

**Logic auto-trial:** `SyncTrialStatusAsync` → nếu `!HasActivePass && TrialRemainingDays==0 && FreeTrialUsed==0` → `StartTrialAsync` → `DeviceTrial(ExpiryDate=now+7days)`.

```mermaid
sequenceDiagram
  participant Mobile
  participant AccessService
  participant AccessController
  participant DB

  Mobile->>AccessService: SyncTrialStatusAsync()
  AccessService->>AccessController: GET /api/access/check?deviceId={id}
  AccessController->>DB: COUNT DISTINCT PoiId FROM FreeTrialRecord WHERE DeviceId=id
  AccessController->>DB: SELECT DeviceTrial WHERE DeviceId=id
  AccessController->>DB: SELECT Payment WHERE UserId=userId AND Status=Completed AND ExpiryDate > now
  AccessController-->>AccessService: {freeTrialUsed, hasActivePass, passExpiryDate, isTrial, trialRemainingDays}

  alt Thiết bị mới - freeTrialUsed==0 AND trialRemainingDays==0 AND !hasActivePass
    AccessService->>AccessController: POST /api/access/start-trial?deviceId={id}
    AccessController->>DB: INSERT DeviceTrial (ExpiryDate=now+7days)
    AccessController-->>AccessService: {expiryDate, remainingDays: 7}
    AccessService->>AccessService: Preferences.Set("access_pass_expiry", expiryDate)
  end

  alt Mua Access Pass
    Mobile->>AccessController: POST /api/payments/initiate {transactionId, type: AccessPass}
    AccessController->>DB: INSERT Payment (Status=Pending)
    Mobile->>AccessController: POST /api/payments/callback {transactionId}
    AccessController->>DB: UPDATE Payment SET Status=Completed, ExpiryDate=CreatedAt+7days
    AccessController-->>Mobile: 200 {expiryDate}
  end
```

---

### F17 — Heatmap & Realtime Dashboard

**Logic ghi sự kiện:** `AnalyticsVisitUseCase.ExecuteAsync` → `BuildAnonymousDeviceId(SHA256)` → `INSERT AnalyticsEvent` → nếu `narration`: upsert `FreeTrialRecord` → `PublishRealtimeUpdateAsync` (throttle 1s) → `analyticsHub.Clients.Group("AdminGroup").SendAsync("analytics:realtime")`.

**Logic heatmap:** group by `(Math.Round(lat,4), Math.Round(lng,4))` → mỗi ô ≈ 11×11m = 121m² → `density = uniqueDevices × 100 / 121` → `Take(500)`.

```mermaid
sequenceDiagram
  participant Mobile
  participant AnalyticsController
  participant AnalyticsVisitUseCase
  participant DB
  participant SignalR as AnalyticsHub
  participant AdminUI

  Mobile->>AnalyticsController: POST /api/analytics/visit {eventType, poiId, lat, lng, deviceId}
  AnalyticsController->>AnalyticsVisitUseCase: ExecuteAsync(command)
  AnalyticsVisitUseCase->>AnalyticsVisitUseCase: BuildAnonymousDeviceId - SHA256 hash - prefix anon-
  AnalyticsVisitUseCase->>DB: INSERT AnalyticsEvent
  AnalyticsController->>DB: Upsert FreeTrialRecord nếu eventType=narration
  AnalyticsController->>AnalyticsController: PublishRealtimeUpdateAsync - throttle 1s
  AnalyticsController->>DB: SELECT AnalyticsEvents WHERE Timestamp >= now-45s
  AnalyticsController->>AnalyticsController: BuildHeatmapPoints - group by Round(lat,4) Round(lng,4)
  Note over AnalyticsController: density = uniqueDevices * 100 / 121
  AnalyticsController->>SignalR: Clients.Group("AdminGroup").SendAsync("analytics:realtime", payload)
  SignalR-->>AdminUI: WebSocket push {onlineCount, points, measuredAt}
  AnalyticsController-->>Mobile: 200 {success: true}
```

---
## 8. Activity Diagrams

### F05 — Vòng đời POI

```mermaid
flowchart TD
  Start([Bắt đầu]) --> Draft["Status = Draft\nShopController.CreatePoi\nIsApproved=false"]
  Draft --> EditDraft{ShopOwner chỉnh sửa?}
  EditDraft -->|UpdatePoi - Draft hoặc Rejected| Draft
  EditDraft -->|SubmitPoi - chỉ khi Draft| Pending["Status = Pending_Approval\nShopController.SubmitPoi"]
  Pending --> AdminReview{Admin xem xét}
  AdminReview --> ReviewNote["GET /api/admin/pois/pending\nOrderBy CreatedAt ASC"]
  ReviewNote --> Decision{Quyết định?}
  Decision -->|Approve - IsApproved=true| Approved["Status = Approved\nHiển thị trên Mobile\nMobile sync nhận được"]
  Decision -->|RejectPoi - reason >= 10 ký tự| Rejected["Status = Rejected\nRejectionReason lưu DB"]
  Rejected --> CanEdit{ShopOwner sửa lại?}
  CanEdit -->|UpdatePoi - cho phép khi Rejected| Draft
  CanEdit -->|Không| End1([Kết thúc])
  Approved --> HideCheck{Admin ẩn POI?}
  HideCheck -->|HidePoi - IsApproved=false| Hidden["Status = Hidden\nKhông hiển thị Mobile"]
  HideCheck -->|Không| Active["POI hoạt động\nGetSyncPoisAsync trả về cho Mobile"]
  Hidden --> End2([Kết thúc])
  Active --> End3([POI hoạt động])
```

**Ràng buộc trạng thái:**
- `UpdatePoi`: chặn khi `Pending_Approval` hoặc `Approved` → 403
- `DeletePoi`: chặn khi `Pending_Approval` → 403
- `SubmitPoi`: chặn khi không phải `Draft` → 400

---

### F10 — GeofenceEngine xử lý GPS

```mermaid
flowchart TD
  Start([GPS cập nhật vị trí]) --> LS["LocationService.ListenLoopAsync\nGetLocationAsync - GeolocationAccuracy.Best, timeout 15s"]
  LS --> ShouldEmit{ShouldEmitLocationChanged?}
  ShouldEmit --> EmitNote["distance >= 1m hoặc silent >= 15s heartbeat"]
  EmitNote --> EmitDecision{Đủ điều kiện?}
  EmitDecision -->|Không| Delay["Task.Delay - _currentInterval\nActiveInterval=15s hoặc IdleInterval=15s"]
  Delay --> LS
  EmitDecision -->|Có| Emit["LocationChanged.Invoke\nTrackActivityAsync - location_update"]
  Emit --> Process["GeofenceEngine.ProcessLocationAsync\n_processLock.WaitAsync - tuần tự"]
  Process --> Clean["CleanupExpiredCooldown\nXóa _cooldownUntilUtc hết hạn"]
  Clean --> Calc["Loop _cachedPois\nCalculateDistance Haversine\nearthRadius=6371000m"]
  Calc --> Classify{distanceMeters <= poi.Radius?}
  Classify -->|Không| Outside["outsidePois.Add\nHandleExitedPois\n_insideStableCounters[id]=0\nOnPoiExited.Invoke"]
  Classify -->|Có| Inside["insideCandidates.Add\nHandleInsidePoisWithPriorityAndDebounce"]
  Inside --> Counter["_insideStableCounters[poi.Id]++"]
  Counter --> Debounce{counter >= 2?}
  Debounce -->|Không| End1([Chờ GPS tiếp theo])
  Debounce -->|Có| ActiveCheck{poi.Id trong _activePoiIds?}
  ActiveCheck -->|Đã active| End2([Bỏ qua])
  ActiveCheck -->|Chưa| CooldownCheck{_cooldownUntilUtc còn hạn?}
  CooldownCheck -->|Còn| End3([Bỏ qua - cooldown])
  CooldownCheck -->|Hết| Select["selectedPoi = OrderByDescending(Priority).ThenBy(Id).First\nPreemption: xóa POI active Priority thấp hơn"]
  Select --> Fire["_activePoiIds.Add(selectedPoi.Id)\nOnPoiEntered.Invoke(selectedPoi)"]
  Fire --> End4([NarrationService xử lý])
  Outside --> End5([Chờ GPS tiếp theo])
```

---

### F10 — NarrationService phát thuyết minh

```mermaid
flowchart TD
  Start([Nhận POI từ OnPoiEntered]) --> AccessCheck{Có quyền truy cập?}
  AccessCheck --> AccessNote["AccessService.HasActivePass\nhoặc FreeTrialRecord < 3"]
  AccessNote --> AccessDecision{Kết quả?}
  AccessDecision -->|Không có quyền| Paywall["Hiển thị Paywall\nPOST /api/payments/initiate"]
  Paywall --> End1([Kết thúc])
  AccessDecision -->|Có quyền| Exclusive["RunExclusiveNarrationAsync\nAudioQueueManager.CancelCurrent - hủy narration cũ\n_queueLock.WaitAsync"]
  Exclusive --> Duck["BeginAudioDuckingAsync\nAndroid AudioFocusRequest.MayDuck\nTask.Delay 120ms"]
  Duck --> LangResolve["GetEffectiveLanguage\nResolveBestLocaleAsync - fallback chain"]
  LangResolve --> AudioCheck{poi.AudioPath != null?}
  AudioCheck -->|Có MP3| NormPath["NormalizeAudioPath - loại Resources/Raw/ prefix\nEnsureAudioAssetExistsAsync - kiểm tra file tồn tại"]
  NormPath --> PlayMP3["PlayWithMediaElementAsync\nmediaElement.Source = MediaSource.FromFile\nmediaElement.Play - timeout 12s"]
  AudioCheck -->|Không có| TTS["SanitizeTtsText - loại emoji\nTextToSpeech.SpeakAsync\nPitch=1.0, Rate=0.92, Volume=1.0"]
  PlayMP3 --> Wait["Chờ MediaEnded hoặc timeout 12s"]
  TTS --> Wait
  Wait --> EndDuck["EndAudioDucking\nAbandonAudioFocus"]
  EndDuck --> Log["TrackActivityAsync - narration, poiId\nPOST /api/analytics/visit"]
  Log --> Cooldown["MarkPoiAsPlayed\n_cooldownUntilUtc[poiId] = now + 10min"]
  Cooldown --> End2([Hoàn thành])
```

---

### F13 + F14 + F15 — Kiểm soát truy cập Visitor

```mermaid
flowchart TD
  Start([Visitor muốn nghe thuyết minh]) --> LocalCheck{HasActivePass local?}
  LocalCheck --> LocalNote["Preferences.Get - access_pass_expiry > UtcNow"]
  LocalNote --> LocalDecision{Còn hạn?}
  LocalDecision -->|Có| Allow1["Cho phép nghe\nPlayAudioAsync hoặc SpeakAsync"]
  Allow1 --> End1([Phát thuyết minh])
  LocalDecision -->|Không| Sync["SyncTrialStatusAsync\nGET /api/access/check?deviceId=..."]
  Sync --> ServerPass{data.HasActivePass?}
  ServerPass -->|Có| SavePass["Preferences.Set - access_pass_expiry\nAllow nghe"]
  SavePass --> End2([Phát thuyết minh])
  ServerPass -->|Không| NewDevice{Thiết bị mới?}
  NewDevice --> NewNote["freeTrialUsed==0 AND trialRemainingDays==0"]
  NewNote --> NewDecision{Kết quả?}
  NewDecision -->|Mới| AutoTrial["StartTrialAsync\nPOST /api/access/start-trial\nDeviceTrial ExpiryDate=now+7days"]
  AutoTrial --> SaveTrial["Preferences.Set - access_pass_expiry"]
  SaveTrial --> End3([Phát thuyết minh - Trial 7 ngày])
  NewDecision -->|Không mới| FreeCheck{freeTrialUsed < 3?}
  FreeCheck -->|Có| Allow3["Cho phép nghe\nLogVisit upsert FreeTrialRecord"]
  Allow3 --> End4([Phát thuyết minh - Free Trial])
  FreeCheck -->|Không - hết 3 POI| Paywall["Hiển thị màn hình mua Access Pass"]
  Paywall --> UserDecide{Người dùng?}
  UserDecide -->|Mua Pass| Buy["POST /api/payments/initiate\nPOST /api/payments/callback\nExpiryDate=CreatedAt+7days"]
  Buy --> End5([Phát thuyết minh - Access Pass])
  UserDecide -->|Bỏ qua| End6([Không phát])
```

---

### F09 — Khởi động Mobile và đồng bộ dữ liệu

```mermaid
flowchart TD
  Start([Khởi động ứng dụng]) --> Init["DatabaseService.InitializeAsync\nSQLiteAsyncConnection - _databasePath\nCreateTableAsync POI"]
  Init --> LegacyCheck{BasePoiId là số nguyên?}
  LegacyCheck -->|Có - dữ liệu cũ| Wipe["DeleteAllAsync POI\nPreferences.Remove - root_last_sync_utc"]
  Wipe --> Schema["EnsureSchemaCompatibilityAsync\nALTER TABLE POI ADD COLUMN BasePoiId"]
  LegacyCheck -->|Không| Schema
  Schema --> Normalize["NormalizeBasePoiIdsAsync\nGroup by Category+Lat+Lng → gán BasePoiId"]
  Normalize --> NetCheck{Có Internet?}
  NetCheck -->|Không| Cache["Dùng SQLite cũ\nGetLocalizedPoisAsync - langCode"]
  Cache --> GeoStart1["GeofenceEngine.StartAsync\nRefreshPoisCoreAsync - load vào RAM"]
  GeoStart1 --> End1([Offline mode])
  NetCheck -->|Có| LastSync["GetLastSyncTime\nPreferences.Get - root_last_sync_utc"]
  LastSync --> API["GET /api/pois/updates?lastSync={ISO8601}"]
  API --> APICheck{Thành công?}
  APICheck -->|Không| Cache
  APICheck -->|Có| Apply["ApplyServerChangesAsync\nUpsert theo BasePoiId+LanguageCode\nPruning - xóa ngoài ActiveBasePoiIds"]
  Apply --> SaveSync["SaveLastSyncTime - payload.ServerTime\nPreferences.Set - root_last_sync_utc"]
  SaveSync --> GeoStart2["GeofenceEngine.StartAsync\nGetLocalizedPoisAsync - SelectByFallback"]
  GeoStart2 --> End2([Online mode - dữ liệu mới nhất])
```

---

*Tài liệu chi tiết từng feature: xem thư mục [`docs/`](./docs/)*
