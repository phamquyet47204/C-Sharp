using System;

namespace VinhKhanh.Domain.Entities;

public enum PaymentType
{
    AccessPass = 0,
    PremiumUpgrade = 1
}

public enum PaymentStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
    Refunded = 3
}

public class Payment
{
    public int Id { get; set; }

    // Mã giao dịch duy nhất từ cổng thanh toán
    public string TransactionId { get; set; } = string.Empty;

    // Khóa ngoại → ApplicationUser.Id
    public string UserId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public PaymentType Type { get; set; }

    public int? PoiId { get; set; }

    public PaymentStatus Status { get; set; }

    // Bằng null cho đến khi thanh toán hoàn tất và được cấp quyền truy cập
    public DateTime? ExpiryDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Thuộc tính điều hướng liên kết bảng User
    public ApplicationUser User { get; set; } = null!;
}
