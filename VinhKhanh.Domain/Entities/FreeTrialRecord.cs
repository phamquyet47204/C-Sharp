using System;

namespace VinhKhanh.Domain.Entities;

public class FreeTrialRecord
{
    public int Id { get; set; }

    // Null for anonymous users
    public string? UserId { get; set; }

    // Null when user is logged in (identified by UserId instead)
    public string? DeviceId { get; set; }

    public int PoiId { get; set; }

    public DateTime FirstHeardAt { get; set; } = DateTime.UtcNow;
}
