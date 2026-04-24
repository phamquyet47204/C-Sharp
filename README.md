# VinhKhanh — Tài liệu PRD Hệ thống Du lịch Ẩm thực Thông minh

> **Phiên bản:** 1.0 | **Ngày:** 24/04/2026 | **Tác giả:** Kiro AI (từ source code thực tế)

---

## Mục lục

1. [Tổng quan dự án](#1-tổng-quan-dự-án)
2. [Kiến trúc hệ thống](#2-kiến-trúc-hệ-thống)
3. [Danh sách chức năng hệ thống](#3-danh-sách-chức-năng-hệ-thống)
4. [Use Case Diagram tổng quan](#4-use-case-diagram-tổng-quan)
5. [Class Diagram tổng quan](#5-class-diagram-tổng-quan)
6. [ERD tổng quan](#6-erd-tổng-quan)
7. [Sequence Diagrams theo chức năng](#7-sequence-diagrams-theo-chức-năng)
8. [Activity Diagrams theo chức năng](#8-activity-diagrams-theo-chức-năng)

---

## 1. Tổng quan dự án

**VinhKhanh** là nền tảng du lịch ẩm thực thông minh (Food Street / POI — Point of Interest) cho phép du khách khám phá các điểm ăn uống đặc sắc thông qua thuyết minh tự động khi đến gần địa điểm. Hệ thống gồm ba thành phần chính:

| Thành phần | Công nghệ | Mô tả |
|---|---|---|
| **VinhKhanh.Admin** | ASP.NET Core 10 Web API | Backend API chính, xử lý toàn bộ nghiệp vụ |
| **VinhKhanh.Admin.Ui** | React + Vite + TailwindCSS | Giao diện quản trị cho Admin và ShopOwner |
| **VinhKhanh.Mobile** | .NET MAUI Android | Ứng dụng mobile cho Visitor |
| **VinhKhanh.Application** | C# Class Library | Use Cases (Clean Architecture) |
| **VinhKhanh.Domain** | C# Class Library | Entities + Interfaces |
| **VinhKhanh.Infrastructure** | EF Core + SQLite/PostgreSQL | Repository, GeminiAiService, EncryptionUtility |
| **VinhKhanh.Shared** | C# Class Library | DTOs dùng chung (SyncRequest, SyncResponse, Haversine) |

### Thuật ngữ

| Thuật ngữ | Ý nghĩa |
|---|---|
| **POI** | Point of Interest — điểm tham quan / quán ăn đăng ký trên hệ thống |
| **PoiStatus** | Enum vòng đời POI: `Draft=0`, `Pending_Approval=1`, `Approved=2`, `Rejected=3`, `Hidden=4` |
| **ShopOwner** | Chủ quán — role cần Admin duyệt (`IsApproved=true`) trước khi sử dụng |
| **Admin** | Quản trị viên — toàn quyền quản lý POI, người dùng, analytics |
| **Visitor** | Du khách — dùng thử 3 POI miễn phí hoặc mua Access Pass |
| **DeviceTrial** | Bản ghi dùng thử 7 ngày gắn với `DeviceId` (PK) |
| **FreeTrialRecord** | Bản ghi lần đầu nghe thuyết minh một POI (giới hạn 3 POI miễn phí) |
| **AccessPass** | Gói trả phí 7 ngày (`PaymentType.AccessPass`) cho phép nghe không giới hạn |
| **GeofenceEngine** | Dịch vụ mobile phát hiện khi người dùng vào vùng bán kính POI (Haversine + Debounce + Cooldown) |
| **NarrationService** | Dịch vụ mobile phát thuyết minh (MP3 qua `MediaElement` hoặc TTS) |
| **GeminiAiService** | Dịch vụ backend gọi Google Gemini API dịch vi → en, ja |
| **QrToken** | Mã token duy nhất dạng `poi-{guid}` (20 ký tự) gắn với POI |
| **AnalyticsEvent** | Sự kiện ghi nhận lượt xem (`visit`) hoặc lượt nghe thuyết minh (`narration`) |
| **BasePoiId** | ID gốc dùng để gom nhóm đa ngôn ngữ của cùng một quán trong SQLite mobile |

---

## 2. Kiến trúc hệ thống

### 2.1 Clean Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      Presentation Layer                          │
│  VinhKhanh.Admin (ASP.NET Core 10 Web API — Controllers)        │
│  VinhKhanh.Admin.Ui (React + Vite + TailwindCSS)                │
│  VinhKhanh.Mobile (.NET MAUI Android — Pages + ViewModels)      │
├─────────────────────────────────────────────────────────────────┤
│                      Application Layer                           │
│  VinhKhanh.Application/UseCases/                                │
│    AdminApproveUseCase.ExecuteAsync(poiId)                      │
│    AnalyticsVisitUseCase.ExecuteAsync(command)                  │
│    PoiSyncUseCase.ExecuteAsync(request)                         │
├─────────────────────────────────────────────────────────────────┤
│                       Domain Layer                               │
│  VinhKhanh.Domain/Entities/ — Poi, ApplicationUser, Payment...  │
│  VinhKhanh.Domain/Interfaces/ — IPoiRepository, IAnalyticsRepo  │
├─────────────────────────────────────────────────────────────────┤
│                    Infrastructure Layer                          │
│  VinhKhanh.Infrastructure/Data/AppDbContext.cs (EF Core)        │
│  VinhKhanh.Infrastructure/Repositories/PoiRepository.cs         │
│  VinhKhanh.Infrastructure/Repositories/AnalyticsRepository.cs   │
│  VinhKhanh.Infrastructure/Services/GeminiAiService.cs           │
│  VinhKhanh.Infrastructure/Security/EncryptionUtility.cs         │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 Luồng dữ liệu tổng quát

```
Mobile App ──GET /api/pois/updates──► Backend API ──EF Core──► Database
                                           │
                                    GeminiAiService ──► Google Gemini API
                                           │
Admin Web UI ──REST API──► Backend API
                                           │
                                    SignalR Hub ──► Admin Dashboard (Realtime)
```

---

## 3. Danh sách chức năng hệ thống

### 3.1 Xác thực & Phân quyền (`AuthController`)

| # | Chức năng | Endpoint | Hàm xử lý | Mô tả |
|---|---|---|---|---|
| 1 | Đăng nhập JWT | `POST /api/auth/login` | `Login(LoginRequest)` | Xác thực email/password qua `userManager.FindByEmailAsync` + `CheckPasswordAsync`, kiểm tra `IsApproved`, tạo JWT 24h với claims `ClaimTypes.NameIdentifier`, `ClaimTypes.Role`, `ActivationDate` |
| 2 | Đăng ký ShopOwner | `POST /api/auth/register-shop` | `RegisterShop(RegisterShopRequest)` | Tạo `ApplicationUser` với `IsApproved=false`, gán role `ShopOwner` qua `userManager.AddToRoleAsync` |
| 3 | Đăng ký Visitor | `POST /api/auth/register-visitor` | `RegisterVisitor(RegisterVisitorRequest)` | Tạo `ApplicationUser` với `IsApproved=true`, `ActivationDate=DateTime.UtcNow`, gán role `Visitor` |

### 3.2 Quản lý POI — Admin (`AdminController`)

| # | Chức năng | Endpoint | Hàm xử lý | Mô tả |
|---|---|---|---|---|
| 4 | Xem tất cả POI | `GET /api/admin/pois` | `GetPois(CancellationToken)` | Query `dbContext.Pois.Include(Localizations).Include(Owner).OrderByDescending(Id)`, gọi `NormalizeCategoryCode()` cho từng POI |
| 5 | Xem POI chờ duyệt | `GET /api/admin/pois/pending` | `GetPendingPois(CancellationToken)` | Lọc `p.Status == PoiStatus.Pending_Approval`, sắp xếp `OrderBy(CreatedAt)` |
| 6 | Xem chi tiết POI | `GET /api/admin/pois/{poiId}` | `GetPoiById(int, CancellationToken)` | Trả kèm `qrLink = BuildQrLink(poi.QrToken)` |
| 7 | Tạo POI mới | `POST /api/admin/pois` | `CreatePoi(CreatePoiRequest, CancellationToken)` | Tạo `Poi` với `Status=Approved`, `IsApproved=true`, upload ảnh qua `UploadFileAsync()`, tạo 3 `PoiLocalization` (vi/en/ja), sinh `QrToken = "poi-{Guid:N}"[..20]` |
| 8 | Cập nhật POI | `PUT /api/admin/pois/{poiId}` | `UpdatePoi(int, CreatePoiRequest, CancellationToken)` | Gọi `UpsertLocalization()` cho 3 ngôn ngữ, upload ảnh mới nếu có |
| 9 | Duyệt POI | `POST /api/admin/pois/{poiId}/approve` | `Approve(int, CancellationToken)` | Set `poi.Status = PoiStatus.Approved`, `poi.IsApproved = true`, `poi.UpdatedAt = DateTime.UtcNow` |
| 10 | Từ chối POI | `POST /api/admin/pois/{poiId}/reject` | `RejectPoi(int, RejectPoiRequest, CancellationToken)` | Validate `reason.Length >= 10`, set `Status=Rejected`, `RejectionReason=reason` |
| 11 | Ẩn POI | `POST /api/admin/pois/{poiId}/hide` | `HidePoi(int, CancellationToken)` | Set `Status=Hidden`, `IsApproved=false` |
| 12 | Xóa POI | `DELETE /api/admin/pois/{poiId}` | `DeletePoi(int, CancellationToken)` | `dbContext.Pois.Remove(poi)` — Cascade xóa Localizations và Ratings |
| 13 | Reset QR Token | `POST /api/admin/pois/{poiId}/reset-qr` | `ResetQrToken(int, CancellationToken)` | Sinh token mới, kiểm tra unique bằng `AnyAsync(p => p.QrToken == token)` |
| 14 | Dashboard summary | `GET /api/admin/dashboard-summary` | `GetDashboardSummary(CancellationToken)` | Đếm POI, visit, narration, online (5 phút), `activitySeries` 8 giờ gần nhất |
| 15 | Duyệt ShopOwner | `POST /api/admin/approve-owner/{userId}` | `ApproveOwner(string)` | Set `user.IsApproved=true` qua `userManager.UpdateAsync` |
| 16 | Từ chối ShopOwner | `POST /api/admin/users/{userId}/reject-owner` | `RejectOwner(string)` | `userManager.DeleteAsync(user)` |
| 17 | Cập nhật ShopOwner | `PUT /api/admin/users/{userId}` | `UpdateOwner(string, UpdateOwnerRequest)` | Cập nhật `FullName`, `PhoneNumber`, xử lý Premium (`None/1Month/6Months/1Year`) |
| 18 | Toggle Premium | `POST /api/admin/users/{userId}/toggle-premium` | `TogglePremium(string)` | Bật/tắt `poi.IsPremium`, set `Priority=100` nếu Premium |
| 19 | AI dịch thuật (Admin) | `POST /api/admin/ai/generate` | `GenerateTranslations(AiTranslationRequest, CancellationToken)` | Gọi `geminiAiService.GenerateTranslationsAsync(name, description)` |

### 3.3 Cổng chủ quán (`ShopController`)

| # | Chức năng | Endpoint | Hàm xử lý | Mô tả |
|---|---|---|---|---|
| 20 | Xem danh sách POI của mình | `GET /api/shop/pois` | `GetMyPois(CancellationToken)` | Lọc `p.OwnerId == CurrentUserId`, kiểm tra `IsApprovedAsync()` |
| 21 | Xem chi tiết POI | `GET /api/shop/pois/{id}` | `GetMyPoi(int, CancellationToken)` | Kiểm tra `p.OwnerId == CurrentUserId` |
| 22 | Tạo POI nháp | `POST /api/shop/pois` | `CreatePoi(CreateShopPoiRequest, CancellationToken)` | Tạo `Poi` với `Status=Draft`, `IsApproved=false`, upload ảnh qua `UploadImageAsync()` |
| 23 | Cập nhật POI | `PUT /api/shop/pois/{id}` | `UpdatePoi(int, CreateShopPoiRequest, CancellationToken)` | Chỉ cho phép khi `Status != Pending_Approval && Status != Approved` |
| 24 | Xóa POI | `DELETE /api/shop/pois/{id}` | `DeletePoi(int, CancellationToken)` | Không xóa khi `Status == Pending_Approval` |
| 25 | Gửi duyệt | `POST /api/shop/pois/{id}/submit` | `SubmitPoi(int, CancellationToken)` | Chỉ cho phép khi `Status == Draft`, chuyển sang `Pending_Approval` |
| 26 | AI dịch thuật (Shop) | `POST /api/shop/ai/generate` | `GenerateAI(ShopAiRequest, CancellationToken)` | Gọi `gemini.GenerateTranslationsAsync()` qua DI |
| 27 | Xem thống kê cá nhân | `GET /api/shop/analytics` | `GetAnalytics(CancellationToken)` | Lọc events 30 ngày, đếm `visit` và `narration` theo từng POI |

### 3.4 Đồng bộ Mobile (`PoisController`)

| # | Chức năng | Endpoint | Hàm xử lý | Mô tả |
|---|---|---|---|---|
| 28 | Sync POI | `GET /api/pois/updates` | `GetUpdates(DateTime lastSync, CancellationToken)` | Gọi `syncUseCase.ExecuteAsync(SyncRequest)` → `PoiRepository.GetSyncPoisAsync(lastSyncAt)` |
| 29 | Sync POI (alias) | `GET /api/pois/sync` | `Sync(DateTime lastSync, CancellationToken)` | Alias của `/updates` |

### 3.5 Analytics (`AnalyticsController`)

| # | Chức năng | Endpoint | Hàm xử lý | Mô tả |
|---|---|---|---|---|
| 30 | Ghi sự kiện | `POST /api/analytics/visit` | `LogVisit(AnalyticsVisitCommand, CancellationToken)` | Gọi `visitUseCase.ExecuteAsync()`, upsert `FreeTrialRecord` nếu `eventType=narration`, gọi `PublishRealtimeUpdateAsync()` qua SignalR |
| 31 | Heatmap | `GET /api/analytics/heatmap` | `GetHeatmap(from, to, CancellationToken)` | Lọc events theo khoảng ngày, gọi `BuildHeatmapPoints()` — tính `density = uniqueDevices * 100 / 121` |
| 32 | Heatmap theo ngày | `GET /api/analytics/heatmap/daily` | `GetHeatmapByDay(date, CancellationToken)` | Lọc theo `DateOnly.TryParse(date)` |
| 33 | Heatmap lịch sử | `GET /api/analytics/heatmap/history` | `GetHeatmapHistory(from, to, CancellationToken)` | Group by `DateOnly.FromDateTime(Timestamp)` |
| 34 | Content performance | `GET /api/analytics/content-performance` | `GetContentPerformance(limit, from, to, CancellationToken)` | Group by `PoiId`, đếm visit/narration, `OrderByDescending(totalNarrations).Take(limit)` |
| 35 | Online count | `GET /api/analytics/online-count` | `GetOnlineCount(CancellationToken)` | Đếm `DeviceId` distinct trong 30 giây, loại `EventType=app_offline` |
| 36 | Realtime overview | `GET /api/analytics/realtime-overview` | `GetRealtimeOverview(CancellationToken)` | Gọi `BuildRealtimePayloadAsync()` — window 10 phút, recency weight |

### 3.6 Kiểm soát truy cập (`AccessController`)

| # | Chức năng | Endpoint | Hàm xử lý | Mô tả |
|---|---|---|---|---|
| 37 | Kiểm tra trạng thái | `GET /api/access/check` | `Check(deviceId, CancellationToken)` | Đếm `FreeTrialRecord` distinct PoiId, kiểm tra `DeviceTrial.ExpiryDate`, kiểm tra `Payment` còn hạn |
| 38 | Bắt đầu dùng thử | `POST /api/access/start-trial` | `StartTrial(deviceId, CancellationToken)` | Tạo `DeviceTrial` với `ExpiryDate = now.AddDays(7)` |

### 3.7 Thanh toán (`PaymentController`)

| # | Chức năng | Endpoint | Hàm xử lý | Mô tả |
|---|---|---|---|---|
| 39 | Khởi tạo giao dịch | `POST /api/payments/initiate` | `Initiate(InitiatePaymentRequest, CancellationToken)` | Tạo `Payment` với `Status=Pending`, kiểm tra `TransactionId` unique |
| 40 | Xác nhận thanh toán | `POST /api/payments/callback` | `Callback(PaymentCallbackRequest, CancellationToken)` | Set `Status=Completed`, `ExpiryDate = CreatedAt.AddDays(7)` nếu `AccessPass`; set `poi.IsPremium=true` nếu `PremiumUpgrade` |
| 41 | Kiểm tra trạng thái | `GET /api/payments/status` | `GetStatus(CancellationToken)` | Tìm `Payment` còn hạn của user hiện tại |

### 3.8 QR Code (`QrController`)

| # | Chức năng | Endpoint | Hàm xử lý | Mô tả |
|---|---|---|---|---|
| 42 | Redirect QR | `GET /qr/{token}` | `OpenPublicPoiPage(string, CancellationToken)` | Redirect sang `{webBaseUrl}/poi/qr/{token}` |
| 43 | Resolve QR | `GET /api/qr/{token}` | `Resolve(string, CancellationToken)` | Tìm `Poi` theo `QrToken` và `Status=Approved`, trả `deepLink = "vinhkhanh://poi/{id}?token=..."` |
| 44 | Tải QR PNG | `GET /api/qr/{token}/png` | `GetQrPng(string, CancellationToken)` | Dùng `QRCodeGenerator` + `PngByteQRCode.GetGraphic(20)` để sinh ảnh PNG |

### 3.9 Đánh giá POI (`PoiRatingsController`)

| # | Chức năng | Endpoint | Hàm xử lý | Mô tả |
|---|---|---|---|---|
| 45 | Xem điểm đánh giá | `GET /api/pois/{poiId}/ratings` | `GetSummary(int, deviceId, CancellationToken)` | Đếm ratings, tính `AverageAsync(r => r.Stars)`, tìm `userStars` theo `deviceId` |
| 46 | Gửi/cập nhật rating | `POST /api/pois/{poiId}/ratings` | `UpsertRating(int, SubmitPoiRatingRequest, CancellationToken)` | Validate `Stars 1-5`, upsert `PoiRating` theo `(poiId, deviceId)` unique index |

### 3.10 Cài đặt hệ thống (`SettingsController`)

| # | Chức năng | Endpoint | Hàm xử lý | Mô tả |
|---|---|---|---|---|
| 47 | Xem cài đặt | `GET /api/admin/settings` | `GetSettings(CancellationToken)` | `dbContext.SystemSettings.ToDictionaryAsync(Key, Value)` |
| 48 | Cập nhật cài đặt | `PUT /api/admin/settings` | `UpdateSettings(Dictionary<string,string>, CancellationToken)` | Upsert từng key-value vào `SystemSettings` |

### 3.11 Mobile Services

| # | Service | Hàm chính | Mô tả |
|---|---|---|---|
| 49 | `GeofenceEngine` | `StartAsync(languageCode)` | Khởi động engine, load POI từ `DatabaseService.GetLocalizedPoisAsync()`, đăng ký `_locationService.LocationChanged += OnLocationChanged` |
| 50 | `GeofenceEngine` | `ProcessLocationAsync(Location)` | Tính `CalculateDistance()` Haversine, phân loại `insideCandidates`/`outsidePois`, gọi `HandleExitedPois()` và `HandleInsidePoisWithPriorityAndDebounce()` |
| 51 | `GeofenceEngine` | `HandleInsidePoisWithPriorityAndDebounce()` | Tăng `_insideStableCounters[poi.Id]`, lọc `>= EnterDebounceThreshold(2)`, kiểm tra `_cooldownUntilUtc`, chọn POI `OrderByDescending(Priority).ThenBy(Id).First()`, kích hoạt `OnPoiEntered?.Invoke(selectedPoi)` |
| 52 | `NarrationService` | `PlayAudioAsync(filePath)` | Gọi `RunExclusiveNarrationAsync()` → `BeginAudioDuckingAsync()` → `PlayWithMediaElementAsync(mediaElement, assetPath, ct)` |
| 53 | `NarrationService` | `SpeakAsync(text, lang)` | Gọi `RunExclusiveNarrationAsync()` → `ResolveBestLocaleAsync()` → `TextToSpeech.Default.SpeakAsync()` |
| 54 | `LocationService` | `StartListeningAsync()` | Xin quyền `LocationWhenInUse`/`LocationAlways`, gọi `AndroidLocationForegroundController.Start()`, chạy `ListenLoopAsync()` |
| 55 | `LocationService` | `ListenLoopAsync(CancellationToken)` | Gọi `_geolocation.GetLocationAsync(GeolocationAccuracy.Best, 15s)`, kiểm tra `ShouldEmitLocationChanged()`, gọi `_analyticsService.TrackActivityAsync()` |
| 56 | `DatabaseService` | `SyncPoisFromServerAsync(CancellationToken)` | Lấy `lastSync` từ `Preferences`, GET `/api/pois/updates?lastSync=...`, gọi `ApplyServerChangesAsync()`, lưu `ServerTime` |
| 57 | `DatabaseService` | `GetLocalizedPoisAsync(langCode)` | Group by `BasePoiId`, gọi `SelectByFallback(variants, targetLang)` — Fallback chain: targetLang → en → vi → Priority cao nhất |
| 58 | `AccessService` | `SyncTrialStatusAsync()` | GET `/api/access/check?deviceId=...`, nếu user mới thì gọi `StartTrialAsync()` → POST `/api/access/start-trial` |
| 59 | `AnalyticsService` | `TrackActivityAsync(lat, lng, eventType, poiId)` | POST `/api/analytics/visit` với `DeviceId = _accessService.DeviceId` |

---
## 4. Use Case Diagram tổng quan

```mermaid
flowchart LR
  Admin["👤 Admin"]
  Shop["👤 ShopOwner"]
  Visitor["👤 Visitor / Mobile App"]

  subgraph Auth["Xác thực"]
    UC_Login["Đăng nhập JWT\nPOST /api/auth/login"]
    UC_RegShop["Đăng ký ShopOwner\nPOST /api/auth/register-shop"]
    UC_RegVisitor["Đăng ký Visitor\nPOST /api/auth/register-visitor"]
  end

  subgraph AdminMgmt["Quản trị (Admin)"]
    UC_ApprovePOI["Duyệt POI\nApprove()"]
    UC_RejectPOI["Từ chối POI\nRejectPoi()"]
    UC_HidePOI["Ẩn POI\nHidePoi()"]
    UC_ManagePOI["CRUD POI\nCreatePoi / UpdatePoi / DeletePoi"]
    UC_ApproveOwner["Duyệt ShopOwner\nApproveOwner()"]
    UC_Dashboard["Dashboard\nGetDashboardSummary()"]
    UC_Settings["Cài đặt hệ thống\nGetSettings / UpdateSettings"]
    UC_ResetQR["Reset QR Token\nResetQrToken()"]
  end

  subgraph ShopMgmt["Cổng chủ quán (ShopOwner)"]
    UC_CreateDraft["Tạo POI nháp\nCreatePoi() Status=Draft"]
    UC_EditPOI["Sửa POI\nUpdatePoi()"]
    UC_DeletePOI["Xóa POI\nDeletePoi()"]
    UC_SubmitPOI["Gửi duyệt\nSubmitPoi() → Pending_Approval"]
    UC_ShopAnalytics["Xem thống kê\nGetAnalytics()"]
  end

  subgraph AI["AI dịch thuật"]
    UC_AI["Dịch vi→en,ja\nGenerateTranslationsAsync()"]
  end

  subgraph MobileFeatures["Mobile (Visitor)"]
    UC_Sync["Đồng bộ POI\nGetUpdates() / SyncPoisFromServerAsync()"]
    UC_Narration["Thuyết minh tự động\nGeofenceEngine + NarrationService"]
    UC_Rating["Đánh giá POI\nUpsertRating()"]
    UC_QR["Quét QR\nResolve() / GetQrPng()"]
    UC_Trial["Dùng thử 7 ngày\nStartTrial()"]
    UC_Payment["Mua Access Pass\nInitiate() + Callback()"]
  end

  subgraph Analytics["Analytics"]
    UC_LogVisit["Ghi sự kiện\nLogVisit() / TrackActivityAsync()"]
    UC_Heatmap["Heatmap\nGetHeatmap()"]
    UC_ContentPerf["Content Performance\nGetContentPerformance()"]
    UC_Realtime["Realtime Dashboard\nSignalR AnalyticsHub"]
  end

  Admin --> UC_Login
  Admin --> UC_ApprovePOI
  Admin --> UC_RejectPOI
  Admin --> UC_HidePOI
  Admin --> UC_ManagePOI
  Admin --> UC_ApproveOwner
  Admin --> UC_Dashboard
  Admin --> UC_Settings
  Admin --> UC_ResetQR
  Admin --> UC_AI
  Admin --> UC_Heatmap
  Admin --> UC_ContentPerf
  Admin --> UC_Realtime

  Shop --> UC_Login
  Shop --> UC_RegShop
  Shop --> UC_CreateDraft
  Shop --> UC_EditPOI
  Shop --> UC_DeletePOI
  Shop --> UC_SubmitPOI
  Shop --> UC_ShopAnalytics
  Shop --> UC_AI

  Visitor --> UC_RegVisitor
  Visitor --> UC_Sync
  Visitor --> UC_Narration
  Visitor --> UC_Rating
  Visitor --> UC_QR
  Visitor --> UC_Trial
  Visitor --> UC_Payment
  Visitor --> UC_LogVisit
```

---

## 5. Class Diagram tổng quan

```mermaid
classDiagram
  %% ─── Domain Entities ───
  class Poi {
    +int Id
    +string BasePoiId
    +string CategoryCode
    +double Latitude
    +double Longitude
    +double Radius
    +string? ImageUrl
    +string? QrToken
    +int Priority
    +bool IsApproved
    +PoiStatus Status
    +bool IsPremium
    +DateTime? PremiumExpiryDate
    +string? OwnerId
    +string? RejectionReason
    +DateTime CreatedAt
    +DateTime UpdatedAt
    +ICollection~PoiLocalization~ Localizations
    +ApplicationUser? Owner
  }

  class PoiLocalization {
    +int Id
    +int PoiId
    +string LanguageCode
    +string Name
    +string Description
    +string? AudioUrl
    +Poi Poi
  }

  class PoiRating {
    +int Id
    +int PoiId
    +string DeviceId
    +int Stars
    +DateTime RatedAt
    +double? Latitude
    +double? Longitude
    +Poi? Poi
  }

  class ApplicationUser {
    +string Id
    +string FullName
    +int? PoiId
    +bool IsApproved
    +DateTime ActivationDate
  }

  class AnalyticsEvent {
    +int Id
    +double Latitude
    +double Longitude
    +DateTime Timestamp
    +string DeviceId
    +int? PoiId
    +string? EventType
  }

  class Payment {
    +int Id
    +string TransactionId
    +string UserId
    +decimal Amount
    +PaymentType Type
    +int? PoiId
    +PaymentStatus Status
    +DateTime? ExpiryDate
    +DateTime CreatedAt
    +ApplicationUser User
  }

  class FreeTrialRecord {
    +int Id
    +string? UserId
    +string? DeviceId
    +int PoiId
    +DateTime FirstHeardAt
  }

  class DeviceTrial {
    +string DeviceId
    +DateTime TrialStartDate
    +DateTime ExpiryDate
    +DateTime? LastCheckedAt
  }

  class SystemSetting {
    +string Key
    +string Value
  }

  class PoiStatus {
    <<enumeration>>
    Draft = 0
    Pending_Approval = 1
    Approved = 2
    Rejected = 3
    Hidden = 4
  }

  class PaymentType {
    <<enumeration>>
    AccessPass = 0
    PremiumUpgrade = 1
  }

  class PaymentStatus {
    <<enumeration>>
    Pending = 0
    Completed = 1
    Failed = 2
    Refunded = 3
  }

  %% ─── Interfaces ───
  class IPoiRepository {
    <<interface>>
    +GetSyncPoisAsync(lastSyncAt, ct) Task~IEnumerable~Poi~~
    +GetByIdAsync(id, ct) Task~Poi?~
    +ApprovePoiAsync(id, ct) Task~bool~
    +GetAllActiveBaseIdsAsync(ct) Task~List~string~~
  }

  class IAnalyticsRepository {
    <<interface>>
    +AddVisitEventAsync(evt, ct) Task
  }

  %% ─── Use Cases ───
  class AdminApproveUseCase {
    -IPoiRepository _repository
    +AdminApproveUseCase(IPoiRepository)
    +ExecuteAsync(poiId, ct) Task~bool~
  }

  class AnalyticsVisitUseCase {
    -IAnalyticsRepository _repository
    +ExecuteAsync(command, ct) Task
    -BuildAnonymousDeviceId(rawDeviceId) string
  }

  class PoiSyncUseCase {
    -IPoiRepository _repository
    +ExecuteAsync(request, ct) Task~SyncResponse~
    -NormalizeCategoryCode(code, name, desc) string
    -InferCategory(name, desc) string
  }

  class AnalyticsVisitCommand {
    +double Latitude
    +double Longitude
    +string DeviceId
    +int? PoiId
    +string? EventType
  }

  class SyncRequest {
    +DateTime LastSyncAt
  }

  class SyncResponse {
    +List~Poi~ UpdatedPois
    +List~int~ DeletedIds
    +List~string~ ActiveBasePoiIds
    +DateTime ServerTime
  }

  %% ─── Infrastructure ───
  class PoiRepository {
    -AppDbContext context
    +GetSyncPoisAsync(lastSyncAt, ct)
    +GetByIdAsync(id, ct)
    +ApprovePoiAsync(id, ct)
    +GetAllActiveBaseIdsAsync(ct)
  }

  class AnalyticsRepository {
    -AppDbContext context
    +AddVisitEventAsync(evt, ct)
  }

  class GeminiAiService {
    -HttpClient _httpClient
    -string _apiKey
    +GenerateTranslationsAsync(name, desc, ct) Task~GeminiTranslationResult?~
    -ExtractJsonPayload(rawText) string
  }

  class EncryptionUtility {
    -byte[] _key
    +EncryptionUtility(key)
    +Encrypt(plainText) string
    +Decrypt(cipherText) string
  }

  class AppDbContext {
    +DbSet~Poi~ Pois
    +DbSet~PoiLocalization~ PoiLocalizations
    +DbSet~AnalyticsEvent~ AnalyticsEvents
    +DbSet~Payment~ Payments
    +DbSet~FreeTrialRecord~ FreeTrialRecords
    +DbSet~DeviceTrial~ DeviceTrials
    +DbSet~PoiRating~ PoiRatings
    +DbSet~SystemSetting~ SystemSettings
    #OnModelCreating(ModelBuilder)
  }

  %% ─── Mobile Services ───
  class GeofenceEngine {
    -const int EnterDebounceThreshold = 2
    -TimeSpan DefaultCooldown = 10min
    -Dictionary~int,int~ _insideStableCounters
    -Dictionary~int,DateTimeOffset~ _cooldownUntilUtc
    -HashSet~int~ _activePoiIds
    -Dictionary~int,POI~ _poiMap
    +event Action~POI~ OnPoiEntered
    +event Action~POI~ OnPoiExited
    +StartAsync(languageCode) Task
    +StopAsync() Task
    +SetLanguageAsync(languageCode) Task
    +RefreshPoisAsync() Task
    +MarkPoiAsPlayed(poiId, cooldown?) void
    -OnLocationChanged(Location) void
    -ProcessLocationAsync(Location) Task
    -HandleExitedPois(outsidePois) void
    -HandleInsidePoisWithPriorityAndDebounce(candidates, now) void
    -RefreshPoisCoreAsync() Task
    -CalculateDistance(lat1,lon1,lat2,lon2) double
  }

  class NarrationService {
    -IAppLanguageService _appLanguageService
    -IAudioQueueManager _audioQueueManager
    +RegisterMediaElement(MediaElement?) void
    +SpeakAsync(text, lang) Task
    +PlayAudioAsync(filePath) Task
    +StopAll() void
    -RunExclusiveNarrationAsync(work) Task
    -SanitizeTtsText(input) string
    -ResolveBestLocaleAsync(languageCode) Task~Locale?~
    -PlayWithMediaElementAsync(mediaElement, assetPath, ct) Task
    -BeginAudioDuckingAsync() Task
    -EndAudioDucking() void
  }

  class LocationService {
    -const double DistanceFilterMeters = 1
    -TimeSpan ActiveInterval = 15s
    -TimeSpan IdleInterval = 15s
    +event Action~Location~ LocationChanged
    +StartListeningAsync() Task
    +StopListeningAsync() Task
    -ListenLoopAsync(CancellationToken) Task
    -ShouldEmitLocationChanged(Location) bool
    -UpdateAdaptiveInterval(Location) void
    -EnsureLocationPermissionsAsync() Task
  }

  class DatabaseService {
    -SQLiteAsyncConnection? _database
    -const string UpdatesEndpoint = "api/pois/updates"
    +InitializeAsync() Task
    +SyncPoisFromServerAsync(ct) Task~bool~
    +GetAllPoisAsync() Task~List~POI~~
    +GetLocalizedPoisAsync(langCode) Task~List~POI~~
    +AddPoiAsync(poi) Task~int~
    +UpdatePoiAsync(poi) Task~int~
    +DeletePoiAsync(poiId) Task~int~
    -ApplyServerChangesAsync(payload, ct) Task
    -SelectByFallback(variants, targetLang) POI?
    -NormalizeLanguageCode(code) string
    -InferCategory(name, desc) string
  }

  class AccessService {
    -const string AccessPassExpiryKey
    -string _deviceId
    +DeviceId string
    +HasActivePass() bool
    +GetExpiryDate() DateTime?
    +GetRemainingDays() int
    +SyncTrialStatusAsync() Task
    -GetPersistentDeviceId() string
    -StartTrialAsync() Task
    +PurchaseSuccess(days) void
  }

  class AnalyticsService {
    +TrackActivityAsync(lat, lng, eventType, poiId?) Task
    +TrackAppLifecycleAsync(status) Task
    -SendAnalyticsEventAsync(lat, lng, eventType, poiId?) Task
  }

  class AudioQueueManager {
    -SemaphoreSlim _queueLock
    -CancellationTokenSource? _currentCts
    +RunExclusiveAsync(work) Task
    +CancelCurrent() void
  }

  class Haversine {
    <<static>>
    +Distance(lat1,lon1,lat2,lon2) double
    -ToRad(deg) double
  }

  %% ─── Relationships ───
  Poi "1" --> "*" PoiLocalization : Localizations (Cascade)
  Poi "*" --> "1" ApplicationUser : Owner (OwnerId FK)
  Poi "1" --> "*" PoiRating : PoiRatings (Cascade)
  Poi "1" --> "*" AnalyticsEvent : PoiId FK
  Poi "1" --> "*" FreeTrialRecord : PoiId FK
  ApplicationUser "1" --> "*" Payment : UserId FK (Cascade)
  Poi --> PoiStatus
  Payment --> PaymentType
  Payment --> PaymentStatus

  AdminApproveUseCase --> IPoiRepository
  PoiSyncUseCase --> IPoiRepository
  AnalyticsVisitUseCase --> IAnalyticsRepository
  PoiSyncUseCase ..> SyncRequest
  PoiSyncUseCase ..> SyncResponse
  AnalyticsVisitUseCase ..> AnalyticsVisitCommand

  PoiRepository ..|> IPoiRepository
  AnalyticsRepository ..|> IAnalyticsRepository
  PoiRepository --> AppDbContext
  AnalyticsRepository --> AppDbContext

  GeofenceEngine --> LocationService
  GeofenceEngine --> DatabaseService
  NarrationService --> AudioQueueManager
  LocationService --> AnalyticsService
  AccessService --> AnalyticsService
  DatabaseService --> Haversine
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

**Ràng buộc quan trọng:**

- `PoiRating`: Unique index `(DeviceId, PoiId)`, Check constraint `Stars >= 1 AND Stars <= 5`
- `FreeTrialRecord`: Unique index `(UserId, PoiId)` khi `UserId IS NOT NULL`, Unique index `(DeviceId, PoiId)` khi `DeviceId IS NOT NULL`
- `Payment`: Unique index `TransactionId`
- `Poi.Status`: Enum `Draft=0, Pending_Approval=1, Approved=2, Rejected=3, Hidden=4`
- `Payment.Type`: Enum `AccessPass=0, PremiumUpgrade=1`
- `Payment.Status`: Enum `Pending=0, Completed=1, Failed=2, Refunded=3`

---

## 7. Sequence Diagrams theo chức năng

### 7.1 Đăng nhập & Đăng ký ShopOwner

```mermaid
sequenceDiagram
  actor ShopOwner
  participant Browser
  participant AuthController
  participant UserManager
  participant DB as Database
  actor Admin
  participant AdminController

  ShopOwner->>Browser: Điền form đăng ký
  Browser->>AuthController: POST /api/auth/register-shop<br/>{email, password, fullName, phoneNumber}
  AuthController->>UserManager: FindByEmailAsync(email)
  UserManager-->>AuthController: null (chưa tồn tại)
  AuthController->>UserManager: CreateAsync(user, password)
  Note over AuthController: user.IsApproved = false
  UserManager->>DB: INSERT ApplicationUser
  DB-->>UserManager: OK
  UserManager-->>AuthController: IdentityResult.Succeeded
  AuthController->>UserManager: AddToRoleAsync(user, "ShopOwner")
  UserManager->>DB: INSERT AspNetUserRoles
  DB-->>UserManager: OK
  AuthController-->>Browser: 200 - "Đăng ký thành công! Vui lòng chờ Admin duyệt."

  Admin->>AdminController: POST /api/admin/approve-owner/{userId}
  AdminController->>UserManager: FindByIdAsync(userId)
  UserManager->>DB: SELECT ApplicationUser WHERE Id=userId
  DB-->>UserManager: ApplicationUser
  UserManager-->>AdminController: user
  AdminController->>AdminController: user.IsApproved = true
  AdminController->>UserManager: UpdateAsync(user)
  UserManager->>DB: UPDATE ApplicationUser SET IsApproved=1
  DB-->>UserManager: OK
  AdminController-->>Admin: 200 - "Đã duyệt ShopOwner thành công."

  ShopOwner->>Browser: Đăng nhập
  Browser->>AuthController: POST /api/auth/login<br/>{email, password}
  AuthController->>UserManager: FindByEmailAsync(email)
  UserManager->>DB: SELECT ApplicationUser WHERE Email=email
  DB-->>UserManager: ApplicationUser
  UserManager-->>AuthController: user
  AuthController->>UserManager: CheckPasswordAsync(user, password)
  UserManager-->>AuthController: true
  AuthController->>AuthController: if (!user.IsApproved) return 403
  AuthController->>UserManager: GetRolesAsync(user)
  UserManager->>DB: SELECT AspNetRoles JOIN AspNetUserRoles
  DB-->>UserManager: ["ShopOwner"]
  UserManager-->>AuthController: roles
  AuthController->>AuthController: Tạo JWT token (24h)<br/>Claims: NameIdentifier, Role, ActivationDate
  AuthController-->>Browser: 200 - {token, expiration, roles}
```

### 7.2 Tạo & Duyệt POI

```mermaid
sequenceDiagram
  actor ShopOwner
  participant ShopController
  participant DB as Database
  participant FileSystem
  actor Admin
  participant AdminController

  ShopOwner->>ShopController: POST /api/shop/pois<br/>(multipart/form-data: nameVi, descVi, lat, lng, image)
  ShopController->>ShopController: IsApprovedAsync() → kiểm tra user.IsApproved
  ShopController->>DB: INSERT Poi<br/>(Status=Draft, IsApproved=false, OwnerId=CurrentUserId)
  DB-->>ShopController: poiId
  ShopController->>FileSystem: UploadImageAsync(image)
  FileSystem-->>ShopController: imageUrl = "/media/img_{guid}.jpg"
  ShopController->>DB: UPDATE Poi SET ImageUrl=imageUrl
  DB-->>ShopController: OK
  ShopController->>DB: INSERT PoiLocalization (vi, en, ja)
  DB-->>ShopController: OK
  ShopController-->>ShopOwner: 200 - {success: true, poiId}

  ShopOwner->>ShopController: POST /api/shop/pois/{id}/submit
  ShopController->>DB: SELECT Poi WHERE Id=id AND OwnerId=CurrentUserId
  DB-->>ShopController: poi
  ShopController->>ShopController: if (poi.Status != Draft) return BadRequest
  ShopController->>DB: UPDATE Poi SET Status=Pending_Approval, UpdatedAt=UtcNow
  DB-->>ShopController: OK
  ShopController-->>ShopOwner: 200 - {success: true}

  Admin->>AdminController: GET /api/admin/pois/pending
  AdminController->>DB: SELECT Poi WHERE Status=Pending_Approval<br/>INCLUDE Localizations, Owner<br/>ORDER BY CreatedAt
  DB-->>AdminController: List<Poi>
  AdminController-->>Admin: Danh sách POI chờ duyệt

  alt Duyệt POI
    Admin->>AdminController: POST /api/admin/pois/{id}/approve
    AdminController->>DB: SELECT Poi WHERE Id=id
    DB-->>AdminController: poi
    AdminController->>DB: UPDATE Poi SET Status=Approved, IsApproved=true, UpdatedAt=UtcNow
    DB-->>AdminController: OK
    AdminController-->>Admin: 200 - {success: true, message: "Đã duyệt thành công."}
  else Từ chối POI
    Admin->>AdminController: POST /api/admin/pois/{id}/reject<br/>{reason: "Thông tin địa chỉ không chính xác..."}
    AdminController->>AdminController: Validate reason.Length >= 10
    AdminController->>DB: UPDATE Poi SET Status=Rejected, RejectionReason=reason, IsApproved=false
    DB-->>AdminController: OK
    AdminController-->>Admin: 200 - {success: true}
  end
```

### 7.3 Đồng bộ Mobile

```mermaid
sequenceDiagram
  participant Mobile as Mobile App
  participant PoisController
  participant PoiSyncUseCase
  participant PoiRepository
  participant DB as Database
  participant SQLite as SQLite Local

  Mobile->>Mobile: Đọc lastSyncAt từ Preferences
  Mobile->>PoisController: GET /api/pois/updates?lastSync=2026-01-01T00:00:00Z
  PoisController->>PoiSyncUseCase: ExecuteAsync(SyncRequest{LastSyncAt})
  PoiSyncUseCase->>PoiRepository: GetSyncPoisAsync(lastSyncAt, ct)
  PoiRepository->>DB: SELECT Poi WHERE UpdatedAt > lastSyncAt<br/>AND Status=Approved<br/>INCLUDE Localizations
  DB-->>PoiRepository: List<Poi>
  PoiRepository-->>PoiSyncUseCase: entities
  PoiSyncUseCase->>PoiSyncUseCase: Map entities → Shared.Models.Poi<br/>NormalizeCategoryCode() cho từng POI
  PoiSyncUseCase->>PoiRepository: GetAllActiveBaseIdsAsync(ct)
  PoiRepository->>DB: SELECT DISTINCT BasePoiId WHERE Status=Approved
  DB-->>PoiRepository: activeIds
  PoiRepository-->>PoiSyncUseCase: activeIds
  PoiSyncUseCase-->>PoisController: SyncResponse{UpdatedPois, DeletedIds, ActiveBasePoiIds, ServerTime}
  PoisController-->>Mobile: 200 - SyncResponse JSON

  Mobile->>SQLite: ApplyServerChangesAsync(payload)
  loop Mỗi remotePoi trong UpdatedPois
    Mobile->>SQLite: Upsert POI theo (BasePoiId, LanguageCode)
  end
  loop Mỗi deletedId trong DeletedIds
    Mobile->>SQLite: DELETE FROM POI WHERE BasePoiId=deletedId OR Id=deletedId
  end
  Mobile->>SQLite: Pruning: DELETE FROM POI WHERE BasePoiId NOT IN (ActiveBasePoiIds)
  SQLite-->>Mobile: OK
  Mobile->>Mobile: Preferences.Set("root_last_sync_utc", ServerTime)
  Mobile->>Mobile: GeofenceEngine.RefreshPoisAsync()
```

### 7.4 Thuyết minh tự động (GeofenceEngine + NarrationService)

```mermaid
sequenceDiagram
  participant GPS as GPS Service
  participant LocationService
  participant GeofenceEngine
  participant NarrationService
  participant MediaElement
  participant AnalyticsController
  participant DB as Database

  GPS->>LocationService: OnLocationChanged(location)
  LocationService->>LocationService: ShouldEmitLocationChanged(location)<br/>DistanceFilter >= 1m hoặc MaxSilentEmit >= 15s
  LocationService->>LocationService: UpdateAdaptiveInterval(location)<br/>Tính speedKmh, điều chỉnh ActiveInterval/IdleInterval
  LocationService->>GeofenceEngine: LocationChanged?.Invoke(location)
  LocationService->>AnalyticsController: TrackActivityAsync(lat, lng, "location_update")
  AnalyticsController->>DB: INSERT AnalyticsEvent (EventType=location_update)

  GeofenceEngine->>GeofenceEngine: ProcessLocationAsync(location)
  loop Mỗi POI trong _cachedPois
    GeofenceEngine->>GeofenceEngine: distanceMeters = CalculateDistance(Haversine)
    alt distanceMeters <= poi.Radius
      GeofenceEngine->>GeofenceEngine: insideCandidates.Add(poi)
    else
      GeofenceEngine->>GeofenceEngine: outsidePois.Add(poi)
    end
  end

  GeofenceEngine->>GeofenceEngine: HandleExitedPois(outsidePois)
  loop Mỗi poi trong outsidePois
    GeofenceEngine->>GeofenceEngine: _insideStableCounters[poi.Id] = 0
    alt _activePoiIds.Remove(poi.Id)
      GeofenceEngine->>GeofenceEngine: OnPoiExited?.Invoke(poi)
    end
  end

  GeofenceEngine->>GeofenceEngine: HandleInsidePoisWithPriorityAndDebounce(insideCandidates, now)
  loop Mỗi poi trong insideCandidates
    GeofenceEngine->>GeofenceEngine: _insideStableCounters[poi.Id]++
  end
  GeofenceEngine->>GeofenceEngine: Lọc readyToEnter:<br/>counter >= EnterDebounceThreshold(2)<br/>!_activePoiIds.Contains(poi.Id)<br/>!_cooldownUntilUtc[poi.Id] > now
  GeofenceEngine->>GeofenceEngine: selectedPoi = readyToEnter<br/>.OrderByDescending(Priority).ThenBy(Id).First()
  GeofenceEngine->>GeofenceEngine: _activePoiIds.Add(selectedPoi.Id)
  GeofenceEngine->>NarrationService: OnPoiEntered?.Invoke(selectedPoi)

  NarrationService->>NarrationService: RunExclusiveNarrationAsync(work)
  NarrationService->>NarrationService: BeginAudioDuckingAsync() — Android AudioFocusRequest
  alt Có AudioUrl (MP3)
    NarrationService->>MediaElement: PlayWithMediaElementAsync(assetPath, ct)
    MediaElement->>MediaElement: mediaElement.Source = MediaSource.FromFile(assetPath)
    MediaElement->>MediaElement: mediaElement.Play()
    MediaElement-->>NarrationService: MediaEnded event
  else Không có audio
    NarrationService->>NarrationService: SanitizeTtsText(description)
    NarrationService->>NarrationService: ResolveBestLocaleAsync(lang) — Fallback chain
    NarrationService->>NarrationService: TextToSpeech.Default.SpeakAsync(text, locale, ct)
  end
  NarrationService->>NarrationService: EndAudioDucking()
  NarrationService->>AnalyticsController: POST /api/analytics/visit<br/>{eventType: "narration", poiId, lat, lng, deviceId}
  AnalyticsController->>DB: INSERT AnalyticsEvent (EventType=narration)
  AnalyticsController->>DB: Upsert FreeTrialRecord (deviceId, poiId)
  AnalyticsController->>AnalyticsController: PublishRealtimeUpdateAsync() — SignalR Hub
  AnalyticsController-->>NarrationService: 200 - {success: true}

  GeofenceEngine->>GeofenceEngine: MarkPoiAsPlayed(poiId, DefaultCooldown=10min)
  GeofenceEngine->>GeofenceEngine: _cooldownUntilUtc[poiId] = now + 10min
```

### 7.5 AI dịch thuật

```mermaid
sequenceDiagram
  actor User as Admin / ShopOwner
  participant UI as Admin Web UI
  participant AdminController
  participant GeminiAiService
  participant GeminiAPI as Google Gemini API

  User->>UI: Nhập tên và mô tả tiếng Việt
  User->>UI: Nhấn "Dịch tự động"
  UI->>AdminController: POST /api/admin/ai/generate<br/>{name: "Quán Ốc Bà Năm", description: "Quán ốc nổi tiếng..."}
  AdminController->>GeminiAiService: GenerateTranslationsAsync(name, description, ct)
  GeminiAiService->>GeminiAiService: Tạo prompt dịch vi → en, ja
  loop Fallback models: gemini-2.5-flash, gemini-1.5-flash, gemini-2.0-flash, gemini-2.5-flash-lite
    GeminiAiService->>GeminiAPI: POST /v1beta/models/{model}:generateContent<br/>?key={apiKey}<br/>{contents, generationConfig: {temperature: 0.4, responseMimeType: "application/json"}}
    alt Success 200
      GeminiAPI-->>GeminiAiService: {candidates[0].content.parts[0].text: JSON}
      GeminiAiService->>GeminiAiService: ExtractJsonPayload(textResult) — Loại bỏ ```json wrapper
      GeminiAiService->>GeminiAiService: JsonSerializer.Deserialize<GeminiTranslationResult>
      GeminiAiService-->>AdminController: {en: {name, description}, ja: {name, description}}
    else Error 503/429 (Ban/Quota)
      GeminiAPI-->>GeminiAiService: 503 Service Unavailable
      GeminiAiService->>GeminiAiService: Retry hoặc chuyển model tiếp theo
    end
  end
  AdminController-->>UI: 200 - {en: {...}, ja: {...}}
  UI->>UI: Tự điền các field nameEn, descEn, nameJa, descJa
  UI-->>User: Form đã được điền đầy đủ
```

### 7.6 Kiểm soát truy cập

```mermaid
sequenceDiagram
  participant Mobile as Mobile App
  participant AccessController
  participant DB as Database

  Mobile->>AccessController: GET /api/access/check?deviceId=ABC123
  AccessController->>DB: SELECT COUNT(DISTINCT PoiId) FROM FreeTrialRecord<br/>WHERE DeviceId='ABC123'
  DB-->>AccessController: freeTrialUsed = N
  AccessController->>DB: SELECT * FROM DeviceTrial WHERE DeviceId='ABC123'
  DB-->>AccessController: DeviceTrial (ExpiryDate)
  AccessController->>AccessController: isTrialActive = ExpiryDate > now
  AccessController->>DB: UPDATE DeviceTrial SET LastCheckedAt=now
  alt User đã đăng nhập (có JWT)
    AccessController->>DB: SELECT * FROM Payment<br/>WHERE UserId=userId AND Status=Completed AND ExpiryDate > now<br/>ORDER BY ExpiryDate DESC
    DB-->>AccessController: Payment hoặc null
    AccessController->>AccessController: hasActivePass = payment != null
  end
  AccessController-->>Mobile: 200 - {freeTrialUsed, freeTrialLimit: 3, hasActivePass, passExpiryDate, isTrial, trialRemainingDays}

  alt Cần mua Access Pass
    Mobile->>AccessController: POST /api/payments/initiate<br/>{transactionId: "TXN123", type: AccessPass}
    AccessController->>DB: SELECT * FROM Payment WHERE TransactionId='TXN123'
    DB-->>AccessController: null (chưa tồn tại)
    AccessController->>DB: INSERT Payment (Status=Pending, UserId, Amount=1.00, Type=AccessPass)
    DB-->>AccessController: paymentId
    AccessController-->>Mobile: 200 - {success: true, paymentId}

    Mobile->>AccessController: POST /api/payments/callback<br/>{transactionId: "TXN123"}
    AccessController->>DB: SELECT * FROM Payment WHERE TransactionId='TXN123'
    DB-->>AccessController: payment
    AccessController->>DB: UPDATE Payment SET Status=Completed, ExpiryDate=CreatedAt+7days
    DB-->>AccessController: OK
    AccessController-->>Mobile: 200 - {success: true, expiryDate}
  end
```

### 7.7 Quét QR

```mermaid
sequenceDiagram
  participant Mobile as Mobile App
  participant Camera as Camera / QR Scanner
  participant QrController
  participant DB as Database

  Mobile->>Camera: Mở camera quét QR
  Camera->>Camera: Nhận dạng QR code
  Camera-->>Mobile: QR token string

  Mobile->>QrController: GET /api/qr/{token}
  QrController->>DB: SELECT Poi WHERE QrToken=token AND Status=Approved<br/>INCLUDE Localizations
  alt POI tìm thấy
    DB-->>QrController: Poi + Localizations
    QrController->>QrController: webPoiUrl = "{webBaseUrl}/poi/qr/{token}"<br/>deepLink = "vinhkhanh://poi/{id}?token={token}"
    QrController->>DB: SELECT Value FROM SystemSettings WHERE Key='mobile.download.android'
    DB-->>QrController: androidStore
    QrController->>DB: SELECT Value FROM SystemSettings WHERE Key='mobile.download.ios'
    DB-->>QrController: iosStore
    QrController-->>Mobile: 200 - {poiId, basePoiId, qrToken, lat, lng, radius, imageUrl, webPoiUrl, deepLink, appLinks: {android, ios}, localizations}
    Mobile->>Mobile: Hiển thị thông tin POI
    Mobile->>Mobile: Tùy chọn phát thuyết minh ngay
  else Không tìm thấy
    DB-->>QrController: null
    QrController-->>Mobile: 404 - {error: "Không tìm thấy POI nào khớp với mã QR này."}
    Mobile->>Mobile: Hiển thị thông báo lỗi
  end
```

### 7.8 Đánh giá POI

```mermaid
sequenceDiagram
  participant Mobile as Mobile App
  participant PoiRatingsController
  participant DB as Database

  Mobile->>PoiRatingsController: GET /api/pois/{id}/ratings?deviceId=ABC123
  PoiRatingsController->>DB: SELECT COUNT(*) FROM Poi WHERE Id=id AND Status=Approved
  DB-->>PoiRatingsController: exists
  alt POI không tồn tại
    PoiRatingsController-->>Mobile: 404 - {error: "POI không tồn tại hoặc chưa được duyệt."}
  else POI tồn tại
    PoiRatingsController->>DB: SELECT COUNT(*), AVG(Stars) FROM PoiRating WHERE PoiId=id
    DB-->>PoiRatingsController: count, averageStars
    PoiRatingsController->>DB: SELECT Stars FROM PoiRating WHERE PoiId=id AND DeviceId='ABC123'
    DB-->>PoiRatingsController: userStars (nullable)
    PoiRatingsController-->>Mobile: 200 - {poiId, averageStars, ratingCount, userStars}
  end

  Mobile->>Mobile: Hiển thị sao đánh giá
  Mobile->>PoiRatingsController: POST /api/pois/{id}/ratings<br/>{stars: 5, deviceId: "ABC123", latitude: 10.7769, longitude: 106.7009}
  PoiRatingsController->>PoiRatingsController: Validate stars (1-5), deviceId không rỗng
  PoiRatingsController->>DB: SELECT * FROM Poi WHERE Id=id AND Status=Approved
  DB-->>PoiRatingsController: poi
  PoiRatingsController->>DB: SELECT * FROM PoiRating WHERE PoiId=id AND DeviceId='ABC123'
  alt Chưa có rating
    DB-->>PoiRatingsController: null
    PoiRatingsController->>DB: INSERT PoiRating (PoiId, DeviceId, Stars, RatedAt, Lat, Lng)
  else Đã có rating
    DB-->>PoiRatingsController: existing
    PoiRatingsController->>DB: UPDATE PoiRating SET Stars=5, RatedAt=now, Lat, Lng
  end
  DB-->>PoiRatingsController: OK
  PoiRatingsController->>DB: SELECT COUNT(*), AVG(Stars) FROM PoiRating WHERE PoiId=id
  DB-->>PoiRatingsController: count, average
  PoiRatingsController-->>Mobile: 200 - {success: true, poiId, userStars: 5, averageStars, ratingCount}
```

---
## 8. Activity Diagrams theo chức năng

### 8.1 Vòng đời POI (PoiStatus State Machine)

```mermaid
flowchart TD
  Start([Bắt đầu]) --> Draft["Status = Draft\nShopController.CreatePoi\nIsApproved=false"]
  Draft --> EditDraft{ShopOwner chỉnh sửa?}
  EditDraft -->|UpdatePoi - Draft/Rejected| Draft
  EditDraft -->|SubmitPoi - chỉ khi Draft| Pending["Status = Pending_Approval\nShopController.SubmitPoi"]
  Pending --> AdminReview{Admin xem xét}
  AdminReview --> ReviewNote["GET /api/admin/pois/pending"]
  ReviewNote --> ApproveDecision{Quyết định?}
  ApproveDecision -->|Approve| Approved["Status = Approved\nIsApproved=true\nHiển thị trên Mobile"]
  ApproveDecision -->|RejectPoi - reason >= 10 ký tự| Rejected["Status = Rejected\nRejectionReason lưu vào DB"]
  Rejected --> CanEdit{ShopOwner sửa lại?}
  CanEdit -->|UpdatePoi - cho phép khi Rejected| Draft
  CanEdit -->|Không| End1([Kết thúc])
  Approved --> HideCheck{Admin ẩn POI?}
  HideCheck -->|HidePoi - Status=Hidden| Hidden["Status = Hidden\nIsApproved=false\nKhông hiển thị Mobile"]
  HideCheck -->|Không| Active["POI hoạt động\nPoiRepository.GetSyncPoisAsync\ntrả về cho Mobile"]
  Hidden --> End2([Kết thúc])
  Active --> End3([POI hoạt động])
```

**Logic chi tiết:**
- `ShopController.UpdatePoi()`: Kiểm tra `poi.Status == Pending_Approval || poi.Status == Approved` → trả 403
- `ShopController.DeletePoi()`: Kiểm tra `poi.Status == Pending_Approval` → trả 403
- `ShopController.SubmitPoi()`: Kiểm tra `poi.Status != Draft` → trả 400
- `AdminController.RejectPoi()`: Validate `request.Reason.Length < 10` → trả 400

---

### 8.2 GeofenceEngine — Xử lý vị trí GPS

```mermaid
flowchart TD
  Start([GPS cập nhật vị trí]) --> LocationService["LocationService.ListenLoopAsync\n_geolocation.GetLocationAsync - Best, 15s"]
  LocationService --> ShouldEmit{ShouldEmitLocationChanged?}
  ShouldEmit --> ShouldEmitNote["distance >= 1m hoặc silent >= 15s"]
  ShouldEmitNote --> ShouldEmitDecision{Đủ điều kiện emit?}
  ShouldEmitDecision -->|Không| Delay["Task.Delay - _currentInterval\nActiveInterval=15s / IdleInterval=15s"]
  Delay --> LocationService
  ShouldEmitDecision -->|Có| Emit["LocationChanged?.Invoke\nTrackActivityAsync - location_update"]
  Emit --> ProcessLoc["GeofenceEngine.ProcessLocationAsync\n_processLock.WaitAsync"]
  ProcessLoc --> CleanCooldown["CleanupExpiredCooldown\nXóa _cooldownUntilUtc hết hạn"]
  CleanCooldown --> CalcDist["Loop _cachedPois\nCalculateDistance - Haversine\ndistanceMeters = earthRadius * 2 * Atan2"]
  CalcDist --> Classify{distanceMeters <= poi.Radius?}
  Classify -->|Không| OutsidePois["outsidePois.Add(poi)"]
  Classify -->|Có| InsidePois["insideCandidates.Add(poi)"]
  OutsidePois --> HandleExit["HandleExitedPois\n_insideStableCounters[poi.Id] = 0\nOnPoiExited?.Invoke(poi)"]
  InsidePois --> HandleInside["HandleInsidePoisWithPriorityAndDebounce"]
  HandleInside --> IncCounter["_insideStableCounters[poi.Id]++"]
  IncCounter --> CheckDebounce{counter >= EnterDebounceThreshold 2?}
  CheckDebounce -->|Không| End1([Chờ GPS tiếp theo])
  CheckDebounce -->|Có| CheckActive{poi.Id trong _activePoiIds?}
  CheckActive -->|Đã active| End2([Bỏ qua - đang phát])
  CheckActive -->|Chưa active| CheckCooldown{_cooldownUntilUtc còn hạn?}
  CheckCooldown -->|Còn cooldown| End3([Bỏ qua - đang cooldown])
  CheckCooldown -->|Hết cooldown| SelectPOI["selectedPoi = readyToEnter\n.OrderByDescending - Priority\n.ThenBy - Id .First()"]
  SelectPOI --> Preempt["Preemption: Xóa POI active Priority thấp hơn\nOnPoiExited?.Invoke(lowerPoi)"]
  Preempt --> FireEvent["_activePoiIds.Add(selectedPoi.Id)\nOnPoiEntered?.Invoke(selectedPoi)"]
  FireEvent --> End4([NarrationService xử lý])
  HandleExit --> End5([Chờ GPS tiếp theo])
```

---

### 8.3 NarrationService — Phát thuyết minh

```mermaid
flowchart TD
  Start([Nhận POI từ GeofenceEngine.OnPoiEntered]) --> CheckAccess{Có quyền truy cập?}
  CheckAccess --> AccessNote["AccessService.HasActivePass\nhoặc FreeTrialRecord < 3"]
  AccessNote --> AccessDecision{Kết quả kiểm tra?}
  AccessDecision -->|Không có quyền| ShowPaywall["Hiển thị Paywall\nPOST /api/payments/initiate"]
  ShowPaywall --> End1([Kết thúc])
  AccessDecision -->|Có quyền| RunExclusive["RunExclusiveNarrationAsync\nAudioQueueManager.RunExclusiveAsync\n_currentCts?.Cancel - Hủy narration cũ"]
  RunExclusive --> BeginDucking["BeginAudioDuckingAsync\nAndroid: AudioManager.RequestAudioFocus"]
  BeginDucking --> Delay120["Task.Delay 120ms - Ducking có hiệu lực"]
  Delay120 --> GetLang["_appLanguageService.GetEffectiveLanguage"]
  GetLang --> CheckAudio{poi.AudioPath có giá trị?}
  CheckAudio -->|Có MP3| NormPath["NormalizeAudioPath\nLoại bỏ Resources/Raw/ prefix"]
  NormPath --> EnsureAsset["EnsureAudioAssetExistsAsync\nFileSystem.OpenAppPackageFileAsync"]
  EnsureAsset --> PlayMP3["PlayWithMediaElementAsync\nmediaElement.Source = MediaSource.FromFile\nmediaElement.Play - Timeout 12s"]
  CheckAudio -->|Không có audio| SanitizeText["SanitizeTtsText\nLoại bỏ emoji, chuẩn hóa khoảng trắng"]
  SanitizeText --> ResolveLocale["ResolveBestLocaleAsync\nFallback: targetLang → en → vi"]
  ResolveLocale --> TTS["TextToSpeech.Default.SpeakAsync\nPitch=1.0, Rate=0.92, Volume=1.0"]
  PlayMP3 --> WaitFinish["Chờ MediaEnded event hoặc Timeout 12s"]
  TTS --> WaitFinish
  WaitFinish --> EndDucking["EndAudioDucking\nAndroid: AudioManager.AbandonAudioFocus"]
  EndDucking --> LogAnalytics["AnalyticsService.TrackActivityAsync - narration, poiId\nPOST /api/analytics/visit"]
  LogAnalytics --> MarkPlayed["GeofenceEngine.MarkPoiAsPlayed\n_cooldownUntilUtc[poiId] = now + 10min"]
  MarkPlayed --> End2([Hoàn thành])
```

---

### 8.4 Kiểm soát truy cập Visitor

```mermaid
flowchart TD
  Start([Visitor muốn nghe thuyết minh]) --> CheckLocalPass{Có Access Pass local?}
  CheckLocalPass --> LocalPassNote["AccessService.HasActivePass\nPreferences.Get - access_pass_expiry > now"]
  LocalPassNote --> LocalPassDecision{Còn hạn?}
  LocalPassDecision -->|Có| Allow1["Cho phép nghe\nNarrationService.PlayAudioAsync / SpeakAsync"]
  Allow1 --> End1([Phát thuyết minh])
  LocalPassDecision -->|Không| SyncTrial["AccessService.SyncTrialStatusAsync\nGET /api/access/check?deviceId=..."]
  SyncTrial --> CheckServerPass{data.HasActivePass?}
  CheckServerPass -->|Có| SaveExpiry["Preferences.Set - access_pass_expiry\nAllow nghe"]
  SaveExpiry --> End2([Phát thuyết minh])
  CheckServerPass -->|Không| CheckNewDevice{Thiết bị mới hoàn toàn?}
  CheckNewDevice --> NewDeviceNote["data.FreeTrialUsed == 0\nAND data.TrialRemainingDays == 0"]
  NewDeviceNote --> NewDeviceDecision{Kết quả?}
  NewDeviceDecision -->|Thiết bị mới| StartTrial["StartTrialAsync\nPOST /api/access/start-trial?deviceId=...\nExpiryDate = now + 7 days"]
  StartTrial --> SaveTrial["Preferences.Set - access_pass_expiry, ExpiryDate"]
  SaveTrial --> End3([Phát thuyết minh - Trial 7 ngày])
  NewDeviceDecision -->|Không phải mới| CheckFree{data.FreeTrialUsed < 3?}
  CheckFree -->|Có - còn slot miễn phí| Allow3["Cho phép nghe\nAnalyticsController.LogVisit upsert FreeTrialRecord"]
  Allow3 --> End4([Phát thuyết minh - Free Trial])
  CheckFree -->|Không - hết 3 POI| ShowPaywall["Hiển thị màn hình mua Access Pass"]
  ShowPaywall --> UserDecide{Người dùng quyết định}
  UserDecide -->|Mua Pass| InitPayment["POST /api/payments/initiate\ntransactionId, type: AccessPass"]
  InitPayment --> Callback["POST /api/payments/callback\ntransactionId"]
  Callback --> UpdateExpiry["Payment.ExpiryDate = CreatedAt + 7 days\nPreferences.Set - access_pass_expiry"]
  UpdateExpiry --> End5([Phát thuyết minh - Access Pass])
  UserDecide -->|Bỏ qua| End6([Không phát thuyết minh])
```

---

### 8.5 Đồng bộ dữ liệu Mobile (DatabaseService.SyncPoisFromServerAsync)

```mermaid
flowchart TD
  Start([Khởi động ứng dụng]) --> Init["DatabaseService.InitializeAsync\nSQLiteAsyncConnection - _databasePath\nCreateTableAsync POI"]
  Init --> CheckLegacy{BasePoiId là số nguyên?}
  CheckLegacy -->|Có - dữ liệu cũ| WipeDB["DeleteAllAsync POI\nPreferences.Remove - root_last_sync_utc"]
  WipeDB --> Schema["EnsureSchemaCompatibilityAsync\nALTER TABLE POI ADD COLUMN BasePoiId"]
  CheckLegacy -->|Không| Schema
  Schema --> Normalize["NormalizeBasePoiIdsAsync\nGroup by Category + Lat + Lng → gán BasePoiId"]
  Normalize --> CheckNet{Có kết nối Internet?}
  CheckNet -->|Không có mạng| UseCache["Dùng dữ liệu SQLite cũ\nGetLocalizedPoisAsync - langCode"]
  UseCache --> StartGeo1["GeofenceEngine.StartAsync - languageCode"]
  StartGeo1 --> End1([Ứng dụng hoạt động - Offline])
  CheckNet -->|Có mạng| ReadLastSync["GetLastSyncTime\nPreferences.Get - root_last_sync_utc"]
  ReadLastSync --> CallAPI["GET /api/pois/updates?lastSync=lastSync:O"]
  CallAPI --> CheckResp{API trả về thành công?}
  CheckResp -->|Thất bại| UseCache
  CheckResp -->|Thành công| ParseResp["Parse RemoteSyncResponse\nUpdatedPois, DeletedIds, ActiveBasePoiIds, ServerTime"]
  ParseResp --> ApplyChanges["ApplyServerChangesAsync\nUpsert POI theo BasePoiId + LanguageCode\nXóa DeletedIds\nPruning: Xóa POI ngoài ActiveBasePoiIds"]
  ApplyChanges --> SaveSync["SaveLastSyncTime - payload.ServerTime\nPreferences.Set - root_last_sync_utc"]
  SaveSync --> StartGeo2["GeofenceEngine.StartAsync\nRefreshPoisCoreAsync → GetLocalizedPoisAsync"]
  StartGeo2 --> End2([Ứng dụng hoạt động - Dữ liệu mới nhất])
```

---

### 8.6 Analytics Realtime (SignalR)

```mermaid
flowchart TD
  Start([Mobile gửi sự kiện]) --> LogVisit["AnalyticsController.LogVisit\nPOST /api/analytics/visit"]
  LogVisit --> ExecUseCase["AnalyticsVisitUseCase.ExecuteAsync\nBuildAnonymousDeviceId - SHA256 hash\nINSERT AnalyticsEvent"]
  ExecUseCase --> CheckNarration{EventType là narration?}
  CheckNarration --> NarrationNote["command.EventType == narration\nAND command.PoiId.HasValue"]
  NarrationNote --> NarrationDecision{Điều kiện đúng?}
  NarrationDecision -->|Có| CheckFreeTrialExists{FreeTrialRecord đã tồn tại?}
  CheckFreeTrialExists --> FreeTrialNote["Kiểm tra theo deviceId + poiId"]
  FreeTrialNote --> FreeTrialDecision{Đã có bản ghi?}
  FreeTrialDecision -->|Chưa có| InsertFreeTrial["INSERT FreeTrialRecord\nDeviceId, PoiId, FirstHeardAt=now"]
  FreeTrialDecision -->|Đã có| SkipFreeTrial["Bỏ qua - đã ghi nhận"]
  InsertFreeTrial --> Publish
  SkipFreeTrial --> Publish
  NarrationDecision -->|Không| Publish["PublishRealtimeUpdateAsync\nThrottle: 1 giây/lần"]
  Publish --> CheckThrottle{now - _lastRealtimePush < 1s?}
  CheckThrottle -->|Có| End1([Bỏ qua - throttled])
  CheckThrottle -->|Không| BuildPayload["BuildRealtimePayloadAsync\nWindow 10 phút\nGetOnlineUserCountInternal - 30s threshold\nBuildHeatmapPoints - recency weight"]
  BuildPayload --> SendSignalR["analyticsHub.Clients.Group - AdminGroup\n.SendAsync - analytics:realtime, payload"]
  SendSignalR --> End2([Admin Dashboard cập nhật realtime])
```

---

## Phụ lục: Cấu trúc thư mục dự án

```
VinhKhanh/
├── VinhKhanh.Admin/                    # ASP.NET Core 10 Web API
│   ├── Controllers/
│   │   ├── AuthController.cs           # Login, RegisterShop, RegisterVisitor
│   │   ├── AdminController.cs          # CRUD POI, Approve/Reject, Dashboard
│   │   ├── ShopController.cs           # ShopOwner POI management
│   │   ├── PoisController.cs           # Mobile sync endpoint
│   │   ├── AnalyticsController.cs      # Visit logging, Heatmap, SignalR
│   │   ├── AccessController.cs         # Trial/Pass check
│   │   ├── PaymentController.cs        # Initiate/Callback payment
│   │   ├── QrController.cs             # QR resolve + PNG generation
│   │   ├── PoiRatingsController.cs     # Star ratings upsert
│   │   └── SettingsController.cs       # System settings CRUD
│   ├── Hubs/
│   │   └── AnalyticsHub.cs             # SignalR hub realtime analytics
│   └── Program.cs                      # DI, JWT, EF Core, CORS config
│
├── VinhKhanh.Admin.Ui/                 # React + Vite + TailwindCSS
│   └── src/
│       ├── pages/                      # Dashboard, POI management, Analytics
│       ├── components/                 # Reusable UI components
│       └── services/                   # API service layer
│
├── VinhKhanh.Application/              # Use Cases
│   └── UseCases/
│       ├── AdminApproveUseCase.cs      # ExecuteAsync(poiId)
│       ├── AnalyticsVisitUseCase.cs    # ExecuteAsync(command) + SHA256 anonymize
│       └── PoiSyncUseCase.cs           # ExecuteAsync(request) + NormalizeCategoryCode
│
├── VinhKhanh.Domain/                   # Entities + Interfaces
│   ├── Entities/
│   │   ├── Poi.cs, PoiLocalization.cs, PoiRating.cs
│   │   ├── ApplicationUser.cs, Payment.cs
│   │   ├── AnalyticsEvent.cs, FreeTrialRecord.cs
│   │   ├── DeviceTrial.cs, SystemSetting.cs
│   │   └── PoiStatus.cs (enum)
│   └── Interfaces/
│       ├── IPoiRepository.cs
│       └── IAnalyticsRepository.cs
│
├── VinhKhanh.Infrastructure/           # EF Core + Services
│   ├── Data/AppDbContext.cs            # DbSets + OnModelCreating (Cascade, Unique indexes)
│   ├── Repositories/
│   │   ├── PoiRepository.cs            # GetSyncPoisAsync, ApprovePoiAsync
│   │   └── AnalyticsRepository.cs      # AddVisitEventAsync
│   ├── Services/GeminiAiService.cs     # GenerateTranslationsAsync + model fallback
│   └── Security/EncryptionUtility.cs   # AES-256 Encrypt/Decrypt
│
├── VinhKhanh.Mobile/                   # .NET MAUI Android
│   ├── Services/
│   │   ├── GeofenceEngine.cs           # Haversine + Debounce + Cooldown + Priority
│   │   ├── NarrationService.cs         # MP3 + TTS + AudioDucking
│   │   ├── LocationService.cs          # GPS polling + AdaptiveInterval
│   │   ├── DatabaseService.cs          # SQLite + Delta Sync + Language Fallback
│   │   ├── AccessService.cs            # DeviceId + Trial/Pass management
│   │   ├── AnalyticsService.cs         # TrackActivityAsync
│   │   └── AudioQueueManager.cs        # RunExclusiveAsync + CancelCurrent
│   └── ViewModels/
│       └── MainPageViewModel.cs        # ObservableCollection<POI> + UpdateLocalizedTextsInPlace
│
├── VinhKhanh.Shared/                   # DTOs dùng chung
│   ├── Models/Dtos.cs                  # SyncRequest, SyncResponse, NarrationEvent
│   ├── Models/Poi.cs                   # Shared Poi + PoiLocalizationDto
│   └── Haversine.cs                    # Distance(lat1,lon1,lat2,lon2) static
│
└── VinhKhanh.Tests/                    # Property-Based Tests
    ├── AccessController_Property9_Tests.cs
    ├── AdminController_Property2_Tests.cs
    ├── AnalyticsController_Property11_14_Tests.cs
    ├── AuthController_Property21_22_Tests.cs
    ├── NarrationEngine_Property15_20_Tests.cs
    ├── PaymentController_Property6_8_Tests.cs
    ├── PoisController_Property3_23_Tests.cs
    └── ShopController_Property1_10_Tests.cs
```

---

*Tài liệu được tạo tự động từ source code thực tế của dự án VinhKhanh — 24/04/2026*
