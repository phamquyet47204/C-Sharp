# Tài liệu Yêu cầu — Tài liệu Triển khai Dự án VinhKhanh

## Giới thiệu

Dự án VinhKhanh là nền tảng du lịch ẩm thực thông minh (Food Street / POI — Point of Interest) gồm ba thành phần chính: Backend API (ASP.NET Core), Admin Web UI (React), và Mobile App (.NET MAUI Android). Tài liệu này mô tả yêu cầu cho việc tạo một tài liệu triển khai chi tiết (README.md) bao gồm đầy đủ chức năng hệ thống, sơ đồ Use Case, Class/ERD, Sequence Diagram và Activity Diagram cho từng chức năng chính, viết bằng tiếng Việt, sử dụng Mermaid cho tất cả sơ đồ.

---

## Bảng thuật ngữ (Glossary)

- **POI** (Point of Interest): Điểm tham quan / quán ăn được đăng ký trên hệ thống.
- **PoiStatus**: Trạng thái vòng đời của POI: `Draft → Pending_Approval → Approved / Rejected / Hidden`.
- **ShopOwner**: Chủ quán — người dùng có role `ShopOwner`, cần được Admin duyệt trước khi sử dụng.
- **Admin**: Quản trị viên hệ thống — có toàn quyền quản lý POI, người dùng, analytics.
- **Visitor**: Du khách — người dùng mobile, có thể dùng thử miễn phí 3 POI hoặc mua Access Pass.
- **DeviceTrial**: Bản ghi thử nghiệm 7 ngày gắn với DeviceId của thiết bị.
- **FreeTrialRecord**: Bản ghi lần đầu nghe thuyết minh một POI cụ thể (giới hạn 3 POI miễn phí).
- **AccessPass**: Gói trả phí 7 ngày cho phép Visitor nghe không giới hạn.
- **GeofenceEngine**: Dịch vụ mobile phát hiện khi người dùng bước vào vùng bán kính của POI.
- **NarrationService**: Dịch vụ mobile phát thuyết minh (MP3 hoặc TTS) khi vào vùng POI.
- **GeminiAiService**: Dịch vụ backend gọi Google Gemini API để dịch nội dung vi → en, ja.
- **QrToken**: Mã token duy nhất gắn với POI, dùng để tra cứu thông tin qua QR code.
- **AnalyticsEvent**: Sự kiện ghi nhận lượt xem (`visit`) hoặc lượt nghe thuyết minh (`narration`).
- **SyncRequest / SyncResponse**: DTO dùng chung giữa Backend và Mobile để đồng bộ danh sách POI.
- **README_Document**: Tài liệu README.md ở thư mục gốc dự án — đầu ra chính của spec này.

---

## Yêu cầu

### Yêu cầu 1: Liệt kê đầy đủ chức năng hệ thống

**User Story:** Là một developer hoặc stakeholder, tôi muốn xem danh sách đầy đủ các chức năng của hệ thống VinhKhanh, để có thể hiểu phạm vi và khả năng của toàn bộ nền tảng.

#### Tiêu chí chấp nhận

1. THE README_Document SHALL liệt kê tất cả chức năng thuộc nhóm **Xác thực & Phân quyền**: đăng nhập JWT, đăng ký ShopOwner (chờ duyệt), đăng ký Visitor (tự động kích hoạt), phân quyền 3 role (Admin / ShopOwner / Visitor).
2. THE README_Document SHALL liệt kê tất cả chức năng thuộc nhóm **Quản lý POI (Admin)**: xem danh sách, tạo mới, cập nhật, duyệt, từ chối (kèm lý do), ẩn POI.
3. THE README_Document SHALL liệt kê tất cả chức năng thuộc nhóm **Cổng chủ quán (ShopOwner)**: tạo/sửa/xóa POI của mình, gửi duyệt, xem thống kê 30 ngày.
4. THE README_Document SHALL liệt kê tất cả chức năng thuộc nhóm **Đồng bộ Mobile**: sync POI theo timestamp, hỗ trợ audio/text-only mode.
5. THE README_Document SHALL liệt kê tất cả chức năng thuộc nhóm **Thuyết minh tự động**: GeofenceEngine (Haversine, debounce, cooldown, priority), NarrationService (MP3 + TTS fallback), audio ducking Android.
6. THE README_Document SHALL liệt kê tất cả chức năng thuộc nhóm **Kiểm soát truy cập**: Free Trial 3 POI, DeviceTrial 7 ngày, Access Pass mua thêm.
7. THE README_Document SHALL liệt kê tất cả chức năng thuộc nhóm **Analytics**: ghi nhận visit/narration, heatmap, content performance, dashboard summary.
8. THE README_Document SHALL liệt kê tất cả chức năng thuộc nhóm **AI dịch thuật**: gọi Gemini API dịch vi → en, ja cho Admin và ShopOwner.
9. THE README_Document SHALL liệt kê tất cả chức năng thuộc nhóm **QR Code**: tạo QrToken, tra cứu POI qua token, hiển thị link QR.
10. THE README_Document SHALL liệt kê tất cả chức năng thuộc nhóm **Đánh giá POI**: gửi/cập nhật rating (1–5 sao) theo DeviceId, xem điểm trung bình.

