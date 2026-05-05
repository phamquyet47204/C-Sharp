# Product Requirements Document (PRD) - Vinh Khanh Food Street

Tài liệu này cung cấp thiết kế kiến trúc chi tiết, sơ đồ UML và phân tích End-to-End (E2E) trực tiếp tới các Class/Method trong mã nguồn. 

---

## 1. Danh Sách Chức Năng Hệ Thống (Features)

### 1.1. Hệ sinh thái Mobile App (Visitor / Khách du lịch)
- **Quét QR Code POI**: Khám phá thông tin quán ăn qua mã QR.
- **Thuyết minh Tự động (Audio Narration / TTS)**: Nghe giới thiệu POI, hỗ trợ đa ngôn ngữ (VI, EN, JA).
- **Tracking Vị trí**: Tự động bắt vị trí GPS, kiểm tra vùng Geofence để kích hoạt TTS.
- **Đánh giá POI**: Rating 1-5 sao cho quán ăn.
- **Mua Access Pass**: Thanh toán In-App Purchase để mở khóa toàn bộ nội dung.

### 1.2. Hệ sinh thái Web Admin (Shop Owner / Chủ quán)
- **Đăng ký Tài khoản**: Gửi yêu cầu làm đối tác (chờ duyệt).
- **Đăng nhập**: Phân quyền truy cập `ShopOwner`.
- **Quản lý POI Của Tôi**: Thêm/Sửa thông tin quán ăn, lấy file mã QR.

### 1.3. Hệ sinh thái Web Admin (Super Admin)
- **Quản lý Phê duyệt Đối tác**: Duyệt yêu cầu tài khoản ShopOwner.
- **Quản lý Toàn bộ POI**: CRUD mọi điểm nhấn, gán "Premium" và "Bán kính quét (Radius)".
- **Dashboard Heatmap Realtime**: Quan sát mật độ đám đông di chuyển theo thời gian thực.
- **Thống kê Nội dung (Content Performance)**: Xem số lượt truy cập và lượt nghe TTS của từng quán.
- **Quản lý User Online**: Thống kê số lượng thiết bị đang trực tuyến.

---

## 2. Sơ Đồ Tổng Quan Hệ Thống

### 2.1. Use Case Diagram
Mô tả các quyền hạn và hành động của 3 nhóm người dùng.

```mermaid
usecaseDiagram
    actor "Visitor" as visitor
    actor "Shop Owner" as owner
    actor "Admin" as admin

    usecase "Listen to TTS Narration" as uc1
    usecase "Scan QR Code" as uc2
    usecase "Purchase Access Pass" as uc3
    
    usecase "Register as Partner" as uc4
    usecase "Manage Own POI" as uc5
    
    usecase "Approve Shop Owner" as uc6
    usecase "Manage All POIs" as uc7
    usecase "View Heatmap" as uc8
    usecase "View Analytics" as uc9

    visitor --> uc1
    visitor --> uc2
    visitor --> uc3

    owner --> uc4
    owner --> uc5

    admin --> uc6
    admin --> uc7
    admin --> uc8
    admin --> uc9
```

### 2.2. Entity Relationship Diagram (ERD)
Ánh xạ trực tiếp từ các entity trong `VinhKhanh.Domain.Entities`.

```mermaid
erDiagram
    ApplicationUser {
        string Id PK
        string FullName
        string Email
        bool IsApproved
    }
    Poi {
        int Id PK
        string BasePoiId
        float Latitude
        float Longitude
        float Radius
        bool IsPremium
    }
    PoiLocalization {
        int Id PK
        int PoiId FK
        string LanguageCode
        string Name
        string Description
    }
    PoiRating {
        int Id PK
        int PoiId FK
        string DeviceId
        int Stars
    }
    AnalyticsEvent {
        int Id PK
        string DeviceId
        int PoiId FK
        string EventType "visit, narration, location_update, app_online"
        float Latitude
        float Longitude
        datetime Timestamp
    }
    FreeTrialRecord {
        int Id PK
        string DeviceId
        int PoiId FK
        datetime FirstHeardAt
    }

    ApplicationUser ||--o{ Poi : "Owns (Optional)"
    Poi ||--|{ PoiLocalization : "Has"
    Poi ||--o{ PoiRating : "Received"
    Poi ||--o{ AnalyticsEvent : "Tracks"
    Poi ||--o{ FreeTrialRecord : "Trial"
```

### 2.3. Class Diagram (Clean Architecture)
Sự tương tác giữa các tầng kiến trúc chính trong mã nguồn.

```mermaid
classDiagram
    class Domain_Entities {
        +AnalyticsEvent
        +ApplicationUser
        +Poi
    }
    
    class Infrastructure_Data {
        +AppDbContext
    }
    
    class Application_UseCases {
        +AnalyticsVisitUseCase
        +PoiSyncUseCase
        +AdminApproveUseCase
    }

    class WebAPI_Controllers {
        +AnalyticsController
        +PoiController
        +AuthController
    }
    
    class WebAPI_Hubs {
        +AnalyticsHub
    }

    Infrastructure_Data ..> Domain_Entities : DbSet
    Application_UseCases ..> Infrastructure_Data : DI injected
    WebAPI_Controllers ..> Application_UseCases : Execute
    WebAPI_Controllers ..> WebAPI_Hubs : Push Update
```

