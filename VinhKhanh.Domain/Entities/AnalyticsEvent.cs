namespace VinhKhanh.Domain.Entities;

public class AnalyticsEvent
{
    public int Id { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string DeviceId { get; set; } = string.Empty;

    // Liên kết sự kiện với POI cụ thể (nullable để tương thích ngược)
    public int? PoiId { get; set; }

    // Loại sự kiện: "visit" | "narration"
    public string? EventType { get; set; }
}
