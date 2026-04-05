using SQLite;

namespace VinhKhanh.Mobile.Models;

public class PoiRecord
{
    [PrimaryKey]
    public int Id { get; set; }

    [Indexed]
    public int BasePoiId { get; set; }

    [NotNull]
    public string Name { get; set; } = string.Empty;

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Radius { get; set; } = 50;
    public string Description { get; set; } = string.Empty;
    public string AudioPath { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;

    [Indexed]
    public string LanguageCode { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsDownloaded { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPremium { get; set; } = false;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Ignore]
    public string CategoryDisplayName => Category switch
    {
        "FOOD_SNAIL"  => "Ốc & Hải sản",
        "FOOD_BBQ"    => "Đồ nướng & Lẩu",
        "FOOD_STREET" => "Ăn vặt",
        "DRINK"       => "Đồ uống",
        "UTILITY"     => "Tiện ích",
        _ => string.IsNullOrWhiteSpace(Category) ? "Khác" : Category
    };
}