---

### Yêu cầu 2: Use Case Diagram

**User Story:** Là một developer hoặc BA, tôi muốn xem Use Case Diagram của hệ thống, để hiểu các tác nhân và tương tác chính.

#### Tiêu chí chấp nhận

1. THE README_Document SHALL chứa một Use Case Diagram dạng Mermaid (`flowchart` hoặc `graph`) thể hiện đầy đủ 3 tác nhân: Admin, ShopOwner, Visitor/Mobile App.
2. THE README_Document SHALL thể hiện trong Use Case Diagram tất cả use case của Admin: quản lý POI, duyệt/từ chối POI, duyệt ShopOwner, xem analytics, tạo QR.
3. THE README_Document SHALL thể hiện trong Use Case Diagram tất cả use case của ShopOwner: đăng ký, tạo/sửa/xóa POI, gửi duyệt, dùng AI dịch, xem thống kê.
4. THE README_Document SHALL thể hiện trong Use Case Diagram tất cả use case của Visitor: đồng bộ POI, nghe thuyết minh tự động, đánh giá POI, mua Access Pass, quét QR.

---

### Yêu cầu 3: Class Diagram / ERD

**User Story:** Là một developer, tôi muốn xem Class Diagram và ERD của hệ thống, để hiểu cấu trúc dữ liệu và quan hệ giữa các entity.

#### Tiêu chí chấp nhận

1. THE README_Document SHALL chứa một ERD dạng Mermaid (`erDiagram`) thể hiện tất cả 8 entity chính: `ApplicationUser`, `Poi`, `PoiLocalization`, `PoiRating`, `AnalyticsEvent`, `Payment`, `FreeTrialRecord`, `DeviceTrial`.
2. WHEN vẽ ERD, THE README_Document SHALL thể hiện đúng quan hệ: `Poi` 1–N `PoiLocalization` (CASCADE DELETE), `Poi` N–1 `ApplicationUser` (OwnerId, nullable), `Payment` N–1 `ApplicationUser`, `PoiRating` N–1 `Poi`, `FreeTrialRecord` N–1 `Poi`.
3. THE README_Document SHALL liệt kê đầy đủ các thuộc tính quan trọng của từng entity trong ERD (kiểu dữ liệu, khóa chính, khóa ngoại).
4. THE README_Document SHALL chứa một Class Diagram dạng Mermaid (`classDiagram`) cho tầng Application/Domain thể hiện các Use Case class và interface: `IPoiRepository`, `IAnalyticsRepository`, `AdminApproveUseCase`, `AnalyticsVisitUseCase`, `PoiSyncUseCase`.

---

### Yêu cầu 4: Sequence Diagram cho từng chức năng chính

**User Story:** Là một developer, tôi muốn xem Sequence Diagram cho từng luồng nghiệp vụ chính, để hiểu thứ tự tương tác giữa các thành phần.

#### Tiêu chí chấp nhận

1. THE README_Document SHALL chứa Sequence Diagram (Mermaid `sequenceDiagram`) cho luồng **Đăng nhập & Đăng ký**: ShopOwner đăng ký → Admin duyệt → ShopOwner đăng nhập nhận JWT.
2. THE README_Document SHALL chứa Sequence Diagram cho luồng **Tạo & Duyệt POI**: ShopOwner tạo POI (Draft) → gửi duyệt (Pending) → Admin duyệt/từ chối → cập nhật trạng thái.
3. THE README_Document SHALL chứa Sequence Diagram cho luồng **Đồng bộ Mobile**: Mobile gửi `GET /api/pois/updates?lastSync=...` → Backend truy vấn DB → trả SyncResponse → Mobile lưu SQLite.
4. THE README_Document SHALL chứa Sequence Diagram cho luồng **Thuyết minh tự động**: GPS cập nhật → GeofenceEngine tính Haversine → vào vùng POI → NarrationService phát MP3/TTS → ghi AnalyticsEvent.
5. THE README_Document SHALL chứa Sequence Diagram cho luồng **AI dịch thuật**: Admin/ShopOwner nhập vi → gọi `/api/admin/ai/generate` → Backend gọi Gemini API → trả en/ja → tự điền form.
6. THE README_Document SHALL chứa Sequence Diagram cho luồng **Kiểm soát truy cập**: Mobile gọi `/api/access/check` → Backend kiểm tra FreeTrialRecord + DeviceTrial + Payment → trả trạng thái.
7. THE README_Document SHALL chứa Sequence Diagram cho luồng **Quét QR**: Mobile quét QR → gọi `/api/qr/{token}` → Backend tra cứu POI theo QrToken → trả thông tin POI.
8. THE README_Document SHALL chứa Sequence Diagram cho luồng **Đánh giá POI**: Mobile gửi `POST /api/pois/{id}/ratings` → Backend upsert PoiRating theo DeviceId → trả điểm trung bình mới.

