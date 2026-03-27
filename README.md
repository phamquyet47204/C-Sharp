# Báo Cáo Tiến Độ Dự Án VinhKhanhFoodStreet

## 1. Mục tiêu sản phẩm
Dự án xây dựng hệ sinh thái du lịch ẩm thực Vĩnh Khánh gồm 2 khối chính:
1. Ứng dụng Mobile bằng .NET MAUI:
- Theo dõi vị trí thời gian thực.
- Kích hoạt thuyết minh theo vùng địa lý (geofence).
- Phát nội dung bằng file thu sẵn hoặc TTS.
2. Hệ thống Web Admin bằng ASP.NET Core:
- Quản trị POI, nội dung, bán kính, ưu tiên.
- Hỗ trợ vận hành và phân tích hành vi người dùng ẩn danh.

## 2. Kiến trúc tổng thể hiện tại
### 2.1 Mobile App (.NET MAUI)
- UI Layer:
  - MainPage.xaml: giao diện bản đồ, danh sách quán, tìm kiếm, bộ lọc.
  - MainPage.xaml.cs: điều phối tương tác UI, tải dữ liệu, chuyển ngôn ngữ, phát âm thanh.
- Service Layer:
  - DatabaseService: CRUD và truy vấn POI từ SQLite.
  - LocationService: theo dõi vị trí, phát sự kiện khi thay đổi tọa độ.
  - GeofenceEngine: xử lý vào/ra vùng bằng Haversine + debounce + cooldown.
  - NarrationService: điều phối phát âm thanh file/TTS, audio ducking.
- Data Layer:
  - SQLite cục bộ, model POI lưu thông tin điểm và nội dung.

### 2.2 Nguyên tắc kỹ thuật
- Dependency Injection để tách lớp rõ ràng.
- Async/await để tránh chặn UI thread.
- Tách interface cho service nhằm dễ kiểm thử và thay thế triển khai.
- Ưu tiên ổn định runtime trước, sau đó nâng cấp UI/UX.

## 3. Luồng hoạt động chi tiết
### 3.1 Luồng khởi động
1. MauiProgram đăng ký các service vào DI.
2. MainPage được khởi tạo qua DI.
3. MainPage đăng ký NarrationPlayer cho NarrationService.
4. OnAppearing thực hiện:
- Xin quyền vị trí.
- Tải dữ liệu POI.
- Render pin bản đồ + danh sách.
- Khởi động GeofenceEngine.

### 3.2 Luồng tải danh sách quán ăn từ POI (đã hoàn thiện thêm)
Vấn đề cũ:
- Danh sách có thể thiếu hoặc không đồng bộ theo ngôn ngữ.

Giải pháp đã triển khai:
1. Tải toàn bộ POI từ DatabaseService.
2. Gom nhóm theo cụm quán (tọa độ làm tròn + category).
3. Chọn 1 bản ghi hiển thị theo thứ tự ưu tiên ngôn ngữ:
- Ngôn ngữ hiện tại người dùng chọn.
- Fallback sang en.
- Fallback sang vi.
- Nếu vẫn không có, lấy bản ghi ưu tiên cao nhất.
4. Sắp xếp lại theo Priority giảm dần.
5. Cập nhật map pin và danh sách từ tập dữ liệu đã chuẩn hóa.

Kết quả:
- Danh sách hiển thị ổn định hơn.
- Giảm rủi ro “đã thêm POI nhưng chưa thấy lên danh sách”.
- Dễ mở rộng khi bổ sung ngôn ngữ mới.

### 3.3 Luồng geofence và trigger nội dung
1. LocationService phát LocationChanged khi vị trí thay đổi đủ điều kiện.
2. GeofenceEngine nhận tọa độ mới, tính khoảng cách bằng Haversine.
3. Áp dụng debounce để tránh nhiễu GPS.
4. Áp dụng cooldown để chống phát lặp.
5. Khi vào vùng hợp lệ, MainPage nhận OnPoiEntered và gọi NarrationService.

### 3.4 Luồng âm thanh
1. Ưu tiên phát file audio nếu POI có AudioPath.
2. Nếu không có file, fallback sang TTS theo ngôn ngữ POI.
3. NarrationService bảo đảm không chồng nhiều luồng phát cùng lúc.
4. Trên Android có audio ducking để giảm âm ứng dụng nền.

### 3.5 Luồng đổi ngôn ngữ
1. Người dùng bấm nút ngôn ngữ trên header.
2. MainPage đổi mã ngôn ngữ vi/en/ja.
3. Gọi GeofenceEngine.SetLanguageAsync để đồng bộ engine.
4. Tải lại dữ liệu POI và render lại map/list theo ngôn ngữ mới.

