# VinhKhanh — Tài liệu PRD Tổng quan

> **Phiên bản:** 2.0 | **Ngày:** 24/04/2026

Tài liệu được tách thành các file riêng theo nhóm chức năng để dễ đọc và bảo trì.

---

## Danh sách tài liệu

| File | Nội dung |
|---|---|
| [01-auth.md](./01-auth.md) | Xác thực & Phân quyền — Login, RegisterShop, RegisterVisitor |
| [02-admin-poi.md](./02-admin-poi.md) | Quản lý POI (Admin) — CRUD, Approve, Reject, Hide, QR |
| [03-shop-owner.md](./03-shop-owner.md) | Cổng chủ quán (ShopOwner) — Draft, Submit, AI dịch thuật |
| [04-mobile-sync.md](./04-mobile-sync.md) | Đồng bộ Mobile — Delta Sync, SQLite, Language Fallback |
| [05-geofence-narration.md](./05-geofence-narration.md) | Geofence & Thuyết minh — GPS, Debounce, Cooldown, MP3/TTS |
| [06-access-payment.md](./06-access-payment.md) | Kiểm soát truy cập & Thanh toán — Trial, FreeTrial, AccessPass |
| [07-analytics.md](./07-analytics.md) | Analytics & Realtime — Heatmap, Content Performance, SignalR |
| [08-ratings-qr-settings.md](./08-ratings-qr-settings.md) | Đánh giá POI, QR Code, Cài đặt hệ thống |

---

## Kiến trúc tổng quan

```
┌─────────────────────────────────────────────────────────────────┐
│                      Presentation Layer                          │
│  VinhKhanh.Admin (ASP.NET Core 10 — Controllers)                │
│  VinhKhanh.Admin.Ui (React + Vite + TailwindCSS)                │
│  VinhKhanh.Mobile (.NET MAUI Android)                           │
├─────────────────────────────────────────────────────────────────┤
│                      Application Layer                           │
│  AdminApproveUseCase / AnalyticsVisitUseCase / PoiSyncUseCase   │
├─────────────────────────────────────────────────────────────────┤
│                       Domain Layer                               │
│  Entities: Poi, ApplicationUser, Payment, AnalyticsEvent...     │
│  Interfaces: IPoiRepository, IAnalyticsRepository               │
├─────────────────────────────────────────────────────────────────┤
│                    Infrastructure Layer                          │
│  AppDbContext (EF Core) / PoiRepository / AnalyticsRepository   │
│  GeminiAiService / EncryptionUtility                            │
└─────────────────────────────────────────────────────────────────┘
```

## ERD tổng quan

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

**Ràng buộc DB quan trọng:**
- `PoiRating`: Unique index `(DeviceId, PoiId)` — mỗi thiết bị chỉ có 1 rating/POI; Check `Stars >= 1 AND Stars <= 5`
- `FreeTrialRecord`: Unique index `(UserId, PoiId)` khi `UserId IS NOT NULL`; Unique index `(DeviceId, PoiId)` khi `DeviceId IS NOT NULL`
- `Payment.TransactionId`: Unique index — tránh duplicate transaction
- `PoiStatus` enum: `Draft=0, Pending_Approval=1, Approved=2, Rejected=3, Hidden=4`
- `PaymentType` enum: `AccessPass=0, PremiumUpgrade=1`
- `PaymentStatus` enum: `Pending=0, Completed=1, Failed=2, Refunded=3`
