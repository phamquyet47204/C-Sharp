using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VinhKhanh.Domain.Entities;
using VinhKhanh.Infrastructure.Data;

namespace VinhKhanh.Admin.Controllers;

[ApiController]
[Route("api/pois/{poiId:int}/ratings")]
public class PoiRatingsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetSummary([FromRoute] int poiId, [FromQuery] string? deviceId, CancellationToken ct)
    {
        var poiExists = await dbContext.Pois
            .AnyAsync(p => p.Id == poiId && p.Status == PoiStatus.Approved, ct);

        if (!poiExists)
        {
            return NotFound(new { error = "POI không tồn tại hoặc chưa được duyệt." });
        }

        var ratings = dbContext.PoiRatings.Where(r => r.PoiId == poiId);
        var count = await ratings.CountAsync(ct);
        var averageStars = count == 0
            ? 0.0
            : await ratings.AverageAsync(r => (double)r.Stars, ct);

        int? userStars = null;
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            userStars = await ratings
                .Where(r => r.DeviceId == deviceId)
                .Select(r => (int?)r.Stars)
                .FirstOrDefaultAsync(ct);
        }

        return Ok(new
        {
            poiId,
            averageStars = Math.Round(averageStars, 2),
            ratingCount = count,
            userStars
        });
    }

    [HttpPost]
    public async Task<IActionResult> UpsertRating([FromRoute] int poiId, [FromBody] SubmitPoiRatingRequest request, CancellationToken ct)
    {
        if (request.Stars is < 1 or > 5)
        {
            return BadRequest(new { error = "Số sao phải từ 1 đến 5." });
        }

        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            return BadRequest(new { error = "DeviceId là bắt buộc." });
        }

        var poi = await dbContext.Pois
            .Where(p => p.Id == poiId && p.Status == PoiStatus.Approved)
            .Select(p => new { p.Id, p.Latitude, p.Longitude })
            .FirstOrDefaultAsync(ct);

        if (poi is null)
        {
            return NotFound(new { error = "POI không tồn tại hoặc chưa được duyệt." });
        }

        var now = DateTime.UtcNow;
        var existing = await dbContext.PoiRatings
            .FirstOrDefaultAsync(r => r.PoiId == poiId && r.DeviceId == request.DeviceId, ct);

        if (existing is null)
        {
            dbContext.PoiRatings.Add(new PoiRating
            {
                PoiId = poiId,
                DeviceId = request.DeviceId.Trim(),
                Stars = request.Stars,
                RatedAt = now,
                Latitude = request.Latitude,
                Longitude = request.Longitude
            });
        }
        else
        {
            existing.Stars = request.Stars;
            existing.RatedAt = now;
            existing.Latitude = request.Latitude;
            existing.Longitude = request.Longitude;
        }

        await dbContext.SaveChangesAsync(ct);

        var ratings = dbContext.PoiRatings.Where(r => r.PoiId == poiId);
        var count = await ratings.CountAsync(ct);
        var averageStars = count == 0
            ? 0.0
            : await ratings.AverageAsync(r => (double)r.Stars, ct);

        return Ok(new
        {
            success = true,
            poiId,
            userStars = request.Stars,
            averageStars = Math.Round(averageStars, 2),
            ratingCount = count
        });
    }
}

public class SubmitPoiRatingRequest
{
    public int Stars { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
