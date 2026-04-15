# Thiết kế kỹ thuật: Đánh giá POI theo thang 5 sao (poi-rating)

## Tổng quan

Tính năng **poi-rating** cho phép Visitor đã đăng nhập đánh giá các POI (điểm tham quan, địa điểm ăn uống) theo thang điểm 5 sao. Hệ thống bao gồm:

- **Backend (ASP.NET Core)**: `RatingController` xử lý API, `RatingRepository` lưu trữ và tổng hợp dữ liệu, entity `PoiRating` trong SQL Server.
- **Mobile (.NET MAUI)**: `RatingService` giao tiếp với API, `StarRatingControl` hiển thị và nhận tương tác, `DatabaseService` đồng bộ điểm trung bình về local SQLite.

Tính năng tích hợp vào cơ chế sync delta hiện có: `SyncResponse` được mở rộng để mang `AverageRating` và `RatingCount` về mobile sau mỗi lần đồng bộ.

---

## Kiến trúc

Hệ thống tuân theo kiến trúc Clean Architecture hiện có của dự án:

```
┌─────────────────────────────────────────────────────────────────┐
│                    .NET MAUI Mobile App                         │
│  ┌──────────────────┐   ┌──────────────────┐                   │
│  │  StarRatingControl│   │  RatingService   │                   │
│  │  (XAML Control)  │──▶│  (HTTP Client)   │                   │
│  └──────────────────┘   └────────┬─────────┘                   │
│  ┌──────────────────┐            │                              │
│  │  DatabaseService │◀───────────┘ (sync AverageRating)        │
│  │  (SQLite local)  │                                           │
│  └──────────────────┘                                           │
└─────────────────────────────────────────────────────────────────┘
                              │ HTTP/JWT
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                  ASP.NET Core Backend                           │
│  ┌──────────────────┐   ┌──────────────────┐                   │
│  │  RatingController│──▶│  IRatingRepository│                  │
│  │  /api/ratings    │   │  (Interface)      │                   │
│  └──────────────────┘   └────────┬─────────┘                   │
│                                  │                              │
│  ┌──────────────────┐   ┌────────▼─────────┐                   │
│  │  PoisController  │   │  RatingRepository │                   │
│  │  /api/pois       │   │  (EF Core)        │                   │
│  └──────────────────┘   └────────┬─────────┘                   │
│                                  │                              │
│                         ┌────────▼─────────┐                   │
│                         │  SQL Server DB    │                   │
│                         │  PoiRatings table │                   │
│                         └──────────────────┘                   │
└─────────────────────────────────────────────────────────────────┘
```

**Luồng đánh giá:**
1. Visitor chọn sao trên `StarRatingControl` → `RatingService.SubmitRatingAsync(poiId, score)`
2. `RatingService` gửi `POST /api/ratings` với JWT token
3. `RatingController` xác thực token, kiểm tra PoiId, upsert vào `PoiRatings`
4. Trả về 201 (tạo mới) hoặc 200 (cập nhật)

**Luồng đồng bộ:**
1. `DatabaseService.SyncPoisFromServerAsync()` gọi `GET /api/pois/updates`
2. `SyncResponse` mang thêm `AverageRating` và `RatingCount` cho mỗi POI
3. `DatabaseService` lưu vào cột `AverageRating` và `RatingCount` trong bảng `POI` local

---

## Thành phần và Giao diện

### Backend

#### `IRatingRepository` (Domain Layer)