---

## 3. Phân Tích Chuyên Sâu End-to-End (E2E Tracing)

### 3.1. Chức Năng Thống Kê Lượt Nghe TTS (Audio Narration)

**Bài toán:** Khi khách du lịch quét QR hoặc đi vào vùng kích hoạt (Geofence), điện thoại phát Audio TTS. Hành động này được biểu diễn bằng "Lượt Nghe" trên Admin Dashboard như thế nào?

**Luồng dữ liệu (Data Flow):**
1. **Mobile App (Triggers)**: 
    *   **Manual**: Người dùng nhấn Play trên danh sách/bản đồ.
    *   **QR Scan**: Người dùng quét mã tại quán, OS mở Deep Link `vinhkhanh://poi/{id}`, App nhận ID và tự động phát.
    *   **Geofence**: `LocationService` phát hiện tọa độ nằm trong vùng POI.
2. **Execution**: `NarrationService.PlayAudioAsync()` được gọi. Ngay lập tức, `AnalyticsService.TrackActivityAsync(eventType: "narration")` gửi lệnh POST `/api/analytics/visit`.
3. **API Backend**: `AnalyticsController.LogVisit(AnalyticsVisitCommand)` tiếp nhận.
4. **Use Case & DB**: `AnalyticsVisitUseCase.ExecuteAsync()` ghi lại sự kiện vào bảng `AnalyticsEvents`.
5. **Realtime & Admin**: `AnalyticsHub` (SignalR) push dữ liệu về client Admin. Dashboard cập nhật biểu đồ lượt nghe.

**Activity Diagram:**
```mermaid
stateDiagram-v2
    [*] --> Trigger
    state Trigger {
        [*] --> ScanQR: User scans QR code
        [*] --> Geofence: Enter POI zone (LocationService)
    }
    
    Trigger --> CheckQueue
    CheckQueue: AudioQueueManager checks priority
    
    state if_queue <<choice>>
    CheckQueue --> if_queue
    if_queue --> CancelOld: Lower priority audio is playing
    if_queue --> PlayNew: No conflict
    
    CancelOld --> PlayNew: Call CancellationToken.Cancel()
    PlayNew: NarrationService.PlayNarrationAsync()
    
    PlayNew --> AnalyticsCall
    AnalyticsCall: AnalyticsService.TrackActivityAsync("narration", poiId)
    
    AnalyticsCall --> BackendReceive
    BackendReceive: POST /api/analytics/visit
    
    state if_valid <<choice>>
    BackendReceive --> if_valid
    if_valid --> SaveAnalytics: Valid Data
    if_valid --> ErrorLog: Invalid Parameters (BadRequest)
    
    SaveAnalytics: AnalyticsVisitUseCase.ExecuteAsync() (INSERT AnalyticsEvents)
    
    SaveAnalytics --> CheckFreeTrial
    CheckFreeTrial: Check FreeTrialRecords
    
    state if_freetrial <<choice>>
    CheckFreeTrial --> if_freetrial
    if_freetrial --> InsertTrial: First time hearing this POI
    if_freetrial --> SkipTrial: Heard before
    
    InsertTrial --> SignalRPublish
    SkipTrial --> SignalRPublish
    
    SignalRPublish: PublishRealtimeUpdateAsync() (Debounce 1s)
    
    SignalRPublish --> AdminDashboard
    AdminDashboard: Update Dashboard/Charts via SignalR
    AdminDashboard --> [*]
```

**Sequence Diagram (Detailed Method Tracing):**
```mermaid
sequenceDiagram
    participant OS as User / OS / GPS
    participant UI as MainPage.xaml.cs
    participant AS as AnalyticsService.cs
    participant API as AnalyticsController.cs
    participant UC as AnalyticsVisitUseCase.cs
    participant NS as NarrationService.cs

    alt Các phương thức kích hoạt (Triggers)
        OS->>UI: Nhấn tay -> OnPinCardPlay(sender, e)
    else QR Scan
        OS->>UI: Deep Link -> OnApplinkReceived()
    else Geofence
        OS->>UI: LocationEvent -> OnPoiEntered(poi)
    end
    
    par Luồng Analytics
        UI->>AS: TrackActivityAsync(lat, lng, "narration", poi.Id)
        AS->>AS: SendAnalyticsEventAsync(lat, lng, eventType, poiId)
        AS->>API: _httpClient.PostAsJsonAsync("api/analytics/visit", command)
        API->>API: LogVisit([FromBody] AnalyticsVisitCommand command)
        API->>UC: ExecuteAsync(command)
        UC-->>API: Task Completed (Lưu DB)
        API-->>AS: return Ok()
    and Luồng Âm thanh
        UI->>NS: PlayAudioAsync(poi.AudioPath) / SpeakAsync()
        NS->>NS: RunExclusiveNarrationAsync(workFunc)
        Note over NS: Chuyển qua AudioQueueManager
    end
```

