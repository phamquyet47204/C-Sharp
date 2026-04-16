using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VinhKhanh.Domain.Entities;
using VinhKhanh.Infrastructure.Data;

namespace VinhKhanh.Admin.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentController(AppDbContext dbContext) : ControllerBase
{
    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>POST /api/payments/initiate — Khởi tạo giao dịch Access Pass</summary>
    [HttpPost("initiate")]
    public async Task<IActionResult> Initiate([FromBody] InitiatePaymentRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.TransactionId))
            return BadRequest("TransactionId không được để trống.");

        var exists = await dbContext.Payments.AnyAsync(p => p.TransactionId == request.TransactionId, ct);
        if (exists) return Conflict(new { error = "Giao dịch đã tồn tại." });

        var payment = new Payment
        {
            TransactionId = request.TransactionId,
            UserId = CurrentUserId!,
            Amount = 1.00m,
            Type = PaymentType.AccessPass,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync(ct);

        return Ok(new { success = true, paymentId = payment.Id });
    }

    /// <summary>POST /api/payments/callback — Xác nhận thanh toán thành công</summary>
    [HttpPost("callback")]
    public async Task<IActionResult> Callback([FromBody] PaymentCallbackRequest request, CancellationToken ct)
    {
        var payment = await dbContext.Payments
            .FirstOrDefaultAsync(p => p.TransactionId == request.TransactionId, ct);

        if (payment is null) return NotFound("Không tìm thấy giao dịch.");

        payment.Status = PaymentStatus.Completed;
        payment.ExpiryDate = payment.CreatedAt.AddDays(7);
        await dbContext.SaveChangesAsync(ct);

        return Ok(new { success = true, expiryDate = payment.ExpiryDate });
    }

    /// <summary>GET /api/payments/status — Kiểm tra trạng thái Access Pass</summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var userId = CurrentUserId;
        var now = DateTime.UtcNow;

        var activePayment = await dbContext.Payments
            .Where(p => p.UserId == userId && p.Status == PaymentStatus.Completed && p.ExpiryDate > now)
            .OrderByDescending(p => p.ExpiryDate)
            .FirstOrDefaultAsync(ct);

        return Ok(new
        {
            hasActivePass = activePayment is not null,
            passExpiryDate = activePayment?.ExpiryDate
        });
    }
}

public class InitiatePaymentRequest
{
    public string TransactionId { get; set; } = string.Empty;
}

public class PaymentCallbackRequest
{
    public string TransactionId { get; set; } = string.Empty;
}
