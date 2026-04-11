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
<<<<<<< HEAD
    private const int FreeTrialLimit = 3;

    /// <summary>
    /// GET /api/access/check
    /// Nhận DeviceId (query/header) hoặc JWT token.
    /// Trả về: { freeTrialUsed, freeTrialLimit, hasActivePass, passExpiryDate }
=======
    private const int FreeTrialDays = 7;
    private const int FreeTrialPoiLimit = 3; // giữ lại cho backward compat

    /// <summary>
    /// POST /api/access/register-device
    /// Lần đầu app mở → đăng ký device, lưu ngày bắt đầu trial.
    /// Nếu device đã tồn tại → trả về thông tin trial hiện tại.
    /// </summary>
    [HttpPost("register-device")]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
            return BadRequest(new { error = "DeviceId không được để trống." });

        var existing = await dbContext.DeviceTrials
            .FirstOrDefaultAsync(d => d.DeviceId == request.DeviceId, ct);

        if (existing is not null)
        {
            // Device đã đăng ký → trả về thông tin cũ
            return Ok(new
            {
                deviceId = existing.DeviceId,
                firstSeenAt = existing.FirstSeenAt,
                trialExpiresAt = existing.TrialExpiresAt,
                isTrialActive = existing.TrialExpiresAt > DateTime.UtcNow
            });
        }

        // Lần đầu → tạo mới
        var now = DateTime.UtcNow;
        var trial = new DeviceTrial
        {
            DeviceId = request.DeviceId,
            FirstSeenAt = now,
            TrialExpiresAt = now.AddDays(FreeTrialDays)
        };

        dbContext.DeviceTrials.Add(trial);
        await dbContext.SaveChangesAsync(ct);

        return Ok(new
        {
            deviceId = trial.DeviceId,
            firstSeenAt = trial.FirstSeenAt,
            trialExpiresAt = trial.TrialExpiresAt,
            isTrialActive = true
        });
    }

    /// <summary>
    /// GET /api/access/check
    /// Kiểm tra trial còn hạn không dựa trên DeviceId.
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
    /// </summary>
    [HttpGet("check")]
    public async Task<IActionResult> Check([FromQuery] string? deviceId, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var now = DateTime.UtcNow;

<<<<<<< HEAD
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
=======
        // Kiểm tra trial 7 ngày theo device
        bool isTrialActive = false;
        DateTime? trialExpiresAt = null;

        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            var trial = await dbContext.DeviceTrials
                .FirstOrDefaultAsync(d => d.DeviceId == deviceId, ct);

            if (trial is not null)
            {
                isTrialActive = trial.TrialExpiresAt > now;
                trialExpiresAt = trial.TrialExpiresAt;
            }
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
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

<<<<<<< HEAD
        return Ok(new
        {
            freeTrialUsed,
            freeTrialLimit = FreeTrialLimit,
            hasActivePass,
            passExpiryDate
        });
    }
}
=======
        // Số POI đã nghe (giữ lại cho backward compat)
        int freeTrialUsed = 0;
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            freeTrialUsed = await dbContext.FreeTrialRecords
                .Where(f => f.DeviceId == deviceId)
                .Select(f => f.PoiId).Distinct().CountAsync(ct);
        }

        return Ok(new
        {
            isTrialActive,
            trialExpiresAt,
            hasActivePass,
            passExpiryDate,
            freeTrialUsed,
            freeTrialLimit = FreeTrialPoiLimit
        });
    }
}

public class RegisterDeviceRequest
{
    public string DeviceId { get; set; } = string.Empty;
}
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