---

### 3.2. Chức Năng Tracking Vị Trí & Bản Đồ Heatmap

**Bài toán:** Quản lý xem được mật độ tụ tập của khách di chuyển thực tế trên phố Vĩnh Khánh.

**Luồng dữ liệu (Data Flow):**
1. **Mobile App**: Hàm `OnLocationChanged` trong `LocationService` định kỳ bắt tọa độ GPS. Truyền sang `AnalyticsService.TrackActivityAsync(eventType: "location_update")`.
2. **Backend**: Nhận tọa độ, lưu vào bảng `UserLocations`.
3. **Realtime Engine**: SignalR Hub (`AnalyticsHub`) phát tín hiệu về Admin Dashboard.
4. **Admin UI**: Sử dụng thư viện `Leaflet.heat` hoặc Google Maps Heatmap Layer để vẽ mật độ dựa trên dữ liệu tọa độ trong 30 phút gần nhất.

**Sequence Diagram (Real-time Data Flow Method Tracing):**
```mermaid
sequenceDiagram
    participant LS as LocationService.cs
    participant AS as AnalyticsService.cs
    participant API as AnalyticsController.cs
    participant Hub as AnalyticsHub.cs
    participant Dash as Dashboard.jsx

    loop Khi GPS cập nhật
        LS->>LS: OnLocationChanged(Location location)
        LS->>AS: TrackActivityAsync(..., "location_update", null)
        AS->>AS: SendAnalyticsEventAsync(lat, lng, eventType, poiId)
        AS->>API: _httpClient.PostAsJsonAsync("api/analytics/visit", command)
        API->>API: LogVisit([FromBody] AnalyticsVisitCommand command)
        API->>Hub: PublishRealtimeUpdateAsync(command)
        Hub-->>Dash: Context.Clients.All.SendAsync("analytics:realtime")
        Dash->>Dash: fetchOverviewData() -> reRenderHeatmap()
    end
```

---

### 3.3. Thuật toán Xử lý Geofence & Nhường Ưu tiên (Preemption)

**Bài toán:** Xử lý trường hợp người dùng đứng ở vùng giao thoa của nhiều quán (POI) hoặc GPS nhảy nhiễu.

**Quy trình xử lý (Logic Flow):**
1. **Debounce (Chống nhiễu)**: Tọa độ phải nằm trong vùng POI ít nhất 2 lần liên tiếp mới xác nhận "Vào vùng".
2. **Cooldown (Chống lặp)**: Sau khi phát xong, POI rơi vào trạng thái chờ 10 phút để tránh đọc đi đọc lại.
3. **Priority (Ưu tiên)**: Nếu đứng giữa 2 vùng, chọn POI có `Priority` cao nhất (thường là quán Premium).
4. **Preemption (Nhường quyền)**: Nếu đang phát quán thường mà người dùng bước vào vùng quán Premium, hệ thống lập tức ngắt âm thanh cũ để phát quán Premium.

**Activity Diagram:**
```mermaid
stateDiagram-v2
    [*] --> GPS_Update: Nhận tọa độ mới
    GPS_Update --> CalculateDistance: Tính khoảng cách (Haversine)
    
    state Filter {
        [*] --> Debounce: Check liên tiếp 2 lần?
        Debounce --> Cooldown: Check 10p hồi chiêu?
        Cooldown --> Priority: Check Priority cao nhất?
    }
    
    CalculateDistance --> Filter
    
    state if_overlap <<choice>>
    Filter --> if_overlap
    
    if_overlap --> PlayNew: Thỏa mãn mọi điều kiện
    if_overlap --> Skip: Không đủ điều kiện
    
    state PlayNew {
        [*] --> CheckCurrentActive: Có quán nào đang phát?
        CheckCurrentActive --> StopOld: Quán mới có Priority cao hơn
        CheckCurrentActive --> StartAudio: Không có quán nào khác
        StopOld --> StartAudio: Ngắt quán cũ
    }
    
    StartAudio --> [*]
```

**Sequence Diagram (Preemption Method Tracing):**
```mermaid
sequenceDiagram
    participant LS as LocationService.cs
    participant GE as GeofenceEngine.cs
    participant QM as AudioQueueManager.cs
    participant NS as NarrationService.cs

    LS->>GE: OnLocationChanged(Location location)
    GE->>GE: ProcessLocationAsync(currentLocation)
    GE->>GE: HandleInsidePoisWithPriorityAndDebounce()
    
    Note over GE: Chọn selectedPoi (Dựa trên Priority)
    
    GE->>NS: OnPoiEntered?.Invoke(selectedPoi)
    NS->>QM: RunExclusiveAsync(Func<CancellationToken, Task> work)
    QM->>QM: CancelCurrent() -> _cts?.Cancel()
    
    rect rgb(255, 240, 240)
        Note right of QM: Task cũ bị ngắt bởi CancellationToken
        QM-->>NS: Ném ngoại lệ OperationCanceledException
        NS->>NS: Bắt exception -> EndAudioDucking() & Stop()
    end

    QM->>QM: Await _semaphore.WaitAsync()
    QM->>NS: Gọi callback `work(newCts.Token)`
    NS->>NS: PlayWithMediaElementAsync() / TextToSpeech.SpeakAsync()
```

