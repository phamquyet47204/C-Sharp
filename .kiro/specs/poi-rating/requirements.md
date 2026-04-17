# Tài liệu Yêu cầu: Đánh giá POI theo thang 5 sao

## Giới thiệu

Tính năng này cho phép khách hàng sử dụng ứng dụng mobile VinhKhanhFoodStreet (.NET MAUI) đánh giá các POI (điểm tham quan, địa điểm ăn uống) theo thang điểm 5 sao. Mỗi khách hàng đã đăng nhập có thể gửi một lượt đánh giá cho mỗi POI, chỉnh sửa đánh giá đã gửi, và xem điểm trung bình cùng số lượt đánh giá của từng POI. Hệ thống backend lưu trữ và tổng hợp dữ liệu đánh giá, đồng bộ điểm trung bình về mobile qua cơ chế sync hiện có.

## Bảng thuật ngữ

- **Rating**: Một lượt đánh giá của một Visitor cho một POI, bao gồm điểm số nguyên từ 1 đến 5.
- **RatingService**: Service phía mobile (.NET MAUI) chịu trách nhiệm gửi và lấy đánh giá qua API.
- **RatingController**: Controller phía backend (ASP.NET Core) xử lý các yêu cầu đánh giá từ mobile.
- **RatingRepository**: Repository phía backend lưu trữ và truy vấn dữ liệu đánh giá trong cơ sở dữ liệu.
- **POI**: Điểm tham quan hoặc địa điểm ăn uống được hiển thị trên bản đồ trong ứng dụng.
- **Visitor**: Người dùng đã đăng ký tài khoản với vai trò Visitor trong hệ thống.
- **AverageRating**: Điểm trung bình cộng của tất cả các Rating hợp lệ cho một POI, làm tròn đến 1 chữ số thập phân.
- **RatingCount**: Tổng số lượt đánh giá hợp lệ cho một POI.
- **StarRatingControl**: Thành phần UI trong ứng dụng mobile hiển thị và nhận tương tác đánh giá sao.

---

## Yêu cầu

### Yêu cầu 1: Gửi đánh giá POI

**User Story:** Là một Visitor, tôi muốn đánh giá một POI theo thang 5 sao, để tôi có thể chia sẻ trải nghiệm của mình với cộng đồng.

#### Tiêu chí chấp nhận

1. WHEN một Visitor đã đăng nhập chọn số sao từ 1 đến 5 cho một POI và xác nhận, THE RatingService SHALL gửi yêu cầu đánh giá lên RatingController kèm theo PoiId, điểm số, và token xác thực của Visitor.
2. WHEN RatingController nhận yêu cầu đánh giá hợp lệ từ một Visitor chưa đánh giá POI đó, THE RatingController SHALL lưu Rating mới vào RatingRepository và trả về HTTP 201 kèm thông tin Rating vừa tạo.
3. WHEN một Visitor đã có Rating cho một POI gửi yêu cầu đánh giá mới cho cùng POI đó, THE RatingController SHALL cập nhật Rating hiện có thay vì tạo mới và trả về HTTP 200.
4. IF RatingController nhận yêu cầu đánh giá với điểm số nằm ngoài khoảng [1, 5], THEN THE RatingController SHALL trả về HTTP 400 kèm thông báo lỗi mô tả giá trị hợp lệ.
5. IF RatingController nhận yêu cầu đánh giá mà không có token xác thực hợp lệ, THEN THE RatingController SHALL trả về HTTP 401.
6. IF RatingController nhận yêu cầu đánh giá với PoiId không tồn tại trong hệ thống, THEN THE RatingController SHALL trả về HTTP 404 kèm thông báo lỗi.

---

### Yêu cầu 2: Hiển thị điểm đánh giá trên giao diện mobile

**User Story:** Là một Visitor, tôi muốn xem điểm đánh giá trung bình và số lượt đánh giá của từng POI, để tôi có thể chọn địa điểm phù hợp.

#### Tiêu chí chấp nhận