## 4. Đánh giá tiến độ hiện tại
| Phân hệ | Tiến độ | Trạng thái |
|---|---:|---|
| Kiến trúc & dữ liệu | 92% | DI + SQLite ổn định, dữ liệu mẫu đa ngôn ngữ có sẵn |
| Location & Geofence | 95% | Đã vận hành, có debounce/cooldown |
| Audio/Narration | 78% | Đã phát file + TTS, đã xử lý đăng ký player |
| UI/UX & bản đồ | 60% | Đã tách map/list, tìm kiếm kính lúp, lọc danh mục |
| Web Admin/API | 0% | Chưa triển khai |
| MVP mở rộng (QR, payment, offline rule) | 10% | Mới ở mức kế hoạch |

## 5. Các tồn đọng quan trọng cần xử lý tiếp
### 5.1 Danh sách quán ăn
Đã cải thiện mạnh phần tải dữ liệu, nhưng vẫn cần:
1. Thêm màn debug nội bộ để so sánh số lượng:
- Tổng POI trong DB.
- Sau chuẩn hóa ngôn ngữ.
- Sau lọc category.
- Sau lọc search text.
2. Thêm thao tác Refresh dữ liệu thủ công trên UI để kiểm chứng nhanh sau khi thêm POI mới.
3. Chuẩn hóa quy tắc định danh quán (group key) để tránh trùng/thiếu khi tọa độ quá sát.

### 5.2 Đa ngôn ngữ
Đã đồng bộ lại luồng đổi ngôn ngữ, nhưng cần hoàn thiện thêm:
1. Kiểm tra độ phủ nội dung cho từng POI ở vi/en/ja.
2. Chuẩn hóa fallback ở mọi lớp (UI list, geofence, narration).
3. Đồng bộ ngôn ngữ cho toàn bộ text tĩnh trên UI.

### 5.3 UI/UX
1. Nâng cấp bottom sheet chuẩn sản phẩm.
2. Thêm mini player có trạng thái đang phát/tạm dừng.
3. Custom pin theo loại quán trên bản đồ.
4. Popup thông tin nhanh khi chạm pin.

## 6. Build, đóng gói và triển khai Android
### 6.1 Trạng thái build
- Build Android Release thành công.
- Không có lỗi chặn build.

### 6.2 Trạng thái đóng gói/cài đặt
- APK đã được xuất và cài lại lên emulator Android Studio.
- Package xác nhận tồn tại trên thiết bị:
  - com.companyname.vinhkhanhfoodstreet

## 7. Hướng dẫn thao tác nhanh cho team
### 7.1 Build Android
- dotnet build -f net10.0-android -c Release

### 7.2 Xuất APK
- dotnet publish -f net10.0-android -c Release -p:AndroidPackageFormat=apk -p:AndroidKeyStore=false

### 7.3 Cài APK lên emulator
- adb install -r đường_dẫn_đến_file_apk

## 8. Kế hoạch thực hiện 2 sprint gần nhất
### Sprint A (ưu tiên cao)
1. Hoàn thiện kiểm chứng danh sách quán bằng bộ log đối chiếu.
2. Chuẩn hóa i18n cho toàn bộ UI và dữ liệu.
3. Hoàn thiện mini player + bottom sheet.

### Sprint B
1. Scaffold ASP.NET Core Web API quản trị POI.
2. Thiết kế cơ sở dữ liệu server cho CMS.
3. Tạo Admin Dashboard bản đầu.

## 9. Rủi ro kỹ thuật và hướng giảm thiểu
1. Nhiễu GPS đô thị gây trigger sai:
- Giảm bằng debounce, cooldown, tối ưu radius theo khu vực.
2. Mất đồng bộ dữ liệu đa ngôn ngữ:
- Giảm bằng fallback rõ ràng + kiểm thử độ phủ theo POI.
3. Audio không ổn định giữa thiết bị:
- Giảm bằng fallback TTS và logging chi tiết từng phiên phát.

## 10. Kết luận
Dự án đã đạt nền tảng kỹ thuật tốt cho lõi Mobile: dữ liệu cục bộ, geofence, và phát thuyết minh theo vị trí. Phần tải danh sách quán ăn từ POI đã được nâng cấp theo hướng có fallback ngôn ngữ và đồng bộ lại khi đổi ngôn ngữ. Bước tiếp theo nên tập trung hoàn thiện trải nghiệm UI/UX chuyên nghiệp và triển khai Web Admin để đưa hệ thống vào vận hành thực tế.