---

### 3.4. Quy trình Đăng ký & Phê duyệt Shop (Onboarding)

**Bài toán:** Quản lý vòng đời của một địa điểm ẩm thực trên hệ thống.

**Vòng đời dữ liệu (State Diagram):**
```mermaid
stateDiagram-v2
    [*] --> Pending: Shop Owner gửi form đăng ký
    Pending --> Approved: Admin kiểm tra & duyệt
    Pending --> Rejected: Thông tin không hợp lệ
    
    Approved --> Active: Admin gán mã QR & Vị trí
    Active --> Suspended: Vi phạm chính sách
    Suspended --> Active: Khôi phục hoạt động
    
    Active --> [*]: Xóa khỏi hệ thống
```

**Sequence Diagram (Shop Onboarding Method Tracing):**
```mermaid
sequenceDiagram
    participant UI as RegisterShopPage.xaml.cs
    participant API as AuthController.cs
    participant DB as AppDbContext.cs
    participant Hub as AnalyticsHub.cs
    participant Admin as AdminController.cs

    UI->>UI: OnSubmitClicked(sender, e)
    UI->>API: _httpClient.PostAsJsonAsync("api/auth/register-shop", data)
    API->>API: RegisterShop([FromBody] RegisterShopRequest request)
    API->>API: userManager.CreateAsync(user, request.Password)
    API->>DB: AddToRoleAsync(user, "ShopOwner")
    API->>Hub: Context.Clients.All.SendAsync("new_registration", payload)
    API-->>UI: return Ok()
    
    Note over Admin: Admin duyệt tại màn hình quản lý
    Admin->>Admin: UpdateOwner(string userId, [FromBody] UpdateOwnerRequest request)
    Admin->>DB: dbContext.Pois.FirstOrDefaultAsync(p => p.OwnerId == userId)
    Admin->>DB: poi.IsPremium = true (Dựa vào PremiumOption)
    Admin->>DB: await dbContext.SaveChangesAsync()
    Admin-->>Admin: return Ok(user)
```

---

### 3.5. Kiến trúc Tổng thể Hệ thống (System Architecture)

```mermaid
graph TD
    subgraph "Client Layer"
        Mobile[Vĩnh Khánh Mobile - MAUI]
        Admin[Admin Dashboard - React/Vite]
    end

    subgraph "API Gateway & Services"
        API[ASP.NET Core Web API]
        SignalR[SignalR Real-time Hub]
    end

    subgraph "Data Layer"
        SQL[(SQL Server / Entity Framework)]
        SQLite[(SQLite - Offline Mobile)]
    end

    Mobile <--> API
    Admin <--> API
    API <--> SQL
    Mobile <--> SQLite
    API -- Push --> SignalR
    SignalR -- Realtime Update --> Admin
```
2. **API Backend**: `AnalyticsController.LogVisit()` lưu tọa độ vào `AnalyticsEvents`.
3. **Logic Heatmap**: Khi Admin mở trang chủ, `Dashboard.jsx` gọi `api.get('/analytics/realtime-overview')`.
4. **Backend Calculation**: Hàm `BuildHeatmapPoints()` phân tích các `AnalyticsEvent` mới nhất. Nó chạy thuật toán "Lực hút POI" (hút tọa độ user về trung tâm quán nếu user ở trong bán kính Radius) và cộng điểm `Weight/Intensity`.
5. **Admin UI**: Dữ liệu `points` được gán vào `leaflet.heat` plugin, render ra mảng màu gradient đỏ/vàng trên bản đồ Leaflet.

**Activity Diagram:**
```mermaid
stateDiagram-v2
    [*] --> LocationChanged
    LocationChanged: LocationService receives coordinates (Lat, Lng)
    
    LocationChanged --> ThrottleCheck
    ThrottleCheck: Check distance / time interval
    
    state if_throttle <<choice>>
    ThrottleCheck --> if_throttle
    if_throttle --> Ignore: Threshold not met
    if_throttle --> SendAPI: Condition met to send
    
    SendAPI: POST /api/analytics/visit ("location_update")
    SendAPI --> DBInsert
    DBInsert: Save to AnalyticsEvents
    
    state Admin_Side {
        [*] --> FetchRealtime
        FetchRealtime: Dashboard fetches last 45s data
        
        FetchRealtime --> FilterOnline
        FilterOnline: Filter Online devices (exclude "app_offline")
        
        FilterOnline --> AssignPOI
        AssignPOI: CalculateDistance() matches User to POI
        
        state if_poi <<choice>>
        AssignPOI --> if_poi
        if_poi --> SnapToPOI: Inside POI radius (Snap to center)
        if_poi --> SnapToGrid: Outside POI (Group by 44m grid)
        
        SnapToPOI --> CalcWeight
        SnapToGrid --> CalcWeight
        
        CalcWeight: Calculate Intensity & Density
        CalcWeight --> RenderHeatmap
        RenderHeatmap: leaflet.heat renders Gradient layer
    }
    
    DBInsert --> FetchRealtime: Data available
```

