namespace VinhKhanh.Domain.Entities;

/// <summary>
/// Lưu thông tin lần đầu thiết bị cài app.
/// Dùng để tính 7 ngày dùng thử - không bị reset khi xóa app.
/// </summary>
public class DeviceTrial
{
    public int Id { get; set; }

    /// <summary>Android ID hoặc GUID được tạo lần đầu và lưu server.</summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>Thời điểm lần đầu đăng ký (cài app).</summary>
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;

    /// <summary>Ngày hết hạn trial = FirstSeenAt + 7 ngày.</summary>
    public DateTime TrialExpiresAt { get; set; }
}
