using SQLite;

namespace VinhKhanh.Mobile.Models;

public class NarrationEvent
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int PoiId { get; set; }
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
}