1. WHEN ứng dụng mobile hiển thị danh sách POI, THE StarRatingControl SHALL hiển thị AverageRating của từng POI dưới dạng biểu tượng sao với độ chính xác 0.5 sao (ví dụ: 3.5 sao, 4.0 sao).
2. WHEN ứng dụng mobile hiển thị danh sách POI, THE StarRatingControl SHALL hiển thị RatingCount kèm theo AverageRating (ví dụ: "4.5 ★ (128)").
3. WHILE AverageRating của một POI bằng 0 hoặc RatingCount bằng 0, THE StarRatingControl SHALL hiển thị trạng thái "Chưa có đánh giá" thay vì hiển thị số 0.
4. WHEN một Visitor đã đánh giá một POI, THE StarRatingControl SHALL hiển thị điểm số mà Visitor đó đã chọn ở trạng thái được chọn (highlighted) để phân biệt với đánh giá của người khác.
5. THE StarRatingControl SHALL cho phép Visitor chạm vào từng ngôi sao để chọn điểm số từ 1 đến 5 trước khi xác nhận gửi.

---

### Yêu cầu 3: Đồng bộ điểm đánh giá về mobile

**User Story:** Là một Visitor, tôi muốn thấy điểm đánh giá cập nhật nhất của các POI sau khi đồng bộ, để thông tin luôn phản ánh đúng thực tế.

#### Tiêu chí chấp nhận

1. WHEN DatabaseService thực hiện đồng bộ POI từ server, THE DatabaseService SHALL cập nhật AverageRating và RatingCount của từng POI trong cơ sở dữ liệu local từ dữ liệu server trả về.
2. THE RatingController SHALL cung cấp endpoint GET `/api/pois/{poiId}/rating-summary` trả về AverageRating và RatingCount hiện tại của POI.
3. WHEN SyncResponse từ server chứa thông tin AverageRating và RatingCount cho một POI, THE DatabaseService SHALL lưu các giá trị này vào bảng POI local thay thế giá trị cũ.
4. IF kết nối mạng không khả dụng khi Visitor cố gắng gửi đánh giá, THEN THE RatingService SHALL thông báo lỗi kết nối cho Visitor và không gửi yêu cầu lên server.

---

### Yêu cầu 4: Lưu trữ và tổng hợp đánh giá phía backend

**User Story:** Là quản trị viên hệ thống, tôi muốn dữ liệu đánh giá được lưu trữ chính xác và tổng hợp tự động, để điểm trung bình luôn phản ánh đúng tất cả lượt đánh giá.

#### Tiêu chí chấp nhận

1. THE RatingRepository SHALL lưu mỗi Rating với các trường: PoiId (int), VisitorId (string), Score (int từ 1 đến 5), CreatedAt (DateTime UTC), UpdatedAt (DateTime UTC).
2. WHEN RatingRepository tính AverageRating cho một POI, THE RatingRepository SHALL tính trung bình cộng của tất cả Score hợp lệ thuộc POI đó và làm tròn đến 1 chữ số thập phân.
3. THE RatingRepository SHALL đảm bảo mỗi cặp (PoiId, VisitorId) chỉ có tối đa một Rating (ràng buộc unique).
4. WHEN một Rating được tạo mới hoặc cập nhật, THE RatingRepository SHALL cập nhật trường UpdatedAt thành thời điểm hiện tại theo UTC.
5. FOR ALL tập hợp Rating hợp lệ của một POI, AverageRating được tính bởi RatingRepository SHALL bằng tổng Score chia cho RatingCount, làm tròn đến 1 chữ số thập phân (tính chất round-trip: tính lại từ dữ liệu gốc phải cho kết quả nhất quán).

---

### Yêu cầu 5: Kiểm soát quyền đánh giá

**User Story:** Là quản trị viên hệ thống, tôi muốn chỉ Visitor đã đăng nhập mới có thể đánh giá POI, để tránh đánh giá giả mạo từ người dùng ẩn danh.

#### Tiêu chí chấp nhận

1. WHILE một người dùng chưa đăng nhập đang xem danh sách POI, THE StarRatingControl SHALL hiển thị điểm đánh giá ở chế độ chỉ đọc và không cho phép tương tác chọn sao.
2. WHEN một người dùng chưa đăng nhập chạm vào StarRatingControl, THE StarRatingControl SHALL hiển thị thông báo yêu cầu đăng nhập để đánh giá.
3. WHERE tính năng xác thực Visitor được bật, THE RatingController SHALL từ chối mọi yêu cầu đánh giá không có JWT token hợp lệ với vai trò Visitor hoặc Admin.
4. IF một tài khoản Visitor bị vô hiệu hóa (IsApproved = false), THEN THE RatingController SHALL trả về HTTP 403 khi tài khoản đó cố gắng gửi đánh giá.
