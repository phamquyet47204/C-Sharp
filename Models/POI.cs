using SQLite;

namespace VinhKhanhFoodStreet.Models;

/// <summary>
/// Model đại diện cho một điểm tham quan/điểm nội dung (POI) trên bản đồ.
/// </summary>
public class POI
{
    /// <summary>
    /// Khóa chính tự tăng của bảng POI.
    /// </summary>
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// Tên điểm POI hiển thị cho người dùng.
    /// </summary>
    [NotNull]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Vĩ độ của POI.
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Kinh độ của POI.
    /// </summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Bán kính vùng kích hoạt (đơn vị mét).
    /// </summary>
    public double Radius { get; set; }

    /// <summary>
    /// Mô tả chi tiết nội dung điểm POI.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Đường dẫn file âm thanh thuyết minh.
    /// </summary>
    public string AudioPath { get; set; } = string.Empty;

    /// <summary>
    /// Đường dẫn hình ảnh POI (ví dụ: "dotnet_bot.png" dùng từ Resources/Images).
    /// </summary>
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>
    /// Mã ngôn ngữ của dữ liệu POI (ví dụ: vi, en, ko).
    /// </summary>
    [Indexed]
    public string LanguageCode { get; set; } = "vi";

    /// <summary>
    /// Danh mục/loại của POI (ví dụ: Oyster, Bbq, Beverage).
    /// </summary>
    public string Category { get; set; } = "All";

    /// <summary>
    /// Mức độ ưu tiên hiển thị/xử lý. Số lớn hơn có thể ưu tiên cao hơn.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Trạng thái đã tải offline của nội dung POI.
    /// </summary>
    public bool IsDownloaded { get; set; }
}