**Sequence Diagram:**
```mermaid
sequenceDiagram
    participant Mobile as LocationService
    participant API as AnalyticsController
    participant DB as AppDbContext
    participant Web as Dashboard.jsx (Admin)

    Mobile->>API: POST /api/analytics/visit (Lat, Lng)
    API->>DB: Log location event
    loop Every 30s
        Web->>API: GET /analytics/realtime-overview
        API->>DB: Fetch Online Users & Coordinates
        API->>API: BuildHeatmapPoints(apply radius)
        API-->>Web: Return Array {lat, lng, intensity}
        Web->>Web: Update Leaflet Heatmap layer
    end
```

---

### 3.3. Chức Năng Đăng Ký và Phê Duyệt Chủ Quán (Shop Owner)

**Bài toán:** Ai đó muốn mở quán trên App phải đăng ký. Admin phải duyệt thì mới đăng nhập được.

**Luồng dữ liệu (Data Flow):**
1. **Đăng Ký (Web/Mobile)**: Nhập Email, Pass trên trang `Register.jsx` hoặc Mobile App. Gọi API POST `/api/auth/register-shop`.
2. **Lưu DB (Chờ Duyệt)**: `AuthController.RegisterShop()` sử dụng `UserManager.CreateAsync(user)`. Gán `IsApproved = false` và Roles = `ShopOwner`.
3. **Kiểm Tra Đăng Nhập**: Nếu cố tình đăng nhập, `AuthController.Login` chặn lại ở đoạn `if (!user.IsApproved) return Unauthorized("Đang chờ duyệt")`.
4. **Admin Phê Duyệt**: Admin vào trang `OwnerManager.jsx`, bấm nút **Approve**. Gọi API PUT `/api/approvals/{id}/approve`.
5. **Xử lý Phê duyệt**: `AdminApproveUseCase.ApproveOwnerAsync()` được chạy, đổi cờ `IsApproved = true` trong database. Lúc này tài khoản mới chính thức hoạt động.

**Activity Diagram:**
```mermaid
stateDiagram-v2
    [*] --> SubmitForm
    SubmitForm: Fill Form (Email, Pass, FullName)
    SubmitForm --> API_Register
    API_Register: POST /auth/register-shop
    
    state check_email <<choice>>
    API_Register --> check_email
    check_email --> Return400: Email exists / Weak Password
    check_email --> CreateUser: Valid
    
    CreateUser: UserManager.CreateAsync()
    CreateUser --> SetFlags
    SetFlags: Set Role="ShopOwner", IsApproved=false
    
    state LoginProcess {
        LoginAttempt: POST /auth/login
        state check_approve <<choice>>
        LoginAttempt --> check_approve
        check_approve --> Reject: IsApproved == false (403)
        check_approve --> Success: IsApproved == true (200 OK)
    }
    
    SetFlags --> LoginAttempt: Shop Owner attempts login
    
    state AdminProcess {
        LoadPending: OwnerManager loads list
        LoadPending --> ApproveClick
        ApproveClick: Admin clicks "Approve"
        ApproveClick --> AdminApproveUseCase
        AdminApproveUseCase: Update IsApproved = true
    }
    
    Reject --> AdminProcess: Waiting for Admin approval
    AdminApproveUseCase --> LoginAttempt: Allow login
```

**Sequence Diagram:**
```mermaid
sequenceDiagram
    participant UI as Web/Mobile UI
    participant Auth as AuthController
    participant DB as AspNetUsers (DB)
    participant AdminUI as OwnerManager.jsx
    participant ApproveUC as AdminApproveUseCase

    UI->>Auth: POST /register-shop {email, password}
    Auth->>DB: CreateAsync (IsApproved = false, Role = ShopOwner)
    UI->>Auth: POST /login {email, password}
    Auth-->>UI: 403 Forbidden (Pending Approval)
    
    AdminUI->>ApproveUC: PUT /api/approvals/{id}/approve
    ApproveUC->>DB: SET IsApproved = true
    ApproveUC-->>AdminUI: Success
    
    UI->>Auth: POST /login {email, password}
    Auth-->>UI: 200 OK + JWT Token
```

---

### 3.4. Quản Lý và Đồng Bộ POI (Points of Interest)

