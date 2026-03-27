namespace VinhKhanh.Shared.Models;

public class SyncRequest
{
    public DateTime LastSyncAt { get; set; }
}

public class SyncResponse
{
    public List<Poi> UpdatedPois { get; set; } = [];
    public List<int> DeletedIds { get; set; } = [];
    public DateTime ServerTime { get; set; } = DateTime.UtcNow;
}

public class NarrationEvent
{
    public int PoiId { get; set; }
    public string Language { get; set; } = "vi-VN";
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
}
