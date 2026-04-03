# VinhKhanh Food Street — Tài liệu dự án

## Mục lục
1. [Tổng quan](#1-tổng-quan)
2. [Kiến trúc hệ thống](#2-kiến-trúc-hệ-thống)
3. [Cách chạy app](#3-cách-chạy-app)
4. [Luồng hoạt động](#4-luồng-hoạt-động)
5. [Cấu trúc thư mục](#5-cấu-trúc-thư-mục)
6. [Tiến độ & tồn đọng](#6-tiến-độ--tồn-đọng)
7. [Lịch sử lỗi & bản vá](#7-lịch-sử-lỗi--bản-vá)

---

## 1. Tổng quan

Hệ sinh thái du lịch ẩm thực phố Vĩnh Khánh, Quận 4, TP.HCM gồm 2 khối:

| Khối | Công nghệ | Mô tả |
|---|---|---|
| Mobile App | .NET 10 MAUI | Bản đồ, geofence, thuyết minh tự động theo vị trí |
| Web Admin | ASP.NET Core + React | Quản trị POI, nội dung, media |

Repo đang dùng mô hình mono-repo. Mobile chạy từ project gốc VinhKhanhFoodStreet.csproj, còn phần backend/admin nằm trong các thư mục VinhKhanh.Admin, VinhKhanh.Application, VinhKhanh.Domain, VinhKhanh.Infrastructure và VinhKhanh.Shared.

---

## 2. Kiến trúc hệ thống

```
VinhKhanh.Mobile          ← MAUI Android/iOS
VinhKhanh.Admin           ← ASP.NET Core Web API
VinhKhanh.Admin.Ui        ← React + Tailwind
VinhKhanh.Application     ← Use cases
VinhKhanh.Domain          ← Entities, interfaces
VinhKhanh.Infrastructure  ← EF Core, repositories
VinhKhanh.Shared          ← Models dùng chung
```

### Mobile — các lớp chính

```
MauiProgram.cs            ← DI container, cấu hình services
App.xaml.cs               ← Entry point, NavigationPage
Views/MapPage             ← UI bản đồ full-screen
ViewModels/MapViewModel   ← State, filter, sync logic
Services/
  LocalDatabase           ← SQLite CRUD
  SyncService             ← Đồng bộ POI từ API
  GeofenceService         ← Theo dõi vào/ra vùng
  NarrationEngine         ← Phát audio/TTS
Platforms/Android/
  MainActivity.cs         ← Android entry point
  MapUiCustomizer.cs      ← Tắt controls mặc định Google Maps

### Dữ liệu hình ảnh

- Backend admin lưu ảnh POI dưới dạng đường dẫn media như /media/ten_anh.jpg.
- Mobile sync sẽ chuyển đường dẫn này sang URL phù hợp cho emulator hoặc máy thật.
- Nếu dữ liệu local cũ đang giữ placeholder dotnet_bot.png hoặc file:///media/... thì cần sync lại hoặc xóa cache app để lấy ảnh mới.
```

---

## 3. Cách chạy app

### Yêu cầu môi trường

| Công cụ | Phiên bản |
|---|---|
| .NET SDK | 10.0+ |
| Android SDK | API 35+ |
| Android Emulator | API 35, x86_64 |
| Visual Studio / Rider | Có MAUI workload |
| Node.js | Dùng cho VinhKhanh.Admin.Ui |

Lưu ý khi chạy trên Android emulator:
- Backend local nên chạy ở http://localhost:5000.
- Emulator không đọc localhost trực tiếp, app sẽ tự đổi sang http://10.0.2.2:5000.

### 3.1 Chạy Admin API (backend)

```bash
cd VinhKhanhFoodStreet/VinhKhanh.Admin
dotnet run
# API chạy tại http://localhost:5000
```

### 3.2 Chạy Admin UI (frontend)

```bash
cd VinhKhanhFoodStreet/VinhKhanh.Admin.Ui
npm install
npm run dev
```

### 3.3 Build Mobile Android

```bash
cd VinhKhanhFoodStreet

dotnet build VinhKhanhFoodStreet.csproj -f net10.0-android -c Debug
```

APK output: `bin/Debug/net10.0-android/com.companyname.vinhkhanhfoodstreet-Signed.apk`

### 3.4 Deploy lên emulator (script tự động)

Chạy file `reinstall.bat` ở thư mục gốc:

```bat
reinstall.bat
```

Script thực hiện tuần tự:
1. Force stop app cũ
2. Xóa toàn bộ data/cache (`pm clear`)
3. Gỡ cài đặt (`uninstall`)
4. Cài APK mới (`install`)
5. Xác nhận package tồn tại
6. Launch app

Nếu muốn cài thủ công sau khi build xong, dùng:

```powershell
$adb = "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"
$apk = "C:\Users\phamq\Downloads\C Sharp\VinhKhanhFoodStreet\bin\Debug\net10.0-android\com.companyname.vinhkhanhfoodstreet-Signed.apk"
& $adb install -r $apk
& $adb shell monkey -p com.companyname.vinhkhanhfoodstreet -c android.intent.category.LAUNCHER 1
```

### 3.5 Deploy thủ công

```bat
set ADB=C:\Users\<user>\AppData\Local\Android\Sdk\platform-tools\adb.exe
set APK=VinhKhanh.Mobile\bin\Debug\net9.0-android\com.vinhkhanh.mobile-Signed.apk

# Xóa data sạch
%ADB% -s emulator-5554 shell am force-stop com.vinhkhanh.mobile
%ADB% -s emulator-5554 shell pm clear com.vinhkhanh.mobile
%ADB% -s emulator-5554 uninstall com.vinhkhanh.mobile

# Cài mới
%ADB% -s emulator-5554 install "%APK%"

# Launch
%ADB% -s emulator-5554 shell monkey -p com.vinhkhanh.mobile -c android.intent.category.LAUNCHER 1
```

### 3.6 Xem crash log

```bat
%ADB% -s emulator-5554 logcat -d AndroidRuntime:E DOTNET:E *:S
```

### Thông tin package

| Thông tin | Giá trị |
|---|---|
| Package ID | `com.companyname.vinhkhanhfoodstreet` |
| API backend (emulator) | `http://10.0.2.2:5000` |
| API backend (localhost) | `http://localhost:5000` |
| DB local | `AppDataDirectory/vinhkhanh_foodstreet.db3` |

---

## 4. Luồng hoạt động

### 4.1 Khởi động app

```
App.xaml.cs
  └─ MainPage = NavigationPage(MapPage)

MauiProgram.CreateMauiApp()
  ├─ Đăng ký DI: LocalDatabase, SyncService, GeofenceService,
  │              NarrationEngine, MapViewModel, MapPage
  └─ UseMauiMaps() — khởi tạo bản đồ

MapPage.OnAppearing
  ├─ MapViewModel.LoadCommand → SyncAndReloadAsync
  │    ├─ SyncService.SyncIfConnectedAsync() → GET api/pois/updates
  │    └─ LocalDatabase.GetActivePoisAsync() → đọc SQLite
  ├─ PlacePins(FilteredPois) → đặt pin lên bản đồ
  ├─ Load danh sách POI theo category dropdown
  ├─ Hiển thị ảnh POI qua ImagePath/PoiImageSource
  └─ StartMonitoring() → GeofenceService.Start() + tự đồng bộ theo chu kỳ
```

### 4.2 Đồng bộ dữ liệu POI

```
SyncService.SyncIfConnectedAsync()
  ├─ Kiểm tra kết nối mạng
  ├─ Kiểm tra dung lượng trống
  │    └─ < 200MB → textOnlyMode = true (không sync audio)
  ├─ GET api/pois/updates?lastSync=...&includeAudio=...
  ├─ MapToLocalPois() — chuyển Poi → PoiRecord (mỗi ngôn ngữ 1 record)
  ├─ Normalize media path — đổi /media/... hoặc file:///media/... sang URL backend
  ├─ UpsertPoisAsync() — lưu/cập nhật SQLite
  ├─ DeletePoisAsync() — xóa POI đã bị xóa trên server
  └─ SaveLastSyncTime() — lưu timestamp sync cuối
```

### 4.3 Lọc POI theo loại quán

```
MapViewModel
  ├─ Pois (toàn bộ từ DB)
  ├─ Categories (tự build từ Pois.Category, luôn có "Tất cả" đầu)
  ├─ SelectedCategory (bind với Picker trên UI)
  └─ FilteredPois = Pois nếu "Tất cả", ngược lại filter theo Category

MapPage ← lắng nghe FilteredPois thay đổi → PlacePins()
```

### 4.4 Hiển thị thông tin POI (tap pin)

```
MapPage.OnMapClicked(tọa độ tap)
  ├─ Tìm pin gần nhất trong vòng ~40m
  └─ ShowPoiCard(poi)
       ├─ Hiển thị tên, mô tả, category
       ├─ Load ảnh từ poi.ImagePath
       │    ├─ URL (http/https) → ImageSource.FromUri
       │    ├─ /media/... hoặc file:///media/... → đổi sang URL backend
       │    └─ File local → ImageSource.FromFile
       └─ PoiCard.IsVisible = true (card trượt lên từ dưới)
```

### 4.5 Hiển thị danh sách POI

```
MainPage.OnAppearing / OnToggleLanguage / OnSearchPoi
  ├─ LoadMapPinsAndListAsync(reloadFromDatabase: true)
  ├─ GetLocalizedPoisAsync() — chọn bản ghi theo ngôn ngữ hiện tại
  ├─ CreateDisplayPoi() — tạo item cho list UI
  ├─ CategoryPicker — lọc theo nhóm quán
  └─ PoiCollectionView — hiển thị thumbnail, tên, mô tả và nút nghe/đi đường
```

### 4.6 Luồng ảnh trên mobile

```
Admin upload ảnh
  └─ lưu /media/ten_anh.jpg trong wwwroot

PoiSyncUseCase / PoisController
  └─ trả ImageUrl về mobile qua api/pois/updates

Mobile sync
  ├─ ResolveRemoteMediaPath / BuildRemoteMediaUrl
  ├─ chuẩn hóa host localhost → 10.0.2.2 trên emulator
  └─ lưu ImagePath vào SQLite

UI
  └─ POI.ImagePath → PoiImageSource → ImageSource.FromUri / FromFile
```

### 4.5 Geofence — trigger thuyết minh tự động

```
GeofenceService (chạy nền)
  ├─ Lắng nghe GPS thay đổi
  ├─ Tính khoảng cách Haversine đến từng POI active
  ├─ Debounce — bỏ qua nhiễu GPS ngắn hạn
  ├─ Cooldown — không trigger lại cùng POI trong thời gian ngắn
  └─ Khi vào vùng hợp lệ → NarrationEngine.PlayAsync(poi)

NarrationEngine.PlayAsync(poi)
  ├─ Có AudioPath → phát file MP3 từ Resources/Raw/
  └─ Không có → fallback TTS theo LanguageCode của POI
```

### 4.6 Zoom & MyLocation (custom buttons)

```
OnZoomInClicked  → _currentRadiusMeters / 1.8 → MoveToRegion
OnZoomOutClicked → _currentRadiusMeters * 1.8 → MoveToRegion
OnMyLocationClicked
  ├─ Geolocation.GetLastKnownLocationAsync() (nhanh)
  ├─ Fallback: GetLocationAsync(Medium accuracy)
  └─ Fallback: CenterOnVinhKhanh() nếu lỗi quyền/GPS
```

### 4.7 Auto sync định kỳ

```
PeriodicTimer (15 phút)
  └─ SyncAndReloadAsync(showAlert: false)
       └─ Cập nhật FilteredPois → PlacePins() tự động
```

---

## 5. Cấu trúc thư mục

```
VinhKhanhFoodStreet/
├─ VinhKhanh.Mobile/
│   ├─ Platforms/Android/
│   │   ├─ MainActivity.cs        ← Android entry point (MainLauncher)
│   │   └─ MapUiCustomizer.cs     ← Tắt Google Maps native controls
│   ├─ Views/
│   │   ├─ MapPage.xaml           ← UI full-screen, overlay controls
│   │   └─ MapPage.xaml.cs        ← Logic pin, card, zoom, location
│   ├─ ViewModels/
│   │   └─ MapViewModel.cs        ← State, categories, filter, sync
│   ├─ Services/
│   │   ├─ LocalDatabase.cs       ← SQLite
│   │   ├─ SyncService.cs         ← API sync
│   │   ├─ GeofenceService.cs     ← Vùng địa lý
│   │   └─ NarrationEngine.cs     ← Audio/TTS
│   ├─ Models/
│   │   └─ PoiRecord.cs           ← SQLite model
│   ├─ MauiProgram.cs             ← DI + cấu hình
│   └─ App.xaml.cs                ← Root page
├─ VinhKhanh.Admin/               ← ASP.NET Core API
├─ VinhKhanh.Admin.Ui/            ← React frontend
├─ VinhKhanh.Shared/              ← Models dùng chung
├─ reinstall.bat                  ← Script deploy emulator
└─ README.md
```

---

## 6. Tiến độ & tồn đọng

| Phân hệ | Tiến độ | Trạng thái |
|---|---:|---|
| Kiến trúc & DI | 100% | Hoàn thiện |
| SQLite + Sync | 90% | Ổn định, cần test offline edge case |
| Geofence | 95% | Debounce/cooldown hoạt động |
| Audio/TTS | 78% | Có fallback TTS, cần test đa thiết bị |
| UI/UX Mobile | 80% | Full-screen map, filter, POI card với ảnh |
| Web Admin API | 40% | CRUD POI cơ bản |
| Admin UI | 30% | Scaffold xong, chưa hoàn thiện |

### Tồn đọng ưu tiên cao

1. Điều tra crash khi khởi động app trên emulator (đang xử lý)
2. Thêm `ACCESS_FINE_LOCATION` permission request khi app lần đầu chạy
3. Kiểm tra `Category` field có được sync từ API không (hiện đang để `string.Empty`)
4. Test ảnh POI load từ URL thực tế

---

## 7. Lịch sử lỗi & bản vá

### 2026-04-03 — Admin mục Phân tích / Cài đặt nhảy về login ở đầu
- Triệu chứng: Khi mở một số trang trong khu vực admin như Phân tích và Cài đặt, giao diện tự cuộn hoặc nhảy về phần login ở phía trên thay vì giữ đúng vị trí nội dung.
- Khu vực ảnh hưởng: Admin UI, các trang có layout dài hoặc có vùng điều hướng/đăng nhập ở đầu trang.
- Ghi chú: Đây là lỗi hành vi giao diện, cần kiểm tra lại state điều hướng, anchor scroll và logic render lại khi chuyển tab.

### 2026-04-03 — Crash khi khởi động (đang điều tra)
- Triệu chứng: App crash ngay sau splash screen
- Nguyên nhân nghi ngờ: thiếu Google Maps API key hoặc lỗi permission location
- Hướng xử lý: xem logcat `AndroidRuntime:E DOTNET:E`

### 2026-04-03 — Thiếu MainActivity.cs
- Triệu chứng: APK cài được nhưng không có launcher activity, monkey abort
- Nguyên nhân: file `Platforms/Android/MainActivity.cs` bị thiếu
- Bản vá: tạo lại `MainActivity : MauiAppCompatActivity` với `MainLauncher = true`

### 2026-04-03 — Package name sai (VinhKhanh.Mobile thay vì com.vinhkhanh.mobile)
- Nguyên nhân: thiếu `<ApplicationId>` trong csproj
- Bản vá: thêm `<ApplicationId>com.vinhkhanh.mobile</ApplicationId>` vào csproj

### 2026-03-28 — Audio không phát được
- Triệu chứng: `Không thể phát xong file âm thanh: lau-nuong.mp3`
- Nguyên nhân: hàm chuẩn hóa đường dẫn cắt mất thư mục con `audio/vi/`
- Bản vá: giữ nguyên đường dẫn đầy đủ, giảm timeout 30s → 12s, thêm fallback TTS

### 2026-03-28 — Frame obsolete warning (.NET 9)
- Nguyên nhân: `Frame` bị deprecated từ .NET 9
- Bản vá: thay toàn bộ `Frame` → `Border` với `StrokeShape="RoundRectangle"`

### 2026-03-28 — PinClicked không tồn tại
- Nguyên nhân: MAUI Maps không có event `PinClicked` trên `Map`
- Bản vá: dùng `MapClicked` + tìm pin gần nhất trong vòng ~40m

### 2026-03-28 — SyncService.MapToLocalPois gọi instance method từ static
- Nguyên nhân: `MapToLocalPois` khai báo `static` nhưng gọi `BuildRemoteMediaUrl` là instance method
- Bản vá: bỏ `static` khỏi `MapToLocalPois`
