using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Graphics;
using SQLite;

namespace VinhKhanhFoodStreet.Models;

/// <summary>
/// Model đại diện cho một điểm tham quan/điểm nội dung (POI) trên bản đồ.
/// </summary>
public class POI : INotifyPropertyChanged
{
    /// <summary>
    /// Khóa chính tự tăng của bảng POI.
    /// </summary>
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// Id goc de gom cac ban dich cua cung mot quan (vi/en/ja) vao 1 doi tuong logic.
    /// </summary>
    [Indexed]
    public int BasePoiId { get; set; }

    /// <summary>
    /// Tên điểm POI hiển thị cho người dùng.
    /// </summary>
    private string _name = string.Empty;

    [NotNull]
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

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
    private string _description = string.Empty;

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    /// <summary>
    /// Đường dẫn file âm thanh thuyết minh.
    /// </summary>
    private string _audioPath = string.Empty;

    public string AudioPath
    {
        get => _audioPath;
        set => SetProperty(ref _audioPath, value);
    }

    /// <summary>
    /// Đường dẫn hình ảnh POI (ví dụ: "dotnet_bot.png" dùng từ Resources/Images).
    /// </summary>
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>
    /// Mã ngôn ngữ của dữ liệu POI (ví dụ: vi, en, ko).
    /// </summary>
    private string _languageCode = "vi";

    [Indexed]
    public string LanguageCode
    {
        get => _languageCode;
        set => SetProperty(ref _languageCode, value);
    }

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

    [Ignore]
    public int AggregateId => BasePoiId > 0 ? BasePoiId : Id;

    private int _distance;

    [Ignore]
    public int Distance
    {
        get => _distance;
        set => SetProperty(ref _distance, value);
    }

    [Ignore]
    public float Rating { get; set; } = 4.5f;

    private bool _isNearest;

    [Ignore]
    public bool IsNearest
    {
        get => _isNearest;
        set
        {
            if (SetProperty(ref _isNearest, value))
            {
                OnPropertyChanged(nameof(IsNearestBorderWidth));
                OnPropertyChanged(nameof(IsNearestCardStroke));
            }
        }
    }

    [Ignore]
    public int IsNearestBorderWidth => IsNearest ? 3 : 1;

    [Ignore]
    public Color IsNearestCardStroke => IsNearest
        ? Color.FromArgb("#FF7F50")
        : Color.FromArgb("#E0E0E0");

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetProperty<T>(ref T backingField, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(backingField, value))
        {
            return false;
        }

        backingField = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
