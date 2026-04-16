using System;
using System.ComponentModel.DataAnnotations;

namespace VinhKhanh.Domain.Entities;

public class DeviceTrial
{
    [Key]
    public string DeviceId { get; set; } = string.Empty;

    public DateTime TrialStartDate { get; set; } = DateTime.UtcNow;

    public DateTime ExpiryDate { get; set; }

    public DateTime? LastCheckedAt { get; set; }
}
