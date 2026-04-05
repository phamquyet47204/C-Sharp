namespace VinhKhanh.Domain.Entities;

public class PoiLocalization
{
    public int Id { get; set; }
    public int PoiId { get; set; }
    public string LanguageCode { get; set; } = "vi"; // vi, ja
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? AudioUrl { get; set; }

    public Poi Poi { get; set; } = null!;
}
