# Kế hoạch Triển khai: Tài liệu Dự án VinhKhanh (README.md)

## Tổng quan

Tạo file `README.md` hoàn chỉnh ở thư mục gốc dự án, bao gồm 10 nhóm chức năng, Use Case Diagram, ERD, Class Diagram, 8 Sequence Diagram, 5 Activity Diagram và API Reference đầy đủ. Tất cả nội dung bằng tiếng Việt, sơ đồ dùng Mermaid syntax.

## Tasks

- [ ] 1. Tạo phần Header, Mục lục và Tổng quan dự án
  - Tạo file `README.md` mới (ghi đè file hiện có) với tiêu đề dự án và badge
  - Viết mục lục (Table of Contents) với anchor links đến tất cả các section
  - Viết section Tổng quan dự án: mô tả nền tảng, 3 thành phần chính (Backend API, Admin Web UI, Mobile App)
  - Viết section Kiến trúc hệ thống: mô tả Clean Architecture 4 tầng
  - _Requirements: 7.1, 7.3, 7.5_

- [ ] 2. Viết phần Chức năng hệ thống (10 nhóm)
  - [ ] 2.1 Viết nhóm 1–5: Xác thực & Phân quyền, Quản lý POI (Admin), Cổng chủ quán (ShopOwner), Đồng bộ Mobile, Thuyết minh tự động
    - Liệt kê đầy đủ endpoint, mô tả chức năng cho từng nhóm
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_
  - [ ] 2.2 Viết nhóm 6–10: Kiểm soát truy cập, Analytics, AI dịch thuật, QR Code, Đánh giá POI
    - Liệt kê đầy đủ endpoint, mô tả chức năng cho từng nhóm
    - _Requirements: 1.6, 1.7, 1.8, 1.9, 1.10_

- [ ] 3. Vẽ Use Case Diagram (Mermaid flowchart LR)
  - Tạo diagram `flowchart LR` với 3 subgraph tác nhân: Admin, ShopOwner, Visitor/Mobile App
  - Thể hiện đầy đủ 15 use case và kết nối tác nhân → use case
  - Đảm bảo Mermaid syntax hợp lệ (không dùng ký tự đặc biệt trong node label)
  - _Requirements: 2.1, 2.2, 2.3, 2.4_

- [ ] 4. Vẽ ERD (Mermaid erDiagram với 8 entity)
  - Tạo diagram `erDiagram` với 8 entity: ApplicationUser, Poi, PoiLocalization, PoiRating, AnalyticsEvent, Payment, FreeTrialRecord, DeviceTrial
  - Thể hiện đúng quan hệ: Poi 1–N PoiLocalization (CASCADE DELETE), ApplicationUser 1–N Poi, ApplicationUser 1–N Payment, Poi 1–N PoiRating, Poi 1–N FreeTrialRecord, Poi 1–N AnalyticsEvent
  - Liệt kê đầy đủ thuộc tính quan trọng (kiểu dữ liệu, PK, FK) cho từng entity
  - _Requirements: 3.1, 3.2, 3.3_

- [ ] 5. Vẽ Class Diagram (Mermaid classDiagram)
  - Tạo diagram `classDiagram` cho tầng Application/Domain
  - Thể hiện: IPoiRepository, IAnalyticsRepository, AdminApproveUseCase, AnalyticsVisitUseCase, PoiSyncUseCase và các DTO (SyncRequest, SyncResponse, AnalyticsVisitCommand)
  - Thể hiện quan hệ dependency (Use Case → Interface)
  - _Requirements: 3.4_