```csharp
namespace VinhKhanh.Domain.Interfaces;

public interface IRatingRepository
{
    /// <summary>
    /// Upsert rating: tạo mới nếu chưa có, cập nhật nếu đã có cho cặp (poiId, visitorId).
    /// Trả về (rating, isNew): isNew=true nếu vừa tạo mới.
    /// </summary>
    Task<(PoiRating rating, bool isNew)> UpsertAsync(int poiId, string visitorId, int score, CancellationToken ct = default);

    /// <summary>
    /// Lấy rating của một Visitor cho một POI cụ thể. Trả về null nếu chưa đánh giá.
    /// </summary>
    Task<PoiRating?> GetByVisitorAsync(int poiId, string visitorId, CancellationToken ct = default);

    /// <summary>
    /// Tính AverageRating và RatingCount cho một POI.
    /// </summary>
    Task<(double averageRating, int ratingCount)> GetSummaryAsync(int poiId, CancellationToken ct = default);
}
```

#### `RatingController` (Admin/API Layer)

```csharp
[ApiController]
[Route("api/ratings")]
public class RatingController : ControllerBase
{
    // POST /api/ratings
    // Body: { poiId: int, score: int }
    // Auth: [Authorize(Roles = "Visitor,Admin")]
    // Returns: 201 (created) | 200 (updated) | 400 | 401 | 403 | 404
    [HttpPost]
    [Authorize(Roles = "Visitor,Admin")]
    public Task<IActionResult> SubmitRating([FromBody] SubmitRatingRequest request, CancellationToken ct);

    // GET /api/pois/{poiId}/rating-summary
    // Returns: { averageRating: double, ratingCount: int }
    [HttpGet("/api/pois/{poiId:int}/rating-summary")]
    public Task<IActionResult> GetRatingSummary(int poiId, CancellationToken ct);
}
```

#### `RatingService` (Mobile Layer)

```csharp
public interface IRatingService
{
    /// <summary>
    /// Gửi đánh giá lên server. Ném RatingException nếu lỗi mạng hoặc server.
    /// </summary>
    Task<RatingResult> SubmitRatingAsync(int poiId, int score, CancellationToken ct = default);
}
```

#### `StarRatingControl` (Mobile XAML Control)

Bindable properties:
- `AverageRating` (double): điểm trung bình hiển thị
- `RatingCount` (int): số lượt đánh giá
- `UserRating` (int): điểm Visitor đã chọn (0 = chưa đánh giá)
- `IsInteractive` (bool): cho phép tương tác hay chỉ đọc
- `Command` (ICommand): được gọi khi Visitor xác nhận chọn sao

---

## Mô hình dữ liệu

### Backend: Entity `PoiRating`

```csharp
namespace VinhKhanh.Domain.Entities;

public class PoiRating
{
    public int Id { get; set; }
    public int PoiId { get; set; }
    public string VisitorId { get; set; } = string.Empty;  // ApplicationUser.Id
    public int Score { get; set; }                          // [1, 5]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Poi? Poi { get; set; }
    public ApplicationUser? Visitor { get; set; }
}
```

**Ràng buộc DB (EF Core `OnModelCreating`):**
- Unique index trên `(PoiId, VisitorId)` — đảm bảo mỗi Visitor chỉ có 1 rating/POI
- Foreign key `PoiId → Pois.Id` (cascade delete)
- Foreign key `VisitorId → AspNetUsers.Id` (no action)
- `Score` check constraint: `Score >= 1 AND Score <= 5`

### Backend: DTO

```csharp
// Request
public class SubmitRatingRequest
{
    public int PoiId { get; set; }
    public int Score { get; set; }  // [1, 5]
}

// Response
public class RatingResponse
{
    public int Id { get; set; }
    public int PoiId { get; set; }
    public int Score { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class RatingSummaryResponse
{
    public double AverageRating { get; set; }
    public int RatingCount { get; set; }
}
```

### Shared: Mở rộng `SyncResponse` / `Poi` DTO

```csharp
// VinhKhanh.Shared/Models/Poi.cs — thêm 2 trường
public class Poi
{
    // ... existing fields ...
    public double AverageRating { get; set; } = 0.0;
    public int RatingCount { get; set; } = 0;
}
```

### Mobile: Mở rộng model `POI` (SQLite)