**Bài toán:** Khi Admin thêm/sửa một quán ăn trên Web, làm sao để Data đó hiện xuống điện thoại cực nhanh và có thể quét QR?

**Luồng dữ liệu (Data Flow):**
1. **Tạo/Sửa POI (Admin/ShopOwner)**: Trang `PoiForm.jsx` submit data lên API POST/PUT `/api/poi`. `PoiController` tiếp nhận.
2. **Database Update**: Dữ liệu lưu vào bảng `Pois` và `PoiLocalizations` trong SQL Server.
3. **Xóa Cache Mobile**: Mỗi lần có POI update, hệ thống tăng cờ `LastSyncAt`.
4. **Mobile Sync**: Khi App Mobile khởi động, `PoiSyncService` bắn GET `/api/poi/sync` kèm theo thời gian sync cuối.
5. **Backend Sync**: `PoiSyncUseCase.ExecuteAsync()` truy vấn DB trả về danh sách các POI mới hoặc đã bị thay đổi (`UpdatedPois`), và các POI đã bị xóa (`DeletedIds`).
6. **Local Storage**: App Mobile lưu dữ liệu này vào SQLite cục bộ (offline cache), đảm bảo việc hiển thị danh sách trên màn hình Home (`MainPage.xaml`) cực kỳ mượt mà.

**Activity Diagram:**
```mermaid
stateDiagram-v2
    [*] --> AdminEdit
    AdminEdit: PoiForm.jsx saves POI
    AdminEdit --> API_Save
    API_Save: PoiController.Create/Update
    API_Save --> DB_Save
    DB_Save: Save to SQL Server (Pois)
    
    state MobileAppSync {
        [*] --> AppStart
        AppStart: Open Mobile App
        AppStart --> SyncService
        SyncService: PoiSyncService.SyncAsync()
        SyncService --> API_GetSync
        API_GetSync: GET /api/poi/sync?lastSync={time}
        
        API_GetSync --> DB_Query
        DB_Query: Fetch updated & deleted POIs
        DB_Query --> ReturnData
        ReturnData: Return {UpdatedPois, DeletedIds}
        
        ReturnData --> LocalSQLite
        LocalSQLite: Insert/Update/Delete Offline DB
        LocalSQLite --> ReloadUI
        ReloadUI: Redraw Map Markers
        ReloadUI --> [*]
    }
    
    DB_Save --> MobileAppSync: Data ready signal
```

**Sequence Diagram:**
```mermaid
sequenceDiagram
    participant Web as PoiForm.jsx
    participant API as PoiController
    participant DB as SQL Server
    participant Mobile as PoiSyncService
    participant SQLite as Local SQLite App

    Web->>API: POST /api/poi (POI Data)
    API->>DB: Save to Pois table
    API-->>Web: OK
    
    Mobile->>API: GET /api/poi/sync?lastSync=2026-01-01
    API->>DB: Fetch changes since 2026-01-01
    API-->>Mobile: {UpdatedPois, DeletedIds}
    Mobile->>SQLite: Update Offline DB
    Mobile->>Mobile: Reload MainPage UI
```

---

### 3.5. Chức Năng Thống Kê Tổng Quan (Dashboard Summary)

**Bài toán:** Hiển thị cho Admin các con số tổng quát về hệ thống bao gồm: Tổng số POI, Tổng số ShopOwner (và số lượng đang chờ duyệt), Lượt truy cập hôm nay, Lượt nghe TTS hôm nay, và số người đang Online.

**Luồng dữ liệu (Data Flow):**
1. **Truy cập Dashboard**: Admin mở trang `Analytics.jsx` hoặc `Dashboard.jsx`. Giao diện Frontend gọi API GET `/api/admin/dashboard-summary`.
2. **Xử lý Backend**: Hàm `AdminController.GetDashboardSummary()` được thực thi.
3. **Tính toán Thống kê**:
   - `poisCount`: Lấy số lượng từ bảng `Pois`.
   - `totalShops` & `pendingOwnersCount`: Dùng `UserManager.GetUsersInRoleAsync("ShopOwner")` để lấy tổng số và lọc ra những user có `IsApproved == false`.
   - `visitsToday` & `narrationCountToday`: Lọc bảng `AnalyticsEvents` theo mốc thời gian từ đầu ngày hiện tại (múi giờ +7).
   - `onlineCount`: Gom nhóm các thiết bị gửi event trong 5 phút gần nhất.
4. **Trả dữ liệu về UI**: Dữ liệu JSON trả về được cập nhật vào State `stats`, truyền xuống các component `<StatCard>` để hiển thị số lớn (ví dụ: "Đối tác Shop", "Địa điểm POI").

