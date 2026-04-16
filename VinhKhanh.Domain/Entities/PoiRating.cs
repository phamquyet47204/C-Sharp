namespace VinhKhanh.Domain.Entities;

public class PoiRating
{
    public int Id { get; set; }
    public int PoiId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public int Stars { get; set; }
    public DateTime RatedAt { get; set; } = DateTime.UtcNow;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public Poi? Poi { get; set; }
}