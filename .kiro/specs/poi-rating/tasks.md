# Kế hoạch triển khai: Đánh giá POI theo thang 5 sao (poi-rating)

## Tổng quan

Triển khai tính năng đánh giá POI theo thang 5 sao trên toàn bộ stack: entity và repository phía backend (ASP.NET Core + EF Core), controller API, mở rộng cơ chế sync, và UI control phía mobile (.NET MAUI). Các bước được sắp xếp theo thứ tự từ tầng domain → infrastructure → API → mobile, đảm bảo mỗi bước tích hợp vào code hiện có.

## Tasks

- [x] 1. Tạo entity `PoiRating` và cấu hình EF Core
  - Tạo file `VinhKhanh.Domain/Entities/PoiRating.cs` với các trường: `Id`, `PoiId`, `VisitorId`, `Score`, `CreatedAt`, `UpdatedAt`
  - Thêm cấu hình EF Core vào `AppDbContext.OnModelCreating`: unique index `(PoiId, VisitorId)`, foreign key `PoiId → Pois.Id` (cascade delete), foreign key `VisitorId → AspNetUsers.Id` (no action), check constraint `Score >= 1 AND Score <= 5`
  - Lưu ý: `AppDbContext` đã có `DbSet<PoiRating>` — chỉ cần tạo entity và thêm cấu hình model
  - _Requirements: 4.1, 4.3, 4.4_

- [x] 2. Tạo `IRatingRepository` và `RatingRepository`
  - [x] 2.1 Tạo interface `VinhKhanh.Domain/Interfaces/IRatingRepository.cs` với 3 phương thức: `UpsertAsync`, `GetByVisitorAsync`, `GetSummaryAsync`
    - _Requirements: 1.2, 1.3, 4.2, 4.5_

  - [x] 2.2 Tạo `VinhKhanh.Infrastructure/Repositories/RatingRepository.cs` implement `IRatingRepository`
    - `UpsertAsync`: dùng `FirstOrDefaultAsync` để tìm bản ghi theo `(poiId, visitorId)`, nếu có thì cập nhật `Score` và `UpdatedAt`, nếu không thì tạo mới với `CreatedAt = UpdatedAt = DateTime.UtcNow`
    - `GetSummaryAsync`: dùng LINQ `Average()` và `Count()` trên `PoiRatings` theo `poiId`; trả về `(0.0, 0)` nếu không có rating; làm tròn bằng `Math.Round(..., 1, MidpointRounding.AwayFromZero)`
    - `GetByVisitorAsync`: truy vấn theo `(poiId, visitorId)`
    - _Requirements: 1.2, 1.3, 4.1, 4.2, 4.3, 4.4, 4.5_

  - [x] 2.3 Viết property test cho Property 1: Upsert không tạo bản ghi trùng lặp
    - **Property 1: Upsert không tạo bản ghi trùng lặp**
    - File: `VinhKhanh.Tests/RatingRepository_Property1_Tests.cs`
    - Generator: `(poiId: int, visitorId: NonEmptyString, scores: NonEmptyArray<int[1..5]>)`
    - Kiểm tra: sau khi upsert tất cả scores, `COUNT(*) WHERE PoiId=poiId AND VisitorId=visitorId == 1`
    - **Validates: Requirements 1.3, 4.3**

  - [x] 2.4 Viết property test cho Property 4: Tính nhất quán của AverageRating
    - **Property 4: Tính nhất quán của AverageRating**
    - File: `VinhKhanh.Tests/RatingRepository_Property4_Tests.cs`
    - Generator: `scores: NonEmptyArray<int>` với mỗi score ∈ [1, 5]
    - Kiểm tra: `GetSummaryAsync(poiId).averageRating == Math.Round(scores.Average(), 1, MidpointRounding.AwayFromZero)`
    - **Validates: Requirements 4.2, 4.5**

  - [x] 2.5 Viết property test cho Property 7: Tất cả trường bắt buộc được lưu đầy đủ
    - **Property 7: Tất cả trường bắt buộc được lưu đầy đủ**
    - File: `VinhKhanh.Tests/RatingRepository_Property7_Tests.cs`
    - Generator: `(poiId: int, visitorId: NonEmptyString, score: int[1..5])`
    - Kiểm tra: bản ghi có đủ `PoiId`, `VisitorId`, `Score ∈ [1,5]`, `CreatedAt` (UTC), `UpdatedAt` (UTC ≥ CreatedAt)
    - **Validates: Requirements 4.1, 4.4**

- [x] 3. Đăng ký `IRatingRepository` vào DI container
  - Thêm `services.AddScoped<IRatingRepository, RatingRepository>()` vào `Program.cs` của `VinhKhanh.Admin`
  - _Requirements: 1.2, 4.2_