**Activity Diagram:**
```mermaid
stateDiagram-v2
    [*] --> Admin_Dashboard
    Admin_Dashboard: Admin opens Analytics/Dashboard
    Admin_Dashboard --> Fetch_Summary
    Fetch_Summary: Call GET /api/admin/dashboard-summary
    
    state Backend_Query {
        [*] --> Count_POI
        Count_POI: dbContext.Pois.CountAsync()
        
        Count_POI --> Count_Events
        Count_Events: Query AnalyticsEvents (Visits, TTS, Online)
        
        Count_Events --> Fetch_Owners
        Fetch_Owners: UserManager.GetUsersInRoleAsync("ShopOwner")
        
        Fetch_Owners --> Filter_Pending
        Filter_Pending: Filter pending owners (!IsApproved)
    }
    
    Fetch_Summary --> Backend_Query
    Backend_Query --> Return_JSON
    Return_JSON: Return Summary Object
    Return_JSON --> Update_State
    Update_State: React setState(stats)
    Update_State --> Render_Cards
    Render_Cards: Display on StatCards (POI, Shop)
    Render_Cards --> [*]
```

**Sequence Diagram:**
```mermaid
sequenceDiagram
    participant UI as Analytics.jsx (Admin)
    participant API as AdminController
    participant DB_Pois as AppDbContext (Pois)
    participant DB_Events as AppDbContext (AnalyticsEvents)
    participant UM as UserManager (AspNetUsers)

    UI->>API: GET /api/admin/dashboard-summary
    
    API->>DB_Pois: CountAsync()
    DB_Pois-->>API: poisCount
    
    API->>DB_Events: CountAsync(Distinct DeviceId / Today)
    DB_Events-->>API: visitsToday, onlineCount, narrationCount
    
    API->>UM: GetUsersInRoleAsync("ShopOwner")
    UM-->>API: List<ApplicationUser> (ShopOwners)
    API->>API: Calculate pendingOwnersCount (!IsApproved)
    
    API-->>UI: { poisCount, totalShops, pendingOwners, visits... }
    
    UI->>UI: setStats(data)
    UI->>UI: Render <StatCard title="Total POIs" />
    UI->>UI: Render <StatCard title="Shop Partners" />
```

---

### 3.6. Chức Năng Quản Lý Quyền Truy Cập (Access Pass & Free Trial)

**Bài toán:** Khách du lịch tải App Mobile lần đầu sẽ được dùng thử miễn phí 7 ngày mà không cần tạo tài khoản đăng nhập. Khi hết hạn, hệ thống tự động khóa tính năng Thuyết minh.

**Luồng dữ liệu (Data Flow):**
1. **Khởi tạo Device ID**: `AccessService` trên Mobile ưu tiên đọc `AndroidId` (hoặc tạo ra một GUID lưu ngầm vào `Preferences` của thiết bị) để định danh duy nhất (Device Identification).
2. **Đồng bộ trạng thái**: App gọi GET `/api/access/check?deviceId=...` để kiểm tra gói cước trên Server.
3. **Tự động Start Trial**: Nếu Backend trả về là "Chưa từng dùng thử", Mobile tự động gọi POST `/api/access/start-trial`. Backend kích hoạt gói 7 ngày và trả về `ExpiryDate`.
4. **Lưu trữ Offline**: Thời hạn dùng thử được lưu vào bộ nhớ cục bộ `Preferences` (khóa `access_pass_expiry`).
5. **Cổng chặn Tính năng**: Mỗi lần khách quét QR hoặc bước vào vùng Geofence, hàm `AccessService.HasActivePass()` sẽ kiểm tra thời hạn Local. Nếu hết hạn, chặn phát âm thanh và hiện thông báo yêu cầu mua gói VIP.

**Activity Diagram:**
```mermaid
stateDiagram-v2
    [*] --> AppStart
    AppStart: App Starts
    AppStart --> GetDeviceID
    GetDeviceID: AccessService gets Android_ID / Generates GUID
    
    GetDeviceID --> API_Check
    API_Check: GET /api/access/check
    
    state if_new_device <<choice>>
    API_Check --> if_new_device
    if_new_device --> HasPass: Has history (Save ExpiryDate)
    if_new_device --> NoPass: Completely new device
    
    NoPass --> API_StartTrial
    API_StartTrial: POST /api/access/start-trial
    API_StartTrial --> SaveLocal
    SaveLocal: Save ExpiryDate (Now + 7 days) to Preferences
    
    HasPass --> SaveLocal
    
    state PlayAudioRequest {
        RequestTTS: Narration Request (Scan QR/Geofence)
        RequestTTS --> CheckPass
        CheckPass: AccessService.HasActivePass()
        
        state if_expired <<choice>>
        CheckPass --> if_expired
        if_expired --> PlayOK: Valid (Allow Audio)
        if_expired --> ShowPaywall: Expired (Block & Require VIP)
    }
    
    SaveLocal --> PlayAudioRequest
```