```csharp
// VinhKhanh.Mobile/Models/POI.cs — thêm 2 trường lưu DB
public double AverageRating { get; set; } = 0.0;
public int RatingCount { get; set; } = 0;
```

### Mobile: `RatingResult`

```csharp
public class RatingResult
{
    public bool IsSuccess { get; set; }
    public bool IsNew { get; set; }       // true = tạo mới, false = cập nhật
    public string? ErrorMessage { get; set; }
}
```

---

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Upsert không tạo bản ghi trùng lặp

*For any* cặp `(poiId, visitorId)` hợp lệ, dù gọi `UpsertAsync` bao nhiêu lần với các `score` khác nhau, số lượng bản ghi `PoiRating` trong DB cho cặp đó phải luôn bằng đúng 1.

**Validates: Requirements 1.3, 4.3**

---

### Property 2: Score ngoài [1, 5] luôn bị từ chối

*For any* giá trị `score` nằm ngoài khoảng `[1, 5]` (bao gồm 0, số âm, số > 5), `RatingController` phải trả về HTTP 400.

**Validates: Requirements 1.4**

---

### Property 3: PoiId không tồn tại luôn trả về 404

*For any* `poiId` không tồn tại trong bảng `Pois`, yêu cầu đánh giá phải trả về HTTP 404.

**Validates: Requirements 1.6**

---

### Property 4: Tính nhất quán của AverageRating

*For any* tập hợp `scores` không rỗng (mỗi score ∈ [1, 5]) thuộc một POI, `AverageRating` được tính bởi `RatingRepository` phải bằng `Math.Round(scores.Sum() / (double)scores.Count, 1, MidpointRounding.AwayFromZero)`.

**Validates: Requirements 4.2, 4.5**

---

### Property 5: Đồng bộ ghi đè giá trị cũ

*For any* cặp `(oldRating, newRating)` với `oldRating ≠ newRating`, sau khi `DatabaseService` xử lý `SyncResponse` chứa `newRating` cho một POI, giá trị `AverageRating` trong SQLite local phải bằng `newRating` (không phải `oldRating`).

**Validates: Requirements 3.1, 3.3**

---

### Property 6: Yêu cầu không có JWT hợp lệ bị từ chối

*For any* yêu cầu gửi đánh giá không kèm JWT token hợp lệ có role `Visitor` hoặc `Admin` (bao gồm: không có token, token hết hạn, token sai role), `RatingController` phải trả về HTTP 401 hoặc 403.

**Validates: Requirements 1.5, 5.3**

---

### Property 7: Tất cả trường bắt buộc được lưu đầy đủ

*For any* rating hợp lệ được tạo qua `UpsertAsync`, bản ghi trong DB phải có đầy đủ: `PoiId`, `VisitorId`, `Score ∈ [1,5]`, `CreatedAt` (UTC), `UpdatedAt` (UTC ≥ CreatedAt).

**Validates: Requirements 4.1, 4.4**

---

### Property 8: Làm tròn AverageRating về 0.5 sao gần nhất

*For any* giá trị `averageRating ∈ [0.0, 5.0]`, hàm làm tròn hiển thị của `StarRatingControl` phải trả về giá trị là bội số của 0.5 gần nhất với `averageRating`.

**Validates: Requirements 2.1**

---

## Xử lý lỗi

### Backend

| Tình huống | HTTP Status | Mô tả |
|---|---|---|
| Score ngoài [1, 5] | 400 Bad Request | `"Score phải nằm trong khoảng [1, 5]"` |
| Không có JWT / JWT không hợp lệ | 401 Unauthorized | ASP.NET Core Identity tự xử lý |
| Visitor bị vô hiệu hóa (IsApproved=false) | 403 Forbidden | `"Tài khoản chưa được kích hoạt"` |
| PoiId không tồn tại | 404 Not Found | `"Không tìm thấy POI với Id={poiId}"` |
| Lỗi DB | 500 Internal Server Error | Log chi tiết, trả về message chung |

