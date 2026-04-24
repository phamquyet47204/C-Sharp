# VinhKhanh — Nền tảng Du lịch Ẩm thực Thông minh

![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-10.0-blue) ![.NET MAUI](https://img.shields.io/badge/.NET_MAUI-Android-green) ![React](https://img.shields.io/badge/React-Vite-61DAFB) ![EF Core](https://img.shields.io/badge/EF_Core-SQLite%2FPostgres-orange)

VinhKhanh là nền tảng du lịch ẩm thực thông minh (Food Street / POI — Point of Interest) cho phép du khách khám phá các điểm ăn uống đặc sắc thông qua thuyết minh tự động khi đến gần địa điểm. Hệ thống gồm ba thành phần chính: **Backend API** (ASP.NET Core), **Admin Web UI** (React + Vite), và **Mobile App** (.NET MAUI Android).

---

## Mục lục

1. [Tổng quan dự án](#1-tổng-quan-dự-án)
2. [Kiến trúc hệ thống](#2-kiến-trúc-hệ-thống)
3. [Chức năng hệ thống](#3-chức-năng-hệ-thống)
4. [Use Case Diagram](#4-use-case-diagram)
5. [Mô hình dữ liệu (ERD)](#5-mô-hình-dữ-liệu-erd)
6. [Class Diagram](#6-class-diagram)
7. [Sequence Diagrams](#7-sequence-diagrams)
8. [Activity Diagrams](#8-activity-diagrams)
9. [API Reference](#9-api-reference)

---

## 1. Tổng quan dự án

### 1.1 Mô tả

VinhKhanh là ứng dụng hướng dẫn du lịch ẩm thực tự động. Khi du khách đến gần một điểm ăn uống (POI) đã đăng ký, ứng dụng mobile tự động phát thuyết minh bằng giọng nói (MP3 hoặc TTS) giới thiệu về địa điểm đó. Nội dung hỗ trợ đa ngôn ngữ (Tiếng Việt, Tiếng Anh, Tiếng Nhật) nhờ tích hợp Google Gemini AI.

### 1.2 Thành phần hệ thống

| Thành phần | Công nghệ | Mô tả |
|-----------|-----------|-------|
| **VinhKhanh.Admin** | ASP.NET Core 8 Web API | Backend API chính, xử lý toàn bộ nghiệp vụ |
| **VinhKhanh.Admin.Ui** | React + Vite + TailwindCSS | Giao diện quản trị cho Admin và ShopOwner |
| **VinhKhanh.Mobile** | .NET MAUI Android | Ứng dụng mobile cho Visitor |
| **VinhKhanh.Application** | C# Class Library | Use Cases (Clean Architecture) |
| **VinhKhanh.Domain** | C# Class Library | Entities + Interfaces |
| **VinhKhanh.Infrastructure** | EF Core + SQLite/PostgreSQL | Repository, GeminiAiService, EncryptionUtility |

### 1.3 Thuật ngữ

| Thuật ngữ | Ý nghĩa |
|-----------|---------|
| **POI** | Point of Interest — điểm tham quan / quán ăn đăng ký trên hệ thống |
| **PoiStatus** | Trạng thái vòng đời POI: Draft → Pending_Approval → Approved / Rejected / Hidden |
| **ShopOwner** | Chủ quán — role cần Admin duyệt trước khi sử dụng |
| **Admin** | Quản trị viên — toàn quyền quản lý POI, người dùng, analytics |
| **Visitor** | Du khách — dùng thử 3 POI miễn phí hoặc mua Access Pass |
| **DeviceTrial** | Bản ghi thử nghiệm 7 ngày gắn với DeviceId |
| **FreeTrialRecord** | Bản ghi lần đầu nghe thuyết minh một POI (giới hạn 3 POI miễn phí) |
| **AccessPass** | Gói trả phí 7 ngày cho phép nghe không giới hạn |
| **GeofenceEngine** | Dịch vụ mobile phát hiện khi người dùng vào vùng bán kính POI |
| **NarrationService** | Dịch vụ mobile phát thuyết minh (MP3 hoặc TTS) |
| **GeminiAiService** | Dịch vụ backend gọi Google Gemini API dịch vi → en, ja |
| **QrToken** | Mã token duy nhất gắn với POI để tra cứu qua QR code |
| **AnalyticsEvent** | Sự kiện ghi nhận lượt xem (visit) hoặc lượt nghe thuyết minh (narration) |

---

## 2. Kiến trúc hệ thống

### 2.1 Clean Architecture

Dự án theo mô hình **Clean Architecture** với 4 tầng tách biệt:

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

### 2.2 Luồng dữ liệu tổng quát

```
Mobile App ──sync──► Backend API ──query──► Database (EF Core)
                          │
                     GeminiAiService ──► Google Gemini API
                          │
Admin Web UI ──manage──► Backend API
```

---
## 3. Chức năng hệ thống

### 3.1 Xác thực & Phân quyền

| Chức năng | Endpoint | Mô tả |
|-----------|----------|-------|
| Đăng nhập JWT | `POST /api/auth/login` | Xác thực email/password, trả JWT token 24h |
| Đăng ký ShopOwner | `POST /api/auth/register-shop` | Tạo tài khoản chủ quán, chờ Admin duyệt |
| Đăng ký Visitor | `POST /api/auth/register-visitor` | Tạo tài khoản du khách, tự động kích hoạt |
| Phân quyền 3 role | — | Admin / ShopOwner / Visitor với quyền hạn khác nhau |

### 3.2 Quản lý POI (Admin)

| Chức năng | Endpoint | Mô tả |
|-----------|----------|-------|
| Xem danh sách POI | `GET /api/admin/pois` | Lấy tất cả POI kèm thông tin chủ quán |
| Xem POI chờ duyệt | `GET /api/admin/pois/pending` | Lọc POI có status Pending_Approval |
| Xem chi tiết POI | `GET /api/admin/pois/{id}` | Chi tiết POI kèm QR link |
| Tạo mới POI | `POST /api/admin/pois` | Tạo POI với upload ảnh, tự động Approved |
| Cập nhật POI | `PUT /api/admin/pois/{id}` | Cập nhật thông tin và ảnh POI |
| Duyệt POI | `POST /api/admin/pois/{id}/approve` | Chuyển status → Approved |
| Từ chối POI | `POST /api/admin/pois/{id}/reject` | Chuyển status → Rejected kèm lý do (≥10 ký tự) |
| Ẩn POI | `POST /api/admin/pois/{id}/hide` | Chuyển status → Hidden |
| Dashboard summary | `GET /api/admin/dashboard-summary` | Tổng số POI, lượt visit, narration, activity series |

### 3.3 Cổng chủ quán (ShopOwner)

| Chức năng | Endpoint | Mô tả |
|-----------|----------|-------|
| Xem danh sách POI của mình | `GET /api/shop/pois` | Chỉ POI thuộc OwnerId hiện tại |
| Xem chi tiết POI | `GET /api/shop/pois/{id}` | Chi tiết POI của mình |
| Tạo POI mới | `POST /api/shop/pois` | Tạo POI ở trạng thái Draft |
| Cập nhật POI | `PUT /api/shop/pois/{id}` | Chỉ được sửa khi status là Draft/Rejected |
| Xóa POI | `DELETE /api/shop/pois/{id}` | Không xóa được khi đang Pending_Approval |
| Gửi duyệt | `POST /api/shop/pois/{id}/submit` | Chuyển Draft → Pending_Approval |
| AI dịch thuật | `POST /api/shop/ai/generate` | Dịch vi → en, ja qua Gemini API |
| Xem thống kê | `GET /api/shop/analytics` | Thống kê visit/narration 30 ngày gần nhất |

### 3.4 Đồng bộ Mobile

| Chức năng | Endpoint | Mô tả |
|-----------|----------|-------|
| Sync POI theo timestamp | `GET /api/pois/updates?lastSync=...` | Trả danh sách POI đã cập nhật sau lastSync |
| Hỗ trợ audio mode | `includeAudio=true/false` | Bao gồm hoặc bỏ qua AudioUrl trong response |
| SyncResponse | — | Trả UpdatedPois, DeletedIds, ServerTime |

### 3.5 Thuyết minh tự động

- **GeofenceEngine**: Tính khoảng cách Haversine giữa GPS hiện tại và tọa độ POI. Yêu cầu debounce ≥2 lần liên tiếp trong vùng bán kính trước khi kích hoạt. Áp dụng cooldown để tránh phát lại ngay. Ưu tiên POI theo trường `Priority`.
- **NarrationService**: Phát file MP3 từ `AudioUrl` nếu có; fallback sang TextToSpeech (TTS) nếu không có audio. Hỗ trợ audio ducking trên Android (giảm âm lượng nhạc nền khi phát thuyết minh).
- **Ghi AnalyticsEvent**: Sau mỗi lần phát thuyết minh, ghi sự kiện `narration` kèm tọa độ GPS và PoiId.

### 3.6 Kiểm soát truy cập

| Chức năng | Endpoint | Mô tả |
|-----------|----------|-------|
| Kiểm tra trạng thái | `GET /api/access/check` | Trả freeTrialUsed, hasActivePass, passExpiryDate |
| Bắt đầu DeviceTrial | `POST /api/access/start-trial?deviceId=...` | Kích hoạt 7 ngày dùng thử cho thiết bị |
| Free Trial | — | 3 POI đầu tiên miễn phí (FreeTrialRecord) |
| Access Pass | `POST /api/payments/initiate` + `POST /api/payments/callback` | Mua gói 7 ngày |

### 3.7 Analytics

| Chức năng | Endpoint | Mô tả |
|-----------|----------|-------|
| Ghi nhận sự kiện | `POST /api/analytics/visit` | Ghi visit hoặc narration kèm tọa độ GPS |
| Heatmap | `GET /api/analytics/heatmap` | Điểm nhiệt theo tọa độ, hỗ trợ lọc theo ngày |
| Content performance | `GET /api/analytics/content-performance` | Top POI theo lượt nghe, hỗ trợ lọc theo ngày |
| Dashboard summary | `GET /api/admin/dashboard-summary` | Tổng quan số liệu cho Admin |

### 3.8 AI dịch thuật

- Gọi **Google Gemini API** để dịch tên và mô tả POI từ Tiếng Việt sang Tiếng Anh và Tiếng Nhật.
- **Admin**: `POST /api/admin/ai/generate` (role Admin hoặc ShopOwner)
- **ShopOwner**: `POST /api/shop/ai/generate`
- Kết quả dịch tự động điền vào form tạo/sửa POI.

### 3.9 QR Code

| Chức năng | Endpoint | Mô tả |
|-----------|----------|-------|
| Tạo QrToken | Tự động khi Admin tạo/xem POI | Token duy nhất dạng `poi-{guid}` (20 ký tự) |
| Tra cứu POI qua QR | `GET /api/qr/{token}` | Trả thông tin POI kèm localizations |
| Hiển thị QR link | Admin UI | Link dạng `{host}/api/qr/{token}` |

### 3.10 Đánh giá POI

| Chức năng | Endpoint | Mô tả |
|-----------|----------|-------|
| Gửi / cập nhật rating | `POST /api/pois/{id}/ratings` | Upsert rating 1–5 sao theo DeviceId |
| Xem điểm trung bình | `GET /api/pois/{id}/ratings` | Trả averageStars, ratingCount, userStars |

> Mỗi DeviceId chỉ có 1 rating per POI (upsert). Rating kèm tọa độ GPS tùy chọn.

---
## 4. Use Case Diagram

```mermaid
flowchart LR
  Admin["👤 Admin"]
  Shop["👤 ShopOwner"]
  Visitor["👤 Visitor / Mobile App"]

  subgraph System["Hệ thống VinhKhanh"]
    UC1["Quản lý POI"]
    UC2["Duyệt / Từ chối POI"]
    UC3["Duyệt ShopOwner"]
    UC4["Xem Analytics"]
    UC5["Tạo QR Code"]
    UC6["AI dịch thuật"]
    UC7["Đăng ký tài khoản"]
    UC8["Tạo / Sửa / Xóa POI"]
    UC9["Gửi POI duyệt"]
    UC10["Xem thống kê cá nhân"]
    UC11["Đồng bộ POI"]
    UC12["Nghe thuyết minh tự động"]
    UC13["Đánh giá POI"]
    UC14["Mua Access Pass"]
    UC15["Quét QR Code"]
    UC16["Bắt đầu dùng thử"]
  end

  Admin --> UC1
  Admin --> UC2
  Admin --> UC3
  Admin --> UC4
  Admin --> UC5
  Admin --> UC6

  Shop --> UC7
  Shop --> UC8
  Shop --> UC9
  Shop --> UC6
  Shop --> UC10

  Visitor --> UC11
  Visitor --> UC12
  Visitor --> UC13
  Visitor --> UC14
  Visitor --> UC15
  Visitor --> UC16
```

---
## 5. Mô hình dữ liệu (ERD)

```mermaid
erDiagram
  ApplicationUser {
    string Id PK
    string UserName
    string Email
    string FullName
    int PoiId FK
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
    string ImageUrl
    string QrToken
    int Priority
    bool IsApproved
    string Status
    bool IsPremium
    string OwnerId FK
    string RejectionReason
    datetime CreatedAt
    datetime UpdatedAt
  }

  PoiLocalization {
    int Id PK
    int PoiId FK
    string LanguageCode
    string Name
    string Description
    string AudioUrl
  }

  PoiRating {
    int Id PK
    int PoiId FK
    string DeviceId
    int Stars
    datetime RatedAt
    double Latitude
    double Longitude
  }

  AnalyticsEvent {
    int Id PK
    string EventType
    int PoiId FK
    string DeviceId
    datetime Timestamp
    double Latitude
    double Longitude
  }

  Payment {
    int Id PK
    string TransactionId
    string UserId FK
    decimal Amount
    string Type
    string Status
    datetime ExpiryDate
    datetime CreatedAt
  }

  FreeTrialRecord {
    int Id PK
    string UserId
    string DeviceId
    int PoiId FK
    datetime FirstHeardAt
  }

  DeviceTrial {
    string DeviceId PK
    datetime TrialStartDate
    datetime ExpiryDate
    datetime LastCheckedAt
  }

  ApplicationUser ||--o{ Poi : "sở hữu (OwnerId)"
  ApplicationUser ||--o{ Payment : "thanh toán"
  Poi ||--|{ PoiLocalization : "có bản dịch (CASCADE)"
  Poi ||--o{ PoiRating : "được đánh giá"
  Poi ||--o{ FreeTrialRecord : "được nghe thử"
  Poi ||--o{ AnalyticsEvent : "ghi nhận sự kiện"
```

---
## 6. Class Diagram

```mermaid
classDiagram
  class IPoiRepository {
    <<interface>>
    +GetSyncPoisAsync(lastSyncAt, ct) Task~List~Poi~~
    +ApprovePoiAsync(poiId, ct) Task~bool~
  }

  class IAnalyticsRepository {
    <<interface>>
    +AddVisitEventAsync(event, ct) Task
  }

  class AdminApproveUseCase {
    -IPoiRepository _repository
    +AdminApproveUseCase(IPoiRepository)
    +ExecuteAsync(poiId, ct) Task~bool~
  }

  class AnalyticsVisitUseCase {
    -IAnalyticsRepository _repository
    +AnalyticsVisitUseCase(IAnalyticsRepository)
    +ExecuteAsync(command, ct) Task
  }

  class PoiSyncUseCase {
    -IPoiRepository _repository
    +PoiSyncUseCase(IPoiRepository)
    +ExecuteAsync(request, ct) Task~SyncResponse~
  }

  class AnalyticsVisitCommand {
    +double Latitude
    +double Longitude
    +string DeviceId
    +int PoiId
    +string EventType
  }

  class SyncRequest {
    +DateTime LastSyncAt
    +bool IncludeAudio
  }

  class SyncResponse {
    +List~Poi~ UpdatedPois
    +List~int~ DeletedIds
    +DateTime ServerTime
  }

  class PoiLocalizationDto {
    +string LanguageCode
    +string Name
    +string Description
    +string AudioFile
  }

  AdminApproveUseCase --> IPoiRepository : uses
  PoiSyncUseCase --> IPoiRepository : uses
  AnalyticsVisitUseCase --> IAnalyticsRepository : uses
  PoiSyncUseCase ..> SyncRequest : input
  PoiSyncUseCase ..> SyncResponse : output
  SyncResponse *-- PoiLocalizationDto : contains
  AnalyticsVisitUseCase ..> AnalyticsVisitCommand : input
```

---
## 7. Sequence Diagrams

### 7.1 Đăng nhập & Đăng ký

```mermaid
sequenceDiagram
  actor ShopOwner
  participant Browser
  participant API as Backend API
  participant DB as Database
  actor Admin

  ShopOwner->>Browser: Điền form đăng ký
  Browser->>API: POST /api/auth/register-shop
  API->>DB: Tạo ApplicationUser (IsApproved=false, role=ShopOwner)
  DB-->>API: OK
  API-->>Browser: 200 - Chờ Admin duyệt

  Admin->>API: POST /api/admin/approve-owner/{userId}
  API->>DB: Cập nhật IsApproved=true
  DB-->>API: OK
  API-->>Admin: 200 - Đã duyệt

  ShopOwner->>Browser: Đăng nhập
  Browser->>API: POST /api/auth/login
  API->>DB: Tìm user theo email, kiểm tra password
  DB-->>API: ApplicationUser
  API->>API: Kiểm tra IsApproved
  API->>API: Tạo JWT token (24h, claims: role, userId)
  API-->>Browser: 200 - token, expiration, roles
```

### 7.2 Tạo & Duyệt POI

```mermaid
sequenceDiagram
  actor ShopOwner
  participant API as Backend API
  participant DB as Database
  actor Admin

  ShopOwner->>API: POST /api/shop/pois (form-data: name, desc, lat, lng, image)
  API->>API: Kiểm tra IsApproved của ShopOwner
  API->>DB: Tạo Poi (Status=Draft, OwnerId=userId)
  DB-->>API: poiId
  API->>DB: Upload ảnh, lưu ImageUrl
  API->>DB: Tạo PoiLocalization (vi, en, ja)
  DB-->>API: OK
  API-->>ShopOwner: 200 - poiId

  ShopOwner->>API: POST /api/shop/pois/{id}/submit
  API->>DB: Cập nhật Status=Pending_Approval
  DB-->>API: OK
  API-->>ShopOwner: 200 - success

  Admin->>API: GET /api/admin/pois/pending
  API->>DB: Lấy danh sách POI Pending_Approval
  DB-->>API: List POI
  API-->>Admin: Danh sách chờ duyệt

  alt Duyệt POI
    Admin->>API: POST /api/admin/pois/{id}/approve
    API->>DB: Status=Approved, IsApproved=true
    DB-->>API: OK
    API-->>Admin: 200 - Đã duyệt thành công
  else Từ chối POI
    Admin->>API: POST /api/admin/pois/{id}/reject (reason)
    API->>DB: Status=Rejected, RejectionReason=reason
    DB-->>API: OK
    API-->>Admin: 200 - Đã từ chối
  end
```

### 7.3 Đồng bộ Mobile

```mermaid
sequenceDiagram
  participant Mobile as Mobile App
  participant API as Backend API
  participant DB as Database
  participant SQLite as SQLite Local

  Mobile->>API: GET /api/pois/updates?lastSync=2026-01-01T00:00:00Z&includeAudio=true
  API->>DB: SELECT Poi WHERE UpdatedAt > lastSync AND Status=Approved
  DB-->>API: List Poi + Localizations
  API->>API: Map entities sang SyncResponse DTO
  API->>API: NormalizeCategoryCode cho từng POI
  API-->>Mobile: SyncResponse (updatedPois, deletedIds, serverTime)

  Mobile->>SQLite: Upsert từng POI vào local DB
  SQLite-->>Mobile: OK
  Mobile->>Mobile: Cập nhật lastSyncAt = serverTime
  Mobile->>Mobile: Khởi động GeofenceEngine với danh sách POI mới
```

### 7.4 Thuyết minh tự động

```mermaid
sequenceDiagram
  participant GPS as GPS Service
  participant GE as GeofenceEngine
  participant NS as NarrationService
  participant API as Backend API
  participant DB as Database

  loop Mỗi cập nhật GPS
    GPS->>GE: OnLocationChanged(lat, lng)
    GE->>GE: Tính Haversine distance đến từng POI
    GE->>GE: Kiểm tra distance <= Radius
    alt Trong vùng POI
      GE->>GE: Tăng debounce counter
      alt debounce >= 2 lần
        GE->>GE: Kiểm tra cooldown (tránh phát lại ngay)
        alt Hết cooldown
          GE->>GE: Chọn POI ưu tiên cao nhất (Priority)
          GE->>NS: OnPoiEntered(poi)
          NS->>NS: Kiểm tra AudioUrl
          alt Có AudioUrl (MP3)
            NS->>NS: PlayAudioAsync(audioUrl)
          else Không có audio
            NS->>NS: TextToSpeech(description)
          end
          NS->>NS: Audio ducking Android
          NS->>API: POST /api/analytics/visit (eventType=narration, poiId, lat, lng)
          API->>DB: Lưu AnalyticsEvent
          API->>DB: Upsert FreeTrialRecord nếu lần đầu nghe POI này
          DB-->>API: OK
          API-->>NS: 200 - success
        end
      end
    else Ra khỏi vùng POI
      GE->>GE: Reset debounce counter
    end
  end
```

### 7.5 AI dịch thuật

```mermaid
sequenceDiagram
  actor User as Admin / ShopOwner
  participant UI as Admin Web UI
  participant API as Backend API
  participant Gemini as GeminiAiService
  participant GeminiAPI as Google Gemini API

  User->>UI: Nhập tên và mô tả tiếng Việt
  User->>UI: Nhấn "Dịch tự động"
  UI->>API: POST /api/admin/ai/generate (name, description)
  API->>Gemini: GenerateTranslationsAsync(name, description)
  Gemini->>GeminiAPI: Gửi prompt dịch vi -> en, ja
  GeminiAPI-->>Gemini: JSON response (en, ja translations)
  Gemini-->>API: TranslationResult
  API-->>UI: 200 - nameEn, descEn, nameJa, descJa
  UI->>UI: Tự điền các field en/ja trong form
  UI-->>User: Form đã được điền đầy đủ
```

### 7.6 Kiểm soát truy cập

```mermaid
sequenceDiagram
  participant Mobile as Mobile App
  participant API as Backend API
  participant DB as Database

  Mobile->>API: GET /api/access/check?deviceId=ABC123
  API->>DB: Đếm FreeTrialRecord theo deviceId (distinct PoiId)
  DB-->>API: freeTrialUsed = N

  API->>DB: Tìm DeviceTrial theo deviceId
  DB-->>API: DeviceTrial (ExpiryDate)
  API->>API: isTrialActive = ExpiryDate > now

  alt User đã đăng nhập (có JWT)
    API->>DB: Tìm Payment (userId, Status=Completed, ExpiryDate > now)
    DB-->>API: Payment hoặc null
    API->>API: hasActivePass = payment != null
  end

  API-->>Mobile: 200 - freeTrialUsed, freeTrialLimit=3, hasActivePass, passExpiryDate, isTrial, trialRemainingDays

  alt Cần mua Access Pass
    Mobile->>API: POST /api/payments/initiate (transactionId)
    API->>DB: Tạo Payment (Status=Pending)
    DB-->>API: paymentId
    API-->>Mobile: 200 - paymentId

    Mobile->>API: POST /api/payments/callback (transactionId)
    API->>DB: Cập nhật Status=Completed, ExpiryDate=+7 ngày
    DB-->>API: OK
    API-->>Mobile: 200 - expiryDate
  end
```

### 7.7 Quét QR

```mermaid
sequenceDiagram
  participant Mobile as Mobile App
  participant Camera as Camera / QR Scanner
  participant API as Backend API
  participant DB as Database

  Mobile->>Camera: Mở camera quét QR
  Camera->>Camera: Nhận dạng QR code
  Camera-->>Mobile: QR token string

  Mobile->>API: GET /api/qr/{token}
  API->>DB: SELECT Poi WHERE QrToken=token AND Status=Approved
  alt POI tìm thấy
    DB-->>API: Poi + Localizations
    API-->>Mobile: 200 - poiId, lat, lng, radius, imageUrl, localizations
    Mobile->>Mobile: Hiển thị thông tin POI
    Mobile->>Mobile: Tùy chọn phát thuyết minh ngay
  else Không tìm thấy
    DB-->>API: null
    API-->>Mobile: 404 - POI not found for this QR token
    Mobile->>Mobile: Hiển thị thông báo lỗi
  end
```

### 7.8 Đánh giá POI

```mermaid
sequenceDiagram
  participant Mobile as Mobile App
  participant API as Backend API
  participant DB as Database

  Mobile->>API: GET /api/pois/{id}/ratings?deviceId=ABC123
  API->>DB: Kiểm tra POI tồn tại và Status=Approved
  API->>DB: Đếm ratings, tính averageStars
  API->>DB: Tìm rating của deviceId này
  DB-->>API: count, average, userStars
  API-->>Mobile: 200 - averageStars, ratingCount, userStars

  Mobile->>Mobile: Hiển thị sao đánh giá
  Mobile->>API: POST /api/pois/{id}/ratings (stars, deviceId, lat, lng)
  API->>API: Validate stars (1-5), deviceId không rỗng
  API->>DB: Tìm PoiRating theo poiId + deviceId
  alt Chưa có rating
    API->>DB: INSERT PoiRating mới
  else Đã có rating
    API->>DB: UPDATE stars, ratedAt, lat, lng
  end
  API->>DB: Tính lại averageStars
  DB-->>API: count, average
  API-->>Mobile: 200 - success, userStars, averageStars, ratingCount
```

---
## 8. Activity Diagrams

### 8.1 Vòng đời POI

```mermaid
flowchart TD
  Start([Bắt đầu]) --> Draft[Trạng thái: Draft]
  Draft --> Edit{ShopOwner chỉnh sửa?}
  Edit -->|Có| Draft
  Edit -->|Gửi duyệt| Pending[Trạng thái: Pending_Approval]
  Pending --> AdminReview{Admin xem xét}
  AdminReview -->|Duyệt| Approved[Trạng thái: Approved]
  AdminReview -->|Từ chối kèm lý do| Rejected[Trạng thái: Rejected]
  Rejected --> CanEdit{ShopOwner sửa lại?}
  CanEdit -->|Có| Draft
  CanEdit -->|Không| End1([Kết thúc])
  Approved --> HideCheck{Admin ẩn POI?}
  HideCheck -->|Có| Hidden[Trạng thái: Hidden]
  HideCheck -->|Không| Active[POI hiển thị trên Mobile]
  Hidden --> End2([Kết thúc])
  Active --> End3([POI hoạt động])
```

### 8.2 GeofenceEngine

```mermaid
flowchart TD
  Start([GPS cập nhật vị trí]) --> CalcDist[Tính Haversine distance đến từng POI]
  CalcDist --> CheckRadius{distance <= Radius?}
  CheckRadius -->|Không| ResetDebounce[Reset debounce counter]
  ResetDebounce --> End1([Chờ GPS tiếp theo])
  CheckRadius -->|Có| IncDebounce[Tăng debounce counter]
  IncDebounce --> CheckDebounce{debounce >= 2 lần?}
  CheckDebounce -->|Không| End2([Chờ GPS tiếp theo])
  CheckDebounce -->|Có| CheckCooldown{Còn trong cooldown?}
  CheckCooldown -->|Có| End3([Bỏ qua - tránh phát lại])
  CheckCooldown -->|Không| SelectPOI[Chọn POI Priority cao nhất]
  SelectPOI --> FireEvent[Kích hoạt OnPoiEntered]
  FireEvent --> SetCooldown[Đặt cooldown timer]
  SetCooldown --> End4([NarrationService xử lý])
```

### 8.3 Kiểm soát truy cập Visitor

```mermaid
flowchart TD
  Start([Visitor muốn nghe thuyết minh]) --> CheckPass{Có Access Pass còn hạn?}
  CheckPass -->|Có| Allow1[Cho phép nghe]
  Allow1 --> End1([Phát thuyết minh])
  CheckPass -->|Không| CheckTrial{Có DeviceTrial còn hạn?}
  CheckTrial -->|Có| Allow2[Cho phép nghe - Trial]
  Allow2 --> End2([Phát thuyết minh])
  CheckTrial -->|Không| CheckFree{FreeTrialRecord < 3 POI?}
  CheckFree -->|Có| Allow3[Cho phép nghe - Free Trial]
  Allow3 --> SaveRecord[Lưu FreeTrialRecord]
  SaveRecord --> End3([Phát thuyết minh])
  CheckFree -->|Không - đã dùng hết 3 POI| ShowPaywall[Hiển thị màn hình mua Access Pass]
  ShowPaywall --> UserDecide{Người dùng quyết định}
  UserDecide -->|Mua Pass| InitPayment[POST /api/payments/initiate]
  InitPayment --> Callback[POST /api/payments/callback]
  Callback --> Allow1
  UserDecide -->|Bỏ qua| End4([Không phát thuyết minh])
```

### 8.4 NarrationService

```mermaid
flowchart TD
  Start([Nhận POI từ GeofenceEngine]) --> CheckAccess{Kiểm tra quyền truy cập}
  CheckAccess -->|Không có quyền| ShowPaywall[Hiển thị Paywall]
  ShowPaywall --> End1([Kết thúc])
  CheckAccess -->|Có quyền| GetLang[Lấy ngôn ngữ thiết bị]
  GetLang --> GetLocalization[Lấy PoiLocalization theo ngôn ngữ]
  GetLocalization --> CheckAudio{Có AudioUrl?}
  CheckAudio -->|Có MP3| PlayMP3[PlayAudioAsync - phát file MP3]
  CheckAudio -->|Không có| UseTTS[TextToSpeech - đọc Description]
  PlayMP3 --> AudioDucking[Bật Audio Ducking Android]
  UseTTS --> AudioDucking
  AudioDucking --> WaitFinish[Chờ phát xong]
  WaitFinish --> StopDucking[Tắt Audio Ducking]
  StopDucking --> LogAnalytics[POST /api/analytics/visit - eventType=narration]
  LogAnalytics --> End2([Hoàn thành])
```

### 8.5 Đồng bộ dữ liệu Mobile

```mermaid
flowchart TD
  Start([Khởi động ứng dụng]) --> ReadLocal[Đọc lastSyncAt từ SQLite]
  ReadLocal --> CallAPI[GET /api/pois/updates?lastSync=lastSyncAt]
  CallAPI --> CheckResponse{API trả về thành công?}
  CheckResponse -->|Thất bại - lỗi mạng| UseCache[Dùng dữ liệu cache SQLite cũ]
  UseCache --> StartGeo[Khởi động GeofenceEngine với dữ liệu cũ]
  StartGeo --> End1([Ứng dụng hoạt động - chế độ offline])
  CheckResponse -->|Thành công| ParseResponse[Parse SyncResponse]
  ParseResponse --> UpsertPOIs[Upsert từng POI vào SQLite]
  UpsertPOIs --> DeletePOIs[Xóa POI trong DeletedIds]
  DeletePOIs --> UpdateSync[Cập nhật lastSyncAt = serverTime]
  UpdateSync --> StartGeo2[Khởi động GeofenceEngine với dữ liệu mới]
  StartGeo2 --> End2([Ứng dụng hoạt động - dữ liệu mới nhất])
```

---
## 9. API Reference

### 9.1 Auth API

Base path: `/api/auth` | Không yêu cầu xác thực

| Method | Endpoint | Mô tả |
|--------|----------|-------|
| POST | `/api/auth/login` | Đăng nhập, nhận JWT token |
| POST | `/api/auth/register-shop` | Đăng ký tài khoản ShopOwner (chờ duyệt) |
| POST | `/api/auth/register-visitor` | Đăng ký tài khoản Visitor (tự động kích hoạt) |

**Ví dụ: POST /api/auth/login**

Request:
```json
{
  "email": "owner@example.com",
  "password": "Password123!"
}
```

Response `200 OK`:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiration": "2026-04-18T10:00:00Z",
  "roles": ["ShopOwner"]
}
```

Response `403 Forbidden` (chưa được duyệt):
```json
"Tài khoản của bạn đang chờ Admin duyệt."
```

---

### 9.2 Admin API

Base path: `/api/admin` | Yêu cầu: `Bearer Token [role: Admin]`

| Method | Endpoint | Mô tả |
|--------|----------|-------|
| GET | `/api/admin/pois` | Lấy tất cả POI kèm thông tin chủ quán |
| GET | `/api/admin/pois/pending` | Lấy danh sách POI chờ duyệt |
| GET | `/api/admin/pois/{id}` | Chi tiết POI kèm QR link |
| POST | `/api/admin/pois` | Tạo POI mới (multipart/form-data) |
| PUT | `/api/admin/pois/{id}` | Cập nhật POI (multipart/form-data) |
| POST | `/api/admin/pois/{id}/approve` | Duyệt POI |
| POST | `/api/admin/pois/{id}/reject` | Từ chối POI kèm lý do |
| POST | `/api/admin/pois/{id}/hide` | Ẩn POI |
| GET | `/api/admin/dashboard-summary` | Tổng quan số liệu dashboard |
| POST | `/api/admin/ai/generate` | Dịch nội dung vi → en, ja qua Gemini |

**Ví dụ: POST /api/admin/pois/{id}/reject**

Request:
```json
{
  "reason": "Thông tin địa chỉ không chính xác, vui lòng cập nhật lại tọa độ."
}
```

Response `200 OK`:
```json
{
  "success": true
}
```

---

### 9.3 Shop API

Base path: `/api/shop` | Yêu cầu: `Bearer Token [role: ShopOwner, IsApproved=true]`

| Method | Endpoint | Mô tả |
|--------|----------|-------|
| GET | `/api/shop/pois` | Danh sách POI của ShopOwner hiện tại |
| GET | `/api/shop/pois/{id}` | Chi tiết POI của mình |
| POST | `/api/shop/pois` | Tạo POI mới (Status=Draft) |
| PUT | `/api/shop/pois/{id}` | Cập nhật POI (chỉ Draft/Rejected) |
| DELETE | `/api/shop/pois/{id}` | Xóa POI (không xóa khi Pending) |
| POST | `/api/shop/pois/{id}/submit` | Gửi POI để Admin duyệt |
| POST | `/api/shop/ai/generate` | Dịch nội dung vi → en, ja |
| GET | `/api/shop/analytics` | Thống kê visit/narration 30 ngày |

---
### 9.4 Mobile API

#### POI Sync

| Method | Endpoint | Auth | Mô tả |
|--------|----------|------|-------|
| GET | `/api/pois/updates` | Không | Sync POI theo timestamp |
| GET | `/api/pois/sync` | Không | Alias của /updates |

**Query params:** `lastSync` (DateTime ISO 8601), `includeAudio` (bool, default=true)

**Ví dụ: GET /api/pois/updates?lastSync=2026-01-01T00:00:00Z&includeAudio=true**

Response `200 OK`:
```json
{
  "updatedPois": [
    {
      "id": 1,
      "basePoiId": "abc123def4",
      "latitude": 10.7769,
      "longitude": 106.7009,
      "radius": 50,
      "imageUrl": "/media/img_abc123.jpg",
      "priority": 1,
      "isActive": true,
      "isPremium": false,
      "categoryCode": "FOOD_STREET",
      "updatedAt": "2026-04-17T10:00:00Z",
      "localizations": [
        {
          "languageCode": "vi",
          "name": "Quán Ốc Bà Năm",
          "description": "Quán ốc nổi tiếng với hơn 20 năm kinh nghiệm",
          "audioFile": "/media/audio_vi_abc123.mp3"
        },
        {
          "languageCode": "en",
          "name": "Ba Nam Snail Restaurant",
          "description": "Famous snail restaurant with over 20 years of experience",
          "audioFile": null
        }
      ]
    }
  ],
  "deletedIds": [],
  "serverTime": "2026-04-17T12:00:00Z"
}
```

#### Analytics

| Method | Endpoint | Auth | Mô tả |
|--------|----------|------|-------|
| POST | `/api/analytics/visit` | Không | Ghi nhận sự kiện visit/narration |
| GET | `/api/analytics/heatmap` | Admin | Dữ liệu heatmap theo tọa độ |
| GET | `/api/analytics/content-performance` | Admin | Top POI theo lượt nghe |

**Ví dụ: POST /api/analytics/visit**

Request:
```json
{
  "latitude": 10.7769,
  "longitude": 106.7009,
  "deviceId": "device-uuid-abc123",
  "poiId": 1,
  "eventType": "narration"
}
```

Response `200 OK`:
```json
{
  "success": true
}
```

#### Access Control

| Method | Endpoint | Auth | Mô tả |
|--------|----------|------|-------|
| GET | `/api/access/check` | Tùy chọn JWT | Kiểm tra trạng thái truy cập |
| POST | `/api/access/start-trial` | Không | Bắt đầu DeviceTrial 7 ngày |

**Ví dụ: GET /api/access/check?deviceId=device-uuid-abc123**

Response `200 OK`:
```json
{
  "freeTrialUsed": 2,
  "freeTrialLimit": 3,
  "hasActivePass": false,
  "passExpiryDate": null,
  "isTrial": false,
  "trialRemainingDays": 0
}
```

Response khi có DeviceTrial:
```json
{
  "freeTrialUsed": 5,
  "freeTrialLimit": 3,
  "hasActivePass": true,
  "passExpiryDate": "2026-04-24T10:00:00Z",
  "isTrial": true,
  "trialRemainingDays": 6
}
```

#### QR Code

| Method | Endpoint | Auth | Mô tả |
|--------|----------|------|-------|
| GET | `/api/qr/{token}` | Không | Tra cứu POI theo QrToken |

#### POI Ratings

| Method | Endpoint | Auth | Mô tả |
|--------|----------|------|-------|
| GET | `/api/pois/{id}/ratings` | Không | Xem điểm trung bình và rating của thiết bị |
| POST | `/api/pois/{id}/ratings` | Không | Gửi hoặc cập nhật rating |

#### Payments

| Method | Endpoint | Auth | Mô tả |
|--------|----------|------|-------|
| POST | `/api/payments/initiate` | JWT | Khởi tạo giao dịch Access Pass |
| POST | `/api/payments/callback` | JWT | Xác nhận thanh toán thành công |
| GET | `/api/payments/status` | JWT | Kiểm tra trạng thái Access Pass |

---

### 9.5 HTTP Status Codes

| Status | Ý nghĩa |
|--------|---------|
| 200 OK | Thành công |
| 400 Bad Request | Dữ liệu đầu vào không hợp lệ |
| 401 Unauthorized | Chưa xác thực (thiếu hoặc sai JWT) |
| 403 Forbidden | Không có quyền (tài khoản chưa duyệt, sai role) |
| 404 Not Found | Không tìm thấy resource |
| 409 Conflict | Trùng lặp (email đã tồn tại, giao dịch đã tồn tại) |
| 500 Internal Server Error | Lỗi server |

---

*Tài liệu được tạo tự động từ spec `project-documentation`. Cập nhật lần cuối: 2026.*