**Sequence Diagram:**
```mermaid
sequenceDiagram
    participant App as Mobile App
    participant AccessService
    participant Prefs as Local Preferences
    participant API as AccessController (Backend)
    participant DB as SQL Server

    App->>AccessService: Start (Get DeviceId)
    AccessService->>API: GET /api/access/check?deviceId=...
    API->>DB: Query FreeTrial / Payment
    DB-->>API: Result (Never used trial)
    API-->>AccessService: { HasActivePass: false }
    
    AccessService->>API: POST /api/access/start-trial
    API->>DB: Activate 7-day trial
    API-->>AccessService: { ExpiryDate: "2026-05-12T..." }
    AccessService->>Prefs: Save 'access_pass_expiry'
    
    Note over App, Prefs: User scans QR or enters Geofence
    App->>AccessService: Call HasActivePass()
    AccessService->>Prefs: Check Local expiry
    Prefs-->>AccessService: Still valid
    AccessService-->>App: Return TRUE (Allow audio)
```

---

### 3.7. Kích Hoạt Thuyết Minh Bằng Geofence & Khóa Luồng (Priority Preemption)

**Bài toán:** Khách đang nghe giới thiệu về "Quán A" (Bình thường). Đột nhiên khách bước vào vùng của "Quán B" (Premium - Trả tiền chạy QC). App phải lập tức ngắt tiếng "Quán A" để ưu tiên phát "Quán B". Đồng thời, App phải chống hiện tượng nhiễu sóng GPS (Debounce) và chống việc phát đi phát lại một bài liên tục (Cooldown).

**Luồng dữ liệu (Data Flow):**
1. **Lắng nghe Tọa độ**: `LocationService` nhận GPS đưa vào `GeofenceEngine.ProcessLocationAsync()`.
2. **So khớp Bán kính**: Dùng công thức lượng giác *Haversine* tính khoảng cách. Nếu khoảng cách <= `Radius` -> Khách đang ở trong ranh giới POI.
3. **Chống nhiễu (Debounce)**: Tọa độ phải nằm trong vùng POI >= 2 lần liên tiếp (`EnterDebounceThreshold`) mới xác nhận là thực sự vào quán.
4. **Xử lý Ưu tiên (Priority Preemption)**: 
   - Lọc các POI hợp lệ. Chọn POI có `Priority` lớn nhất (Premium).
   - Nếu đang phát bài cũ có Priority thấp hơn, gọi hủy (`OnPoiExited`) bài cũ lập tức để nhường luồng.
5. **Khóa Cooldown**: Sau khi phát sự kiện `OnPoiEntered` (đọc tiếng), gọi hàm `MarkPoiAsPlayed()` khóa POI đó trong 10 phút. Ngăn chặn việc khách đứng yên một chỗ mà App cứ lặp đi lặp lại một bài duy nhất.

**Activity Diagram:**
```mermaid
stateDiagram-v2
    [*] --> OnLocationChanged
    OnLocationChanged: Receive new GPS signal
    OnLocationChanged --> Haversine
    Haversine: Calculate distance to all Cached POIs
    
    Haversine --> FilterRadius
    FilterRadius: Distance <= Radius
    
    FilterRadius --> DebounceCheck
    DebounceCheck: Count stable hits >= 2 (Debounce)
    
    state if_debounce <<choice>>
    DebounceCheck --> if_debounce
    if_debounce --> Drop: GPS noise or Cooldown active (10m)
    if_debounce --> CheckPriority: Filter passed
    
    CheckPriority: Compare Priority (New POI vs Playing POI)
    
    state if_priority <<choice>>
    CheckPriority --> if_priority
    if_priority --> CancelOld: New POI has Higher Priority (Preemption)
    if_priority --> PlayNew: Channel is idle
    if_priority --> Drop: New POI has Lower Priority -> Ignore
    
    CancelOld --> EmitExit
    EmitExit: Emit OnPoiExited (Stop old Audio)
    EmitExit --> PlayNew
    
    PlayNew --> EmitEnter
    EmitEnter: Emit OnPoiEntered (Play new Audio)
    EmitEnter --> MarkCooldown
    MarkCooldown: Lock POI in Cooldown for 10 mins
    MarkCooldown --> [*]
```

**Sequence Diagram:**
```mermaid
sequenceDiagram
    participant GPS as LocationService
    participant Engine as GeofenceEngine
    participant RAM as POI Cache (RAM)
    participant Audio as NarrationService
    
    GPS->>Engine: OnLocationChanged(lat, lng)
    Engine->>RAM: Scan distance (Haversine)
    RAM-->>Engine: Detect [Shop A (Pri 0), Shop B (Pri 100)]
    
    Engine->>Engine: Increase Debounce counter >= 2
    
    Note over Engine, Audio: Priority Preemption
    Engine->>Engine: Notice Shop B is Premium (Higher Priority)
    
    Engine->>Audio: Emit OnPoiExited(Shop A)
    Audio->>Audio: CancellationToken.Cancel() to interrupt
    
    Engine->>Audio: Emit OnPoiEntered(Shop B)
    Audio->>Audio: PlayNarrationAsync(Shop B Audio)
    
    Engine->>Engine: MarkPoiAsPlayed(Shop B)
    Note over Engine: Freeze (Cooldown) Shop B for 10 mins
```
