namespace VinhKhanh.Shared.Models;

public class Poi
{
    public int Id { get; set; }
    public string BasePoiId { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
<<<<<<< HEAD
    public double Radius { get; set; } = 50; // meters
    public string? ImageUrl { get; set; }
    public int Priority { get; set; } = 0;   // higher = plays first
    public bool IsActive { get; set; } = true;
    public bool IsPremium { get; set; } = false;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Chứa thông tin 3 ngôn ngữ thay vì Flatten
=======
    public double Radius { get; set; } = 50;
    public string? ImageUrl { get; set; }
    public int Priority { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public bool IsPremium { get; set; } = false;
    public string CategoryCode { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
    public List<PoiLocalizationDto> Localizations { get; set; } = new();
}

public class PoiLocalizationDto
{
    public string LanguageCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? AudioFile { get; set; }
}
