# VinhKhanh Admin Module

Đây là phân hệ quản trị của hệ thống **Vĩnh Khánh Digital Tour Guide**, được xây dựng để dành cho hai đối tượng chính: **Admin hệ thống** và **Chủ cửa hàng (Shop Owner)**.

## 1. Tổng quan kỹ thuật
- **Backend**: ASP.NET Core 8.0 (Web API).
- **Frontend**: React (Vite) + Tailwind CSS + Lucide Icons.
- **AI Integration**: Google Gemini AI (Dùng cho dịch thuật tự động).
- **Database**: SQL Server (Entity Framework Core).

---

## 2. Sơ đồ Chức năng (Use Case Diagram)
Sơ đồ dưới đây mô tả các chức năng chính và quyền hạn của hai nhóm người dùng chính trong hệ thống Admin.

```mermaid
graph TD
    subgraph Users
        Admin((Admin Hệ thống))
        Owner((Chủ cửa hàng))
    end

    subgraph "VinhKhanh Admin System"
        UC1[Quản lý toàn bộ POI]
        UC2[Duyệt/Từ chối POI]
        UC3[Xem Dashboard & Thống kê]
        UC4[Quản lý các sự kiện Analytics]
        UC5[Phê duyệt tài khoản Chủ quán]
        
        UC6[Đăng ký tài khoản Shop]
        UC7[Quản lý POI cá nhân]
        UC8[Gửi yêu cầu xét duyệt POI]
        UC9[Tự động dịch thuật bằng AI]
    end

    Admin --- UC1
    Admin --- UC2
    Admin --- UC3
    Admin --- UC4
    Admin --- UC5

    Owner --- UC6
    Owner --- UC7
    Owner --- UC8
    Owner --- UC9
    
    UC1 -.- UC9
    UC7 -.- UC9
```

---

## 3. Sơ đồ Trạng thái POI (State Diagram)
Mô tả vòng đời của một điểm tham quan (POI) từ khi khởi tạo đến khi được hiển thị trên bản đồ mobile.

```mermaid
stateDiagram-v2
    [*] --> Draft : Khởi tạo
    Draft --> Pending_Approval : Gửi xét duyệt
    Pending_Approval --> Approved : Admin phê duyệt
    Pending_Approval --> Rejected : Admin từ chối
    Rejected --> Draft : Chỉnh sửa & Gửi lại
    Approved --> Hidden : Admin/Owner ẩn POI
    Hidden --> Approved : Admin hiện lại POI
```

---

## 4. Quy trình tạo và duyệt POI (Sequence Diagram)
Quy trình tương tác giữa Chủ cửa hàng, Hệ thống Admin, và AI Gemini khi tạo mới một địa điểm.

```mermaid
sequenceDiagram
    participant Owner as Chủ cửa hàng
    participant UI as Admin UI
    participant API as Backend API
    participant AI as Gemini AI Service
    participant DB as SQL Database
    participant Admin as Admin User
    
    Owner->>UI: Nhập thông tin POI (Tiếng Việt)
    UI->>API: Yêu cầu AI dịch thuật (POST /ai/generate)
    API->>AI: Gửi prompt (Dịch Vi -> En, Ja)
    AI-->>API: Trả về kết quả JSON
    API-->>UI: Trả về các bản dịch
    UI-->>Owner: Tự động điền Form tiếng Anh & Nhật
    
    Owner->>UI: Nhấn Gửi xét duyệt
    UI->>API: Lưu POI (POST /api/admin/pois)
    API->>DB: INSERT POI (Status: Pending)
    DB-->>API: OK
    API-->>UI: Thông báo thành công
    
    Admin->>UI: Mở trang Duyệt POI
    UI->>API: Lấy DS chờ duyệt (GET /pois/pending)
    API->>DB: Truy vấn POI Pending
    DB-->>API: Danh sách kết quả
    API-->>UI: Hiển thị danh sách cho Admin
    
    Admin->>UI: Nhấn Phê duyệt
    UI->>API: Cập nhật trạng thái (POST /pois/{id}/approve)
    API->>DB: UPDATE Status = Approved
    DB-->>API: Thành công
    API-->>UI: Giao diện cập nhật (Biến mất khỏi DS chờ)
```

---

## 5. Cấu trúc dữ liệu chính (Class Diagram)
Mối quan hệ giữa các thực thể cốt lõi trong Domain.

```mermaid
classDiagram
    class Poi {
        +int Id
        +string BasePoiId
        +string CategoryCode
        +double Latitude
        +double Longitude
        +PoiStatus Status
        +bool IsApproved
        +string OwnerId
        +DateTime CreatedAt
    }
    
    class PoiLocalization {
        +int Id
        +int PoiId
        +string LanguageCode
        +string Name
        +string Description
        +string AudioUrl
    }
    
    class ApplicationUser {
        +string Id
        +string FullName
        +string Email
        +bool IsApproved
    }
    
    class AnalyticsEvent {
        +int Id
        +DateTime Timestamp
        +string EventType
        +int? PoiId
    }
    
    Poi "1" -- "*" PoiLocalization : chứa nội dung
    Poi "*" -- "1" ApplicationUser : thuộc sở hữu
    Poi "1" -- "*" AnalyticsEvent : phát hiện sự kiện
```

---

## 6. Danh sách các chức năng chi tiết
### Đối với Admin:
- **Tổng quan**: Theo dõi biểu đồ lượt nghe và lượt ghé thăm POI theo giờ.
- **Duyệt POI**: Phê duyệt hoặc từ chối các địa điểm mới đăng ký.
- **Quản lý POI**: Có quyền can thiệp vào bất kỳ POI nào trên hệ thống.
- **Quản lý Owner**: Phê duyệt tài khoản cho các quán ăn tham gia hệ thống.

### Đối với Chủ quán (Shop Owner):
- **Dashboard**: Xem thống kê riêng cho quán của mình.
- **Quản lý POI cá nhân**: Chỉ xem và sửa được các POI do mình tạo ra.
- **AI Tool**: Dùng AI để dịch mô tả quán sang tiếng Anh và Nhật một cách chuyên nghiệp.
