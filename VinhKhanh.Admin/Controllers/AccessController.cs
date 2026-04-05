using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VinhKhanh.Domain.Entities;
using VinhKhanh.Infrastructure.Data;

namespace VinhKhanh.Admin.Controllers;

[ApiController]
[Route("api/access")]
public class AccessController(AppDbContext dbContext) : ControllerBase
{
    private const int FreeTrialLimit = 3;

    /// <summary>
    /// GET /api/access/check
    /// Nhận DeviceId (query/header) hoặc JWT token.
    /// Trả về: { freeTrialUsed, freeTrialLimit, hasActivePass, passExpiryDate }
    /// </summary>
    [HttpGet("check")]
    public async Task<IActionResult> Check([FromQuery] string? deviceId, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var now = DateTime.UtcNow;

        // Đếm số POI duy nhất đã nghe trong Free Trial
        int freeTrialUsed;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            freeTrialUsed = await dbContext.FreeTrialRecords
                .Where(f => f.UserId == userId)
                .Select(f => f.PoiId)
                .Distinct()
                .CountAsync(ct);
        }
        else if (!string.IsNullOrWhiteSpace(deviceId))
        {
            freeTrialUsed = await dbContext.FreeTrialRecords
                .Where(f => f.DeviceId == deviceId)
                .Select(f => f.PoiId)
                .Distinct()
                .CountAsync(ct);
        }
        else
        {
            freeTrialUsed = 0;
        }

        // Kiểm tra Access Pass còn hạn
        DateTime? passExpiryDate = null;
        bool hasActivePass = false;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var activePayment = await dbContext.Payments
                .Where(p => p.UserId == userId && p.Status == PaymentStatus.Completed && p.ExpiryDate > now)
                .OrderByDescending(p => p.ExpiryDate)
                .FirstOrDefaultAsync(ct);

            if (activePayment is not null)
            {
                hasActivePass = true;
                passExpiryDate = activePayment.ExpiryDate;
            }
        }

        return Ok(new
        {
            freeTrialUsed,
            freeTrialLimit = FreeTrialLimit,
            hasActivePass,
            passExpiryDate
        });
    }
}
