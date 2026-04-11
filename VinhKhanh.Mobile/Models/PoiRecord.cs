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
<<<<<<< HEAD
=======

    [Ignore]
    public string CategoryBadgeColor => Category switch
    {
        "FOOD_SNAIL"  => "#E3F2FD",
        "FOOD_BBQ"    => "#FFF3E0",
        "FOOD_STREET" => "#FFF8E1",
        "DRINK"       => "#E8F5E9",
        "UTILITY"     => "#F3E5F5",
        _             => "#F5F5F5"
    };

    [Ignore]
    public string CategoryTextColor => Category switch
    {
        "FOOD_SNAIL"  => "#1565C0",
        "FOOD_BBQ"    => "#E65100",
        "FOOD_STREET" => "#F57F17",
        "DRINK"       => "#2E7D32",
        "UTILITY"     => "#6A1B9A",
        _             => "#757575"
    };
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
}
