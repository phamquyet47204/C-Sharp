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
        DateTime? trialExpiryDate = null;
        bool hasActivePass = false;
        bool isTrialActive = false;

        // 1. Kiểm tra Trial từ thiết bị
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            var trial = await dbContext.DeviceTrials
                .FirstOrDefaultAsync(d => d.DeviceId == deviceId, ct);

            if (trial is not null)
            {
                trialExpiryDate = trial.ExpiryDate;
                isTrialActive = trial.ExpiryDate > now;
                
                // Update last checked
                trial.LastCheckedAt = now;
                await dbContext.SaveChangesAsync(ct);
            }
        }

        // 2. Kiểm tra Access Pass mua thực tế
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
            hasActivePass = hasActivePass || isTrialActive,
            passExpiryDate = hasActivePass ? passExpiryDate : trialExpiryDate,
            isTrial = isTrialActive && !hasActivePass,
            trialRemainingDays = trialExpiryDate.HasValue && trialExpiryDate > now 
                ? (int)(trialExpiryDate.Value - now).TotalDays 
                : 0
        });
    }

    /// <summary>
    /// POST /api/access/start-trial?deviceId=...
    /// </summary>
    [HttpPost("start-trial")]
    public async Task<IActionResult> StartTrial([FromQuery] string deviceId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return BadRequest("DeviceId is required");

        var existing = await dbContext.DeviceTrials
            .FirstOrDefaultAsync(d => d.DeviceId == deviceId, ct);

        if (existing is not null)
            return BadRequest("Trial already started for this device");

        var now = DateTime.UtcNow;
        var trial = new DeviceTrial
        {
            DeviceId = deviceId,
            TrialStartDate = now,
            ExpiryDate = now.AddDays(7),
            LastCheckedAt = now
        };

        dbContext.DeviceTrials.Add(trial);
        await dbContext.SaveChangesAsync(ct);

        return Ok(new
        {
            success = true,
            expiryDate = trial.ExpiryDate,
            remainingDays = 7
        });
    }
}