### Mobile

| Tình huống | Xử lý |
|---|---|
| Không có kết nối mạng | `RatingService` kiểm tra `Connectivity.NetworkAccess` trước khi gọi API, ném `RatingException("Không có kết nối mạng")` |
| Server trả về lỗi (4xx/5xx) | Parse error message từ response body, hiển thị cho Visitor |
| Timeout | Sau 15 giây, hiển thị thông báo "Kết nối quá chậm, vui lòng thử lại" |
| Visitor chưa đăng nhập | `StarRatingControl` ở chế độ read-only; khi tap hiển thị dialog yêu cầu đăng nhập |

---

## Chiến lược kiểm thử

### Unit Tests (xUnit + Moq)

Các trường hợp cụ thể cần test:

- `RatingController`: score=0, score=6 → 400; poiId không tồn tại → 404; không có token → 401; IsApproved=false → 403
- `RatingRepository.GetSummaryAsync`: POI không có rating → (0.0, 0); POI có 1 rating → (score, 1)
- `StarRatingControl`: AverageRating=0 → hiển thị "Chưa có đánh giá"; UserRating=3 → highlighted sao 1-3
- `RatingService`: network unavailable → ném exception, không gọi HTTP

### Property-Based Tests (FsCheck.Xunit — tối thiểu 100 iterations mỗi property)

Dự án đã sử dụng **FsCheck.Xunit** (xem `VinhKhanh.Tests.csproj`). Mỗi property test phải được tag theo format:

```
// Feature: poi-rating, Property {N}: {property_text}
```

**Property 1** — Upsert không tạo bản ghi trùng lặp:
- Generator: `(poiId: int, visitorId: string, scores: NonEmptyArray<int[1..5]>)`
- Kiểm tra: sau khi upsert tất cả scores, `COUNT(*) WHERE PoiId=poiId AND VisitorId=visitorId == 1`

**Property 2** — Score ngoài [1, 5] bị từ chối:
- Generator: `score: int` với filter `score < 1 || score > 5`
- Kiểm tra: response.StatusCode == 400

**Property 3** — PoiId không tồn tại → 404:
- Generator: `poiId: int` không có trong DB (dùng InMemory DB rỗng)
- Kiểm tra: response.StatusCode == 404

**Property 4** — Tính nhất quán AverageRating:
- Generator: `scores: NonEmptyArray<int>` với mỗi score ∈ [1, 5]
- Kiểm tra: `repository.GetSummaryAsync(poiId).averageRating == Math.Round(scores.Average(), 1, MidpointRounding.AwayFromZero)`

**Property 5** — Đồng bộ ghi đè giá trị cũ:
- Generator: `(oldRating: double[0..5], newRating: double[0..5], ratingCount: int[0..1000])`
- Kiểm tra: sau `ApplyServerChanges`, `poi.AverageRating == newRating`

**Property 6** — JWT không hợp lệ bị từ chối:
- Generator: các loại token không hợp lệ (null, expired, wrong role)
- Kiểm tra: response.StatusCode ∈ {401, 403}

**Property 7** — Tất cả trường bắt buộc được lưu:
- Generator: `(poiId: int, visitorId: string, score: int[1..5])`
- Kiểm tra: bản ghi có đủ tất cả trường, `UpdatedAt >= beforeOperation`

**Property 8** — Làm tròn về 0.5 sao:
- Generator: `averageRating: double[0.0..5.0]`
- Kiểm tra: `result % 0.5 == 0.0` và `Math.Abs(result - averageRating) <= 0.25`

### Integration Tests

- `GET /api/pois/{poiId}/rating-summary` trả về đúng format `{averageRating, ratingCount}`
- Luồng đầy đủ: đăng nhập → gửi rating → sync → kiểm tra AverageRating trong local DB
- Kiểm tra unique constraint DB: insert 2 ratings cùng (PoiId, VisitorId) → chỉ có 1 bản ghi