---

### Yêu cầu 5: Activity Diagram cho từng chức năng chính

**User Story:** Là một developer hoặc BA, tôi muốn xem Activity Diagram cho từng luồng nghiệp vụ, để hiểu logic rẽ nhánh và điều kiện trong từng chức năng.

#### Tiêu chí chấp nhận

1. THE README_Document SHALL chứa Activity Diagram (Mermaid `flowchart TD`) cho luồng **Vòng đời POI**: Draft → gửi duyệt → Pending_Approval → [Duyệt → Approved | Từ chối → Rejected] → [Ẩn → Hidden].
2. THE README_Document SHALL chứa Activity Diagram cho luồng **GeofenceEngine**: nhận GPS → tính khoảng cách Haversine → kiểm tra debounce (≥2 lần) → kiểm tra cooldown → kiểm tra priority → kích hoạt OnPoiEntered.
3. THE README_Document SHALL chứa Activity Diagram cho luồng **Kiểm soát truy cập Visitor**: kiểm tra Access Pass → [còn hạn → cho phép] → kiểm tra DeviceTrial → [còn hạn → cho phép] → kiểm tra FreeTrialRecord (≤3 POI) → [còn quota → cho phép] → yêu cầu mua pass.
4. THE README_Document SHALL chứa Activity Diagram cho luồng **NarrationService**: nhận POI → kiểm tra AudioPath → [có MP3 → PlayAudioAsync] / [không có → TextToSpeech] → audio ducking → ghi AnalyticsEvent.
5. THE README_Document SHALL chứa Activity Diagram cho luồng **Đồng bộ dữ liệu Mobile**: khởi động app → gọi sync API → [thành công → lưu SQLite → khởi động GeofenceEngine] / [thất bại → dùng cache cũ].

---

### Yêu cầu 6: Tài liệu API Reference đầy đủ

**User Story:** Là một developer tích hợp, tôi muốn xem tài liệu API đầy đủ, để biết endpoint nào cần gọi và với tham số gì.

#### Tiêu chí chấp nhận

1. THE README_Document SHALL liệt kê đầy đủ tất cả endpoint của **Auth API** (`/api/auth/*`) với method, path, auth requirement và mô tả.
2. THE README_Document SHALL liệt kê đầy đủ tất cả endpoint của **Admin API** (`/api/admin/*`) với method, path, auth requirement và mô tả.
3. THE README_Document SHALL liệt kê đầy đủ tất cả endpoint của **Shop API** (`/api/shop/*`) với method, path, auth requirement và mô tả.
4. THE README_Document SHALL liệt kê đầy đủ tất cả endpoint của **Mobile API** (`/api/pois/*`, `/api/analytics/*`, `/api/access/*`, `/api/qr/*`, `/api/pois/{id}/ratings`) với method, path, auth requirement và mô tả.
5. WHEN mô tả endpoint, THE README_Document SHALL cung cấp ví dụ request/response JSON cho ít nhất các endpoint quan trọng: login, sync POI, analytics visit, access check.

---

### Yêu cầu 7: Định dạng và chất lượng tài liệu

**User Story:** Là một người đọc tài liệu, tôi muốn tài liệu được trình bày rõ ràng, nhất quán và dễ điều hướng, để có thể tìm thông tin nhanh chóng.

**User Story:** Là một người đọc tài liệu, tôi muốn tài liệu được trình bày rõ ràng, nhất quán và dễ điều hướng, để có thể tìm thông tin nhanh chóng.

#### Tiêu chí chấp nhận

1. THE README_Document SHALL sử dụng tiếng Việt làm ngôn ngữ chính cho toàn bộ nội dung mô tả.
2. THE README_Document SHALL sử dụng Mermaid syntax cho tất cả sơ đồ (Use Case, ERD, Class Diagram, Sequence Diagram, Activity Diagram).
3. THE README_Document SHALL có mục lục (Table of Contents) với liên kết anchor đến từng phần chính.
4. WHEN viết sơ đồ Mermaid, THE README_Document SHALL đảm bảo syntax hợp lệ, không có ký tự đặc biệt gây lỗi render (dấu ngoặc đơn trong label phải được escape hoặc dùng dấu ngoặc kép).
5. THE README_Document SHALL ghi đè (overwrite) file `README.md` ở thư mục gốc của dự án.
