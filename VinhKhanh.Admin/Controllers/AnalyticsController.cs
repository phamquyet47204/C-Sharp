using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VinhKhanh.Application.UseCases;
using VinhKhanh.Domain.Entities;
using VinhKhanh.Infrastructure.Data;

namespace VinhKhanh.Admin.Controllers;

[ApiController]
[Route("api/analytics")]
public class AnalyticsController(AnalyticsVisitUseCase visitUseCase, AppDbContext dbContext) : ControllerBase
{
    private const int HeatmapMaxPoints = 500;
    private const double HeatmapClusterRadiusMeters = 10.0;

    [HttpPost("visit")]
    public async Task<IActionResult> LogVisit([FromBody] AnalyticsVisitCommand command, CancellationToken cancellationToken)
    {
        try
        {
            await visitUseCase.ExecuteAsync(command, cancellationToken);

            // Upsert FreeTrialRecord nếu là lần đầu nghe POI này
            if (command.PoiId.HasValue && command.EventType == "narration")
            {
                var deviceId = string.IsNullOrWhiteSpace(command.DeviceId) ? null : command.DeviceId;
                var alreadyExists = await dbContext.FreeTrialRecords
                    .AnyAsync(f => (deviceId != null && f.DeviceId == deviceId && f.PoiId == command.PoiId.Value), cancellationToken);

                if (!alreadyExists && deviceId != null)
                {
                    dbContext.FreeTrialRecords.Add(new FreeTrialRecord
                    {
                        DeviceId = deviceId,
                        PoiId = command.PoiId.Value,
                        FirstHeardAt = DateTime.UtcNow
                    });
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return Problem($"Lỗi khi lưu analytics: {ex.Message}");
        }
    }

    [HttpGet("heatmap")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetHeatmap(
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken cancellationToken)
    {
        DateTime? fromDate = null, toDate = null;

        if (!string.IsNullOrWhiteSpace(from))
        {
            if (!DateTime.TryParse(from, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedFrom))
                return BadRequest(new { error = "Định dạng ngày không hợp lệ. Sử dụng ISO 8601 (VD: 2026-01-01T00:00:00Z)" });
            fromDate = parsedFrom.ToUniversalTime();
        }

        if (!string.IsNullOrWhiteSpace(to))
        {
            if (!DateTime.TryParse(to, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedTo))
                return BadRequest(new { error = "Định dạng ngày không hợp lệ. Sử dụng ISO 8601 (VD: 2026-01-01T00:00:00Z)" });
            toDate = parsedTo.ToUniversalTime();
        }

        var query = dbContext.AnalyticsEvents.AsQueryable();
        if (fromDate.HasValue) query = query.Where(e => e.Timestamp >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(e => e.Timestamp <= toDate.Value);

        var events = await query.Select(e => new { e.Latitude, e.Longitude }).ToListAsync(cancellationToken);

        // Cluster events within HeatmapClusterRadiusMeters
        var points = events
            .GroupBy(e => (
                lat: Math.Round(e.Latitude, 4),
                lng: Math.Round(e.Longitude, 4)))
            .Select(g => new { lat = g.Key.lat, lng = g.Key.lng, intensity = g.Count() })
            .OrderByDescending(p => p.intensity)
            .Take(HeatmapMaxPoints)
            .ToList();

        return Ok(new { points, total = points.Count });
    }

    [HttpGet("content-performance")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetContentPerformance(
        [FromQuery] int limit = 10,
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
        limit = Math.Clamp(limit, 1, 50);

        DateTime? fromDate = null, toDate = null;

        if (!string.IsNullOrWhiteSpace(from))
        {
            if (!DateTime.TryParse(from, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedFrom))
                return BadRequest(new { error = "Định dạng ngày không hợp lệ. Sử dụng ISO 8601 (VD: 2026-01-01T00:00:00Z)" });
            fromDate = parsedFrom.ToUniversalTime();
        }

        if (!string.IsNullOrWhiteSpace(to))
        {
            if (!DateTime.TryParse(to, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedTo))
                return BadRequest(new { error = "Định dạng ngày không hợp lệ. Sử dụng ISO 8601 (VD: 2026-01-01T00:00:00Z)" });
            toDate = parsedTo.ToUniversalTime();
        }

        var eventsQuery = dbContext.AnalyticsEvents.Where(e => e.PoiId.HasValue);
        if (fromDate.HasValue) eventsQuery = eventsQuery.Where(e => e.Timestamp >= fromDate.Value);
        if (toDate.HasValue) eventsQuery = eventsQuery.Where(e => e.Timestamp <= toDate.Value);

        var grouped = await eventsQuery
            .GroupBy(e => e.PoiId!.Value)
            .Select(g => new
            {
                poiId = g.Key,
                totalVisits = g.Count(e => e.EventType == "visit"),
                totalNarrations = g.Count(e => e.EventType == "narration")
            })
            .OrderByDescending(g => g.totalNarrations)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var poiIds = grouped.Select(g => g.poiId).ToList();
        var pois = await dbContext.Pois
            .Include(p => p.Localizations)
            .Where(p => poiIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        var items = grouped.Select((g, idx) =>
        {
            var poi = pois.FirstOrDefault(p => p.Id == g.poiId);
            var viName = poi?.Localizations.FirstOrDefault(l => l.LanguageCode == "vi")?.Name ?? string.Empty;
            return new
            {
                g.poiId,
                poiName = viName,
                g.totalVisits,
                g.totalNarrations,
                rank = idx + 1
            };
        });

        return Ok(new { items, total = grouped.Count });
        }
        catch (Exception ex)
        {
            return Problem($"content-performance error: {ex.Message} | {ex.InnerException?.Message}");
        }
    }
}
