# Thiết kế — Tài liệu Triển khai Dự án VinhKhanh

## Mục lục

1. [Tổng quan](#1-tổng-quan)
2. [Kiến trúc hệ thống](#2-kiến-trúc-hệ-thống)
3. [Thành phần và Giao diện](#3-thành-phần-và-giao-diện)
4. [Mô hình dữ liệu](#4-mô-hình-dữ-liệu)
5. [Xử lý lỗi](#5-xử-lý-lỗi)
6. [Chiến lược kiểm thử](#6-chiến-lược-kiểm-thử)

---

## 1. Tổng quan

Spec `project-documentation` có mục tiêu tạo ra file `README.md` ở thư mục gốc dự án VinhKhanh — một tài liệu triển khai toàn diện bằng tiếng Việt, bao gồm:

- Danh sách đầy đủ 10 nhóm chức năng hệ thống
- Use Case Diagram (Mermaid flowchart) với 3 tác nhân
- ERD (Mermaid erDiagram) với 8 entity
- Class Diagram (Mermaid classDiagram) cho tầng Application/Domain
- 8 Sequence Diagram cho các luồng nghiệp vụ chính
- 5 Activity Diagram cho các luồng logic rẽ nhánh
- API Reference đầy đủ với ví dụ JSON request/response

**Đầu ra duy nhất:** File `README.md` tại thư mục gốc dự án (ghi đè file hiện có).

---

## 2. Kiến trúc hệ thống

### 2.1 Kiến trúc tổng thể

Dự án VinhKhanh theo mô hình **Clean Architecture** với 4 tầng:

```
┌─────────────────────────────────────────────────────┐
│                  Presentation Layer                  │
│  VinhKhanh.Admin (ASP.NET Core Web API)             │
│  VinhKhanh.Admin.Ui (React + Vite + TailwindCSS)    │
│  VinhKhanh.Mobile (.NET MAUI Android)               │
├─────────────────────────────────────────────────────┤
│                 Application Layer                    │
│  VinhKhanh.Application (Use Cases)                  │
│  AdminApproveUseCase / AnalyticsVisitUseCase        │
│  PoiSyncUseCase                                     │
├─────────────────────────────────────────────────────┤
│                   Domain Layer                       │
│  VinhKhanh.Domain (Entities + Interfaces)           │
│  IPoiRepository / IAnalyticsRepository              │
├─────────────────────────────────────────────────────┤
│               Infrastructure Layer                   │
│  VinhKhanh.Infrastructure (EF Core, SQLite/SQL)     │
│  PoiRepository / AnalyticsRepository                │
│  GeminiAiService / EncryptionUtility                │
└─────────────────────────────────────────────────────┘
```

### 2.2 Cấu trúc README.md sẽ được tạo

README.md được tổ chức theo thứ tự sau:

| Thứ tự | Section | Heading Level |
|--------|---------|---------------|
| 1 | Tiêu đề dự án + badge | H1 |
| 2 | Mục lục (Table of Contents) | H2 |
| 3 | Tổng quan dự án | H2 |
| 4 | Kiến trúc hệ thống | H2 |
| 5 | Chức năng hệ thống (10 nhóm) | H2 + H3 |
| 6 | Use Case Diagram | H2 |
| 7 | Mô hình dữ liệu (ERD) | H2 |
| 8 | Class Diagram | H2 |
| 9 | Sequence Diagrams (8 luồng) | H2 + H3 |
| 10 | Activity Diagrams (5 luồng) | H2 + H3 |
| 11 | API Reference | H2 + H3 |

---

## 3. Thành phần và Giao diện

### 3.1 Nội dung Section "Chức năng hệ thống"

10 nhóm chức năng được liệt kê dưới dạng danh sách có cấu trúc:

**Nhóm 1 — Xác thực & Phân quyền**
- Đăng nhập JWT (POST /api/auth/login), token 24h
- Đăng ký ShopOwner (POST /api/auth/register-shop) — chờ Admin duyệt
- Đăng ký Visitor (POST /api/auth/register-visitor) — tự động kích hoạt
- Phân quyền 3 role: Admin / ShopOwner / Visitor

**Nhóm 2 — Quản lý POI (Admin)**
- Xem danh sách tất cả POI (GET /api/admin/pois)
- Tạo mới POI với upload ảnh (POST /api/admin/pois)
- Cập nhật POI (PUT /api/admin/pois/{id})
- Duyệt POI (POST /api/admin/pois/{id}/approve)
- Từ chối POI kèm lý do (POST /api/admin/pois/{id}/reject)
- Ẩn POI (POST /api/admin/pois/{id}/hide)
- Xem POI chờ duyệt (GET /api/admin/pois/pending)

**Nhóm 3 — Cổng chủ quán (ShopOwner)**
- Tạo/sửa/xóa POI của mình (CRUD /api/shop/pois)
- Gửi POI để Admin duyệt (POST /api/shop/pois/{id}/submit)
- Dùng AI dịch nội dung (POST /api/shop/ai/generate)
- Xem thống kê 30 ngày (GET /api/shop/analytics)

**Nhóm 4 — Đồng bộ Mobile**
- Sync POI theo timestamp (GET /api/pois/updates?lastSync=...)
- Hỗ trợ audio mode và text-only mode (includeAudio=true/false)
- Trả SyncResponse với danh sách POI đã cập nhật và deletedIds

**Nhóm 5 — Thuyết minh tự động**
- GeofenceEngine: tính khoảng cách Haversine, debounce ≥2 lần, cooldown, priority
- NarrationService: phát MP3 (AudioUrl) hoặc TTS fallback
- Audio ducking Android khi phát thuyết minh
- Ghi AnalyticsEvent (visit/narration) sau mỗi lần phát

**Nhóm 6 — Kiểm soát truy cập**
- Free Trial: 3 POI đầu tiên miễn phí (FreeTrialRecord)
- DeviceTrial: 7 ngày dùng thử gắn với DeviceId (POST /api/access/start-trial)
- Access Pass: mua thêm 7 ngày (POST /api/payments/initiate + callback)
- Kiểm tra trạng thái (GET /api/access/check)

**Nhóm 7 — Analytics**
- Ghi nhận visit/narration (POST /api/analytics/visit)
- Heatmap theo tọa độ GPS (GET /api/analytics/heatmap)
- Content performance — top POI theo lượt nghe (GET /api/analytics/content-performance)
- Dashboard summary — tổng quan số liệu (GET /api/admin/dashboard-summary)

**Nhóm 8 — AI dịch thuật**
- Gọi Google Gemini API dịch vi → en, ja
- Admin: POST /api/admin/ai/generate
- ShopOwner: POST /api/shop/ai/generate
- Tự điền form sau khi nhận kết quả dịch

**Nhóm 9 — QR Code**
- Tạo QrToken duy nhất khi Admin tạo/xem POI
- Tra cứu POI theo token (GET /api/qr/{token})
- Hiển thị QR link trong Admin UI

**Nhóm 10 — Đánh giá POI**
- Gửi/cập nhật rating 1–5 sao theo DeviceId (POST /api/pois/{id}/ratings)
- Upsert: mỗi DeviceId chỉ có 1 rating per POI
- Xem điểm trung bình và số lượt đánh giá (GET /api/pois/{id}/ratings)

### 3.2 Use Case Diagram

Diagram dạng `flowchart LR` với 3 subgraph tác nhân. Mỗi tác nhân kết nối đến các use case tương ứng.

**Cấu trúc diagram:**
```
flowchart LR
  subgraph Admin
    A[Admin]
  end
  subgraph ShopOwner
    S[ShopOwner]
  end
  subgraph Visitor
    V[Visitor / Mobile App]
  end
  subgraph UseCases[Use Cases]
    UC1[Quản lý POI]
    UC2[Duyệt / Từ chối POI]
    UC3[Duyệt ShopOwner]
    UC4[Xem Analytics]
    UC5[Tạo QR Code]
    UC6[Đăng ký tài khoản]
    UC7[Tạo / Sửa / Xóa POI]
    UC8[Gửi POI duyệt]
    UC9[Dùng AI dịch thuật]
    UC10[Xem thống kê cá nhân]
    UC11[Đồng bộ POI]
    UC12[Nghe thuyết minh tự động]
    UC13[Đánh giá POI]
    UC14[Mua Access Pass]
    UC15[Quét QR Code]
  end
  A --> UC1 & UC2 & UC3 & UC4 & UC5
  S --> UC6 & UC7 & UC8 & UC9 & UC10
  V --> UC11 & UC12 & UC13 & UC14 & UC15
```

### 3.3 ERD — 8 Entity

Diagram dạng `erDiagram` với đầy đủ thuộc tính và quan hệ:

**Quan hệ chính:**
- `ApplicationUser` ||--o{ `Poi` : "sở hữu (OwnerId, nullable)"
- `ApplicationUser` ||--o{ `Payment` : "thanh toán"
- `Poi` ||--|{ `PoiLocalization` : "có bản dịch (CASCADE DELETE)"
- `Poi` ||--o{ `PoiRating` : "được đánh giá"
- `Poi` ||--o{ `FreeTrialRecord` : "được nghe thử"
- `Poi` ||--o{ `AnalyticsEvent` : "ghi nhận sự kiện"
- `DeviceTrial` — entity độc lập (PK = DeviceId)
- `FreeTrialRecord` — liên kết Poi + DeviceId/UserId

### 3.4 Class Diagram — Application/Domain Layer

Diagram dạng `classDiagram` thể hiện:
- Interface `IPoiRepository` với các method: GetSyncPoisAsync, ApprovePoiAsync
- Interface `IAnalyticsRepository` với method: AddVisitEventAsync
- Class `AdminApproveUseCase` sử dụng `IPoiRepository`
- Class `AnalyticsVisitUseCase` sử dụng `IAnalyticsRepository`
- Class `PoiSyncUseCase` sử dụng `IPoiRepository`
- DTO classes: `SyncRequest`, `SyncResponse`, `AnalyticsVisitCommand`

### 3.5 Sequence Diagrams — 8 luồng

| # | Tên luồng | Participants |
|---|-----------|-------------|
| 1 | Đăng nhập & Đăng ký | ShopOwner, Browser, API, DB, Admin |
| 2 | Tạo & Duyệt POI | ShopOwner, API, DB, Admin |
| 3 | Đồng bộ Mobile | MobileApp, API, DB, SQLite |
| 4 | Thuyết minh tự động | GPS, GeofenceEngine, NarrationService, API |
| 5 | AI dịch thuật | User, API, GeminiAiService, GeminiAPI |
| 6 | Kiểm soát truy cập | MobileApp, API, DB |
| 7 | Quét QR | MobileApp, Camera, API, DB |
| 8 | Đánh giá POI | MobileApp, API, DB |

### 3.6 Activity Diagrams — 5 luồng

| # | Tên luồng | Loại diagram |
|---|-----------|-------------|
| 1 | Vòng đời POI | flowchart TD với 5 trạng thái |
| 2 | GeofenceEngine | flowchart TD với debounce/cooldown/priority |
| 3 | Kiểm soát truy cập Visitor | flowchart TD với 3 lớp kiểm tra |
| 4 | NarrationService | flowchart TD với MP3/TTS fallback |
| 5 | Đồng bộ dữ liệu Mobile | flowchart TD với success/failure path |

### 3.7 API Reference

Tổ chức thành 4 nhóm với bảng markdown:

| Nhóm | Base Path | Auth |
|------|-----------|------|
| Auth API | /api/auth | Không cần |
| Admin API | /api/admin | Bearer [Admin] |
| Shop API | /api/shop | Bearer [ShopOwner] |
| Mobile API | /api/pois, /api/analytics, /api/access, /api/qr, /api/payments | Tùy endpoint |

Ví dụ JSON được cung cấp cho: login, sync POI, analytics visit, access check.

---

## 4. Mô hình dữ liệu

### 4.1 Entity chi tiết

**ApplicationUser** (kế thừa IdentityUser)
- `Id` string PK
- `UserName` string
- `Email` string
- `FullName` string
- `PoiId` int? (FK → Poi, nullable)
- `IsApproved` bool
- `ActivationDate` DateTime

**Poi**
- `Id` int PK
- `BasePoiId` string (mã định danh base)
- `CategoryCode` string (FOOD_STREET/FOOD_SNAIL/FOOD_BBQ/DRINK/UTILITY)
- `Latitude` double
- `Longitude` double
- `Radius` double (mét, default 50)
- `ImageUrl` string?
- `QrToken` string? (max 128)
- `Priority` int
- `IsApproved` bool
- `Status` PoiStatus (Draft/Pending_Approval/Approved/Rejected/Hidden)
- `IsPremium` bool
- `OwnerId` string? (FK → ApplicationUser)
- `RejectionReason` string?
- `CreatedAt` DateTime
- `UpdatedAt` DateTime

**PoiLocalization**
- `Id` int PK
- `PoiId` int FK → Poi (CASCADE DELETE)
- `LanguageCode` string (vi/en/ja)
- `Name` string
- `Description` string
- `AudioUrl` string?

**PoiRating**
- `Id` int PK
- `PoiId` int FK → Poi (CASCADE DELETE)
- `DeviceId` string
- `Stars` int (1–5, CHECK constraint)
- `RatedAt` DateTime
- `Latitude` double?
- `Longitude` double?
- UNIQUE INDEX (DeviceId, PoiId)

**AnalyticsEvent**
- `Id` int PK
- `EventType` string? (visit/narration)
- `PoiId` int? FK → Poi (nullable)
- `DeviceId` string
- `Timestamp` DateTime
- `Latitude` double
- `Longitude` double

**Payment**
- `Id` int PK
- `TransactionId` string UNIQUE
- `UserId` string FK → ApplicationUser (CASCADE DELETE)
- `Amount` decimal
- `Type` PaymentType (AccessPass=0)
- `Status` PaymentStatus (Pending/Completed/Failed/Refunded)
- `ExpiryDate` DateTime? (null cho đến khi Completed)
- `CreatedAt` DateTime

**FreeTrialRecord**
- `Id` int PK
- `UserId` string? (nullable, UNIQUE với PoiId khi không null)
- `DeviceId` string? (nullable, UNIQUE với PoiId khi không null)
- `PoiId` int FK → Poi
- `FirstHeardAt` DateTime

**DeviceTrial**
- `DeviceId` string PK
- `TrialStartDate` DateTime
- `ExpiryDate` DateTime
- `LastCheckedAt` DateTime?

### 4.2 PoiStatus Enum

```
Draft → Pending_Approval → Approved
                        → Rejected
Approved → Hidden
```

### 4.3 Shared DTOs (VinhKhanh.Shared)

**SyncRequest**
- `LastSyncAt` DateTime
- `IncludeAudio` bool

**SyncResponse**
- `UpdatedPois` List<Poi>
- `DeletedIds` List<int>
- `ServerTime` DateTime

**PoiLocalizationDto**
- `LanguageCode` string
- `Name` string
- `Description` string
- `AudioFile` string? (null khi IncludeAudio=false)

---

## 5. Xử lý lỗi

### 5.1 Chiến lược xử lý lỗi trong README.md

README.md không cần section riêng về error handling vì đây là tài liệu mô tả. Tuy nhiên, API Reference sẽ ghi chú các HTTP status code cho từng endpoint:

| HTTP Status | Ý nghĩa |
|-------------|---------|
| 200 OK | Thành công |
| 400 Bad Request | Dữ liệu đầu vào không hợp lệ |
| 401 Unauthorized | Chưa xác thực |
| 403 Forbidden | Không có quyền (tài khoản chưa duyệt, sai role) |
| 404 Not Found | Không tìm thấy resource |
| 409 Conflict | Trùng lặp (email đã tồn tại, giao dịch đã tồn tại) |
| 500 Internal Server Error | Lỗi server |

### 5.2 Xử lý lỗi khi tạo README.md

Khi task tạo README.md được thực thi:
- Nếu file README.md đã tồn tại → ghi đè (overwrite)
- Nếu Mermaid syntax có ký tự đặc biệt → escape hoặc dùng dấu ngoặc kép cho label
- Nếu diagram quá phức tạp → chia thành nhiều diagram nhỏ hơn

### 5.3 Quy tắc Mermaid syntax an toàn

Để tránh lỗi render Mermaid:
1. Label có dấu ngoặc đơn `()` → dùng `["text (detail)"]` hoặc escape
2. Label có dấu `/` → dùng `["A / B"]`
3. Tên node không dùng ký tự đặc biệt — chỉ dùng chữ cái, số, gạch dưới
4. Trong `erDiagram`, tên attribute không có dấu cách
5. Trong `sequenceDiagram`, note text không có ký tự `:`

---

## 6. Chiến lược kiểm thử

### 6.1 Đánh giá khả năng áp dụng Property-Based Testing

Feature `project-documentation` tạo ra một file markdown tĩnh. Đây là tác vụ **tạo nội dung tài liệu**, không phải logic thuật toán hay transformation function. Cụ thể:

- Không có pure function với input/output có thể vary
- Không có thuật toán cần kiểm tra tính đúng đắn trên nhiều input
- Output là file markdown cố định, không phụ thuộc vào input ngẫu nhiên

**Kết luận: PBT không phù hợp cho feature này.** Sử dụng example-based tests và smoke tests thay thế.

### 6.2 Kiểm thử Example-Based

Sau khi README.md được tạo, kiểm tra các điều kiện sau:

**Kiểm tra sự tồn tại và cấu trúc:**
- File `README.md` tồn tại ở thư mục gốc
- File có mục lục (Table of Contents) với anchor links
- File sử dụng tiếng Việt làm ngôn ngữ chính

**Kiểm tra nội dung 10 nhóm chức năng:**
- Chứa từ khóa: "JWT", "ShopOwner", "Visitor", "Admin"
- Chứa từ khóa: "GeofenceEngine", "NarrationService", "Haversine"
- Chứa từ khóa: "Free Trial", "DeviceTrial", "Access Pass"
- Chứa từ khóa: "Gemini", "dịch thuật", "QrToken"
- Chứa từ khóa: "heatmap", "analytics", "content-performance"

**Kiểm tra Mermaid diagrams:**
- Có ít nhất 1 block `flowchart` (Use Case Diagram)
- Có ít nhất 1 block `erDiagram` (ERD)
- Có ít nhất 1 block `classDiagram` (Class Diagram)
- Có ít nhất 8 block `sequenceDiagram` (Sequence Diagrams)
- Có ít nhất 5 block `flowchart TD` (Activity Diagrams)

**Kiểm tra API Reference:**
- Chứa các endpoint: `/api/auth/login`, `/api/auth/register-shop`
- Chứa các endpoint: `/api/admin/pois`, `/api/admin/pois/{id}/approve`
- Chứa các endpoint: `/api/shop/pois`, `/api/shop/analytics`
- Chứa các endpoint: `/api/pois/updates`, `/api/analytics/visit`
- Chứa các endpoint: `/api/access/check`, `/api/qr/{token}`
- Chứa ví dụ JSON request/response

### 6.3 Smoke Tests

- File README.md có thể đọc được (không bị corrupt)
- Mermaid syntax không có lỗi cú pháp cơ bản (kiểm tra bằng Mermaid CLI nếu có)
- File encoding là UTF-8 (hỗ trợ tiếng Việt)

### 6.4 Công cụ kiểm thử đề xuất

Vì đây là dự án .NET, có thể dùng:
- **xUnit** cho example-based tests
- **System.IO.File.ReadAllText** để đọc và kiểm tra nội dung README.md
- **Regex** để kiểm tra sự tồn tại của các pattern (Mermaid blocks, endpoint paths)
- **Mermaid CLI** (`mmdc`) để validate syntax nếu cần
