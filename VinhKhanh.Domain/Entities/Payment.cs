using System;

namespace VinhKhanh.Domain.Entities;

public enum PaymentType
{
    AccessPass = 0
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

    // Unique transaction identifier from payment gateway
    public string TransactionId { get; set; } = string.Empty;

    // FK → ApplicationUser.Id
    public string UserId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public PaymentType Type { get; set; }

    public PaymentStatus Status { get; set; }

    // Null until payment is completed and access is granted
    public DateTime? ExpiryDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ApplicationUser User { get; set; } = null!;
}