- [x] 4. Tạo DTOs cho Rating API
  - Tạo file `VinhKhanh.Shared/Models/RatingDtos.cs` chứa: `SubmitRatingRequest` (`PoiId`, `Score`), `RatingResponse` (`Id`, `PoiId`, `Score`, `UpdatedAt`), `RatingSummaryResponse` (`AverageRating`, `RatingCount`)
  - _Requirements: 1.1, 1.2, 3.2_

- [x] 5. Tạo `RatingController`
  - [x] 5.1 Tạo `VinhKhanh.Admin/Controllers/RatingController.cs` với 2 endpoint:
    - `POST /api/ratings` — `[Authorize(Roles = "Visitor,Admin")]`: validate `Score ∈ [1,5]` (trả 400 nếu sai), kiểm tra `PoiId` tồn tại (trả 404 nếu không), kiểm tra `IsApproved` của user (trả 403 nếu false), gọi `UpsertAsync`, trả 201 nếu `isNew=true` hoặc 200 nếu cập nhật
    - `GET /api/pois/{poiId:int}/rating-summary` — không yêu cầu auth: gọi `GetSummaryAsync`, trả `RatingSummaryResponse`
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 3.2, 5.3, 5.4_

  - [x] 5.2 Viết property test cho Property 2: Score ngoài [1, 5] luôn bị từ chối
    - **Property 2: Score ngoài [1, 5] luôn bị từ chối**
    - File: `VinhKhanh.Tests/RatingController_Property2_Tests.cs`
    - Generator: `score: int` với filter `score < 1 || score > 5`; dùng InMemory DB với POI hợp lệ
    - Kiểm tra: response.StatusCode == 400
    - **Validates: Requirements 1.4**

  - [x] 5.3 Viết property test cho Property 3: PoiId không tồn tại luôn trả về 404
    - **Property 3: PoiId không tồn tại luôn trả về 404**
    - File: `VinhKhanh.Tests/RatingController_Property3_Tests.cs`
    - Generator: `poiId: int` không có trong DB (dùng InMemory DB rỗng)
    - Kiểm tra: response.StatusCode == 404
    - **Validates: Requirements 1.6**

  - [x] 5.4 Viết property test cho Property 6: Yêu cầu không có JWT hợp lệ bị từ chối
    - **Property 6: Yêu cầu không có JWT hợp lệ bị từ chối**
    - File: `VinhKhanh.Tests/RatingController_Property6_Tests.cs`
    - Generator: các loại token không hợp lệ (null, expired, wrong role) — dùng `ClaimsPrincipal` không có role hợp lệ
    - Kiểm tra: response.StatusCode ∈ {401, 403}
    - **Validates: Requirements 1.5, 5.3**

- [x] 6. Checkpoint — Đảm bảo tất cả tests backend pass
  - Đảm bảo tất cả tests pass, hỏi người dùng nếu có vấn đề phát sinh.

- [x] 7. Mở rộng `SyncResponse` và `Poi` DTO để mang `AverageRating` và `RatingCount`
  - Thêm 2 trường `AverageRating` (double, default 0.0) và `RatingCount` (int, default 0) vào `VinhKhanh.Shared/Models/Poi.cs`
  - Cập nhật `PoiSyncUseCase` (hoặc `PoiRepository.GetSyncPoisAsync`) để populate `AverageRating` và `RatingCount` từ bảng `PoiRatings` khi build `SyncResponse`
  - _Requirements: 3.1, 3.2, 3.3_

- [x] 8. Mở rộng model `POI` mobile và cập nhật `DatabaseService`
  - [x] 8.1 Thêm 2 trường `AverageRating` (double) và `RatingCount` (int) vào `VinhKhanh.Mobile/Models/POI.cs` — không có `[Ignore]` để SQLite lưu được; thêm migration schema tương tự `EnsureSchemaCompatibilityAsync` hiện có
    - _Requirements: 3.1, 3.3_

  - [x] 8.2 Cập nhật `ApplyServerChangesAsync` trong `DatabaseService` để gán `matched.AverageRating` và `matched.RatingCount` từ `remotePoi` khi sync
    - Cập nhật `RemotePoi` private class để có 2 trường mới tương ứng
    - _Requirements: 3.1, 3.3_

  - [x] 8.3 Viết property test cho Property 5: Đồng bộ ghi đè giá trị cũ
    - **Property 5: Đồng bộ ghi đè giá trị cũ**
    - File: `VinhKhanh.Tests/DatabaseService_Property5_Tests.cs`
    - Generator: `(oldRating: double[0..5], newRating: double[0..5], ratingCount: int[0..1000])`
    - Kiểm tra: sau `ApplyServerChanges`, `poi.AverageRating == newRating`
    - Lưu ý: test trực tiếp logic `ApplyServerChangesAsync` bằng cách expose hoặc extract method, hoặc test qua `SyncPoisFromServerAsync` với mock `HttpClient`
    - **Validates: Requirements 3.1, 3.3**