- [ ] 6. Viết 8 Sequence Diagrams (Mermaid sequenceDiagram)
  - [ ] 6.1 Sequence Diagram: Đăng nhập & Đăng ký
    - Luồng: ShopOwner đăng ký → Admin duyệt → ShopOwner đăng nhập nhận JWT
    - _Requirements: 4.1_
  - [ ] 6.2 Sequence Diagram: Tạo & Duyệt POI
    - Luồng: ShopOwner tạo POI (Draft) → gửi duyệt (Pending) → Admin duyệt/từ chối → cập nhật trạng thái
    - _Requirements: 4.2_
  - [ ] 6.3 Sequence Diagram: Đồng bộ Mobile
    - Luồng: Mobile gửi GET /api/pois/updates → Backend truy vấn DB → trả SyncResponse → Mobile lưu SQLite
    - _Requirements: 4.3_
  - [ ] 6.4 Sequence Diagram: Thuyết minh tự động
    - Luồng: GPS cập nhật → GeofenceEngine tính Haversine → vào vùng POI → NarrationService phát MP3/TTS → ghi AnalyticsEvent
    - _Requirements: 4.4_
  - [ ] 6.5 Sequence Diagram: AI dịch thuật
    - Luồng: Admin/ShopOwner nhập vi → gọi /api/admin/ai/generate → Backend gọi Gemini API → trả en/ja → tự điền form
    - _Requirements: 4.5_
  - [ ] 6.6 Sequence Diagram: Kiểm soát truy cập
    - Luồng: Mobile gọi /api/access/check → Backend kiểm tra FreeTrialRecord + DeviceTrial + Payment → trả trạng thái
    - _Requirements: 4.6_
  - [ ] 6.7 Sequence Diagram: Quét QR
    - Luồng: Mobile quét QR → gọi /api/qr/{token} → Backend tra cứu POI theo QrToken → trả thông tin POI
    - _Requirements: 4.7_
  - [ ] 6.8 Sequence Diagram: Đánh giá POI
    - Luồng: Mobile gửi POST /api/pois/{id}/ratings → Backend upsert PoiRating theo DeviceId → trả điểm trung bình mới
    - _Requirements: 4.8_

- [ ] 7. Viết 5 Activity Diagrams (Mermaid flowchart TD)
  - [ ] 7.1 Activity Diagram: Vòng đời POI
    - Luồng: Draft → gửi duyệt → Pending_Approval → [Duyệt → Approved | Từ chối → Rejected] → [Ẩn → Hidden]
    - _Requirements: 5.1_
  - [ ] 7.2 Activity Diagram: GeofenceEngine
    - Luồng: nhận GPS → tính khoảng cách Haversine → kiểm tra debounce (≥2 lần) → kiểm tra cooldown → kiểm tra priority → kích hoạt OnPoiEntered
    - _Requirements: 5.2_
  - [ ] 7.3 Activity Diagram: Kiểm soát truy cập Visitor
    - Luồng: kiểm tra Access Pass → [còn hạn → cho phép] → kiểm tra DeviceTrial → [còn hạn → cho phép] → kiểm tra FreeTrialRecord (≤3 POI) → [còn quota → cho phép] → yêu cầu mua pass
    - _Requirements: 5.3_
  - [ ] 7.4 Activity Diagram: NarrationService
    - Luồng: nhận POI → kiểm tra AudioPath → [có MP3 → PlayAudioAsync] / [không có → TextToSpeech] → audio ducking → ghi AnalyticsEvent
    - _Requirements: 5.4_
  - [ ] 7.5 Activity Diagram: Đồng bộ dữ liệu Mobile
    - Luồng: khởi động app → gọi sync API → [thành công → lưu SQLite → khởi động GeofenceEngine] / [thất bại → dùng cache cũ]
    - _Requirements: 5.5_

- [ ] 8. Viết API Reference (4 nhóm + ví dụ JSON)
  - [ ] 8.1 Auth API và Admin API
    - Liệt kê đầy đủ endpoint /api/auth/* và /api/admin/* với method, path, auth requirement, mô tả
    - Cung cấp ví dụ JSON request/response cho endpoint login
    - _Requirements: 6.1, 6.2, 6.5_
  - [ ] 8.2 Shop API và Mobile API
    - Liệt kê đầy đủ endpoint /api/shop/* và /api/pois/*, /api/analytics/*, /api/access/*, /api/qr/*, /api/payments/*
    - Cung cấp ví dụ JSON request/response cho sync POI, analytics visit, access check
    - _Requirements: 6.3, 6.4, 6.5_

- [ ] 9. Checkpoint cuối — Kiểm tra README.md hoàn chỉnh
  - Đảm bảo tất cả section đã có mặt trong file README.md
  - Kiểm tra Mermaid syntax không có ký tự đặc biệt gây lỗi render
  - Kiểm tra mục lục có anchor links đúng với các heading
  - Đảm bảo file encoding UTF-8, nội dung tiếng Việt hiển thị đúng
  - Hỏi người dùng nếu có vấn đề cần làm rõ.

## Ghi chú

- Tasks đánh dấu `*` là tùy chọn, có thể bỏ qua để tạo MVP nhanh hơn
- Tất cả nội dung ghi vào file `README.md` ở thư mục gốc (ghi đè file hiện có)
- Mermaid syntax: label có dấu ngoặc đơn dùng `["text (detail)"]`, tên node chỉ dùng chữ cái/số/gạch dưới
- Mỗi task xây dựng tiếp nối task trước — không có code/nội dung bị bỏ lơ
