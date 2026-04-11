namespace VinhKhanh.Shared.Models;

public class SyncRequest
{
    public DateTime LastSyncAt { get; set; } = DateTime.MinValue;
    public bool IncludeAudio { get; set; } = true;
}

public class SyncResponse
{
    public List<Poi> UpdatedPois { get; set; } = new();
    public List<int> DeletedIds { get; set; } = new();
    public DateTime ServerTime { get; set; } = DateTime.UtcNow;
}

public class NarrationEvent
{
    public long Id { get; set; }

    public int PoiId { get; set; }
    public string Language { get; set; } = "vi-VN";
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
}