- [x] 9. Tạo `IRatingService` và `RatingService` phía mobile
  - Tạo `VinhKhanh.Mobile/Services/IRatingService.cs` với phương thức `SubmitRatingAsync(int poiId, int score, CancellationToken ct)`
  - Tạo `VinhKhanh.Mobile/Services/RatingService.cs` implement `IRatingService`:
    - Kiểm tra `Connectivity.NetworkAccess` trước khi gọi API; ném `RatingException` nếu không có mạng
    - Gọi `POST /api/ratings` với JWT token từ `SecureStorage`
    - Parse response: 201 → `IsNew=true`, 200 → `IsNew=false`, 4xx/5xx → parse error message
    - Timeout 15 giây
  - Tạo `VinhKhanh.Mobile/Models/RatingResult.cs` và `RatingException.cs`
  - Đăng ký `IRatingService` vào DI trong `MauiProgram.cs`
  - _Requirements: 1.1, 3.4_

- [x] 10. Tạo `StarRatingControl` (XAML Control)
  - [x] 10.1 Tạo `VinhKhanh.Mobile/Controls/StarRatingControl.xaml` và `StarRatingControl.xaml.cs`
    - Bindable properties: `AverageRating` (double), `RatingCount` (int), `UserRating` (int), `IsInteractive` (bool), `Command` (ICommand)
    - Hiển thị 5 ngôi sao; mỗi sao tô màu dựa trên `AverageRating` làm tròn về bội số 0.5 gần nhất
    - Khi `RatingCount == 0` hoặc `AverageRating == 0`: hiển thị label "Chưa có đánh giá"
    - Khi `UserRating > 0`: highlight các sao từ 1 đến `UserRating` theo màu khác biệt
    - Khi `IsInteractive = false`: disable tap gesture
    - Khi `IsInteractive = true` và user tap sao: gọi `Command` với giá trị sao được chọn
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 5.1_

  - [x] 10.2 Implement hàm làm tròn `RoundToHalfStar(double value)` trong `StarRatingControl`
    - Công thức: `Math.Round(value * 2, MidpointRounding.AwayFromZero) / 2.0`
    - _Requirements: 2.1_

  - [x] 10.3 Viết property test cho Property 8: Làm tròn AverageRating về 0.5 sao gần nhất
    - **Property 8: Làm tròn AverageRating về 0.5 sao gần nhất**
    - File: `VinhKhanh.Tests/StarRatingControl_Property8_Tests.cs`
    - Generator: `averageRating: double[0.0..5.0]`
    - Kiểm tra: `result % 0.5 == 0.0` và `Math.Abs(result - averageRating) <= 0.25`
    - Lưu ý: extract `RoundToHalfStar` thành static method hoặc helper class để test độc lập không cần MAUI runtime
    - **Validates: Requirements 2.1**

- [x] 11. Tích hợp `StarRatingControl` vào UI danh sách POI
  - Thêm `StarRatingControl` vào layout hiển thị card POI (tìm trong `MainPage.xaml` hoặc template POI hiện có)
  - Bind `AverageRating`, `RatingCount` từ model `POI`
  - Bind `IsInteractive` theo trạng thái đăng nhập của user (lấy từ `SecureStorage` hoặc ViewModel)
  - Khi user chưa đăng nhập và tap: hiển thị dialog "Vui lòng đăng nhập để đánh giá"
  - Khi user đã đăng nhập và xác nhận chọn sao: gọi `RatingService.SubmitRatingAsync`, cập nhật `UserRating` trên UI
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 5.1, 5.2_

- [x] 12. Tạo EF Core migration cho bảng `PoiRatings`
  - Tạo migration mới trong `VinhKhanh.Infrastructure/Migrations` để tạo bảng `PoiRatings` với đầy đủ constraints đã cấu hình ở Task 1
  - Kiểm tra migration script có đủ: unique index `(PoiId, VisitorId)`, check constraint `Score BETWEEN 1 AND 5`, foreign keys
  - _Requirements: 4.1, 4.3_

- [x] 13. Checkpoint cuối — Đảm bảo tất cả tests pass
  - Đảm bảo tất cả tests pass (property tests + unit tests), hỏi người dùng nếu có vấn đề phát sinh.

## Ghi chú

- Tasks đánh dấu `*` là tùy chọn và có thể bỏ qua để triển khai MVP nhanh hơn
- `AppDbContext` đã có `DbSet<PoiRating>` — chỉ cần tạo entity `PoiRating` trong Domain và thêm cấu hình model
- Property tests dùng **FsCheck.Xunit v3** (đã có trong `VinhKhanh.Tests.csproj`), theo pattern của các test hiện có
- `RoundToHalfStar` nên được extract thành static helper để test độc lập không phụ thuộc MAUI runtime
- Mỗi property test phải có comment header: `// Feature: poi-rating, Property {N}: {mô tả}`
- Schema migration cho SQLite mobile (`AverageRating`, `RatingCount`) cần dùng `ALTER TABLE` tương tự `EnsureSchemaCompatibilityAsync` hiện có trong `DatabaseService`
