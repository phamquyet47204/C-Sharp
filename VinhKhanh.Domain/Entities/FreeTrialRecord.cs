using System;

namespace VinhKhanh.Domain.Entities;

public class FreeTrialRecord
{
    public int Id { get; set; }

    // Giá trị null nếu là khách ẩn danh
    public string? UserId { get; set; }

    // Giá trị null khi người dùng đã đăng nhập (định danh bằng UserId thay thế)
    public string? DeviceId { get; set; }

    public int PoiId { get; set; }

    public DateTime FirstHeardAt { get; set; } = DateTime.UtcNow;
}
