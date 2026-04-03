using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Graphics;
using VinhKhanhFoodStreet.Configuration;
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
    private string _imagePath = string.Empty;

    public string ImagePath
    {
        get => _imagePath;
        set
        {
            if (SetProperty(ref _imagePath, value))
            {
                OnPropertyChanged(nameof(PoiImageSource));
            }
        }
    }

    /// <summary>
    /// Nguồn ảnh đã được chuẩn hóa để UI dùng trực tiếp, hỗ trợ cả ảnh local và ảnh từ URL.
    /// </summary>
    [Ignore]
    public ImageSource PoiImageSource => CreateImageSource(ImagePath);

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
    /// Ten hien thi than thien voi nguoi dung cho category.
    /// </summary>
    [Ignore]
    public string CategoryDisplayName => Category switch
    {
        "FOOD_SNAIL" => "Ốc & Hải sản",
        "FOOD_BBQ" => "Đồ nướng & Lẩu",
        "FOOD_STREET" => "Ăn vặt",
        "DRINK" => "Đồ uống",
        "UTILITY" => "Tiện ích",
        "OYSTER" => "Ốc & Hải sản",
        "BBQ" => "Đồ nướng & Lẩu",
        "BEVERAGE" => "Đồ uống",
        _ => string.IsNullOrWhiteSpace(Category) ? "Khác" : Category
    };

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

    /// <summary>
    /// Text hien thi cho nut nghe thuyet minh trong card danh sach.
    /// Thuoc tinh UI runtime, khong luu DB.
    /// </summary>
    private string _playButtonText = "Nghe";

    [Ignore]
    public string PlayButtonText
    {
        get => _playButtonText;
        set => SetProperty(ref _playButtonText, value);
    }

    /// <summary>
    /// Text hien thi cho nut dan duong trong card danh sach.
    /// Thuoc tinh UI runtime, khong luu DB.
    /// </summary>
    private string _navigateButtonText = "Den";

    [Ignore]
    public string NavigateButtonText
    {
        get => _navigateButtonText;
        set => SetProperty(ref _navigateButtonText, value);
    }

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

    private static ImageSource CreateImageSource(string? imagePath)
    {
        var normalizedPath = imagePath?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return ImageSource.FromFile("dotnet_bot.png");
        }

        if (Uri.TryCreate(normalizedPath, UriKind.Absolute, out var absoluteUri))
        {
            absoluteUri = NormalizeAndroidLoopbackUri(absoluteUri);
            return ImageSource.FromUri(absoluteUri);
        }

        if (normalizedPath.StartsWith("/", StringComparison.Ordinal) ||
            normalizedPath.StartsWith("media/", StringComparison.OrdinalIgnoreCase))
        {
            var baseUri = new Uri(AppConfig.BaseApiUrl, UriKind.Absolute);
            var relativePath = normalizedPath.StartsWith("/") ? normalizedPath[1..] : normalizedPath;
            return ImageSource.FromUri(new Uri(baseUri, relativePath));
        }

        return ImageSource.FromFile(normalizedPath);
    }

    private static Uri NormalizeAndroidLoopbackUri(Uri uri)
    {
        if (DeviceInfo.Current.Platform != DevicePlatform.Android)
        {
            return uri;
        }

        if (!string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Host, "::1", StringComparison.OrdinalIgnoreCase))
        {
            return uri;
        }

        var builder = new UriBuilder(uri)
        {
            Host = "10.0.2.2"
        };

        return builder.Uri;
    }
}
