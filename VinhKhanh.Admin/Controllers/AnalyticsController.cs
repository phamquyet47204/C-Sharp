using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using VinhKhanh.Admin.Hubs;
using VinhKhanh.Application.UseCases;
using VinhKhanh.Domain.Entities;
using VinhKhanh.Infrastructure.Data;

namespace VinhKhanh.Admin.Controllers;

[ApiController]
[Route("api/analytics")]
public class AnalyticsController(
    AnalyticsVisitUseCase visitUseCase,
    AppDbContext dbContext,
    IHubContext<AnalyticsHub> analyticsHub) : ControllerBase
{
    private static DateTime _lastRealtimePush = DateTime.MinValue;
    private static readonly object _pushLock = new();

    private sealed class HeatmapEvent
    {
        public string? DeviceId { get; init; }
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public DateTime Timestamp { get; init; }
    }

    private const int HeatmapMaxPoints = 500;
    private static readonly TimeSpan OnlineThreshold = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RealtimeWindow = TimeSpan.FromSeconds(45);

    [HttpGet("online-count")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetOnlineCount(CancellationToken cancellationToken)
    {
        var count = await GetOnlineUserCountInternal(cancellationToken);
        return Ok(new { onlineCount = count, measuredAt = DateTime.UtcNow });
    }

    [HttpGet("realtime-overview")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetRealtimeOverview(CancellationToken cancellationToken)
    {
        var payload = await BuildRealtimePayloadAsync(cancellationToken);
        return Ok(payload);
    }

    [HttpPost("visit")]
    public async Task<IActionResult> LogVisit([FromBody] AnalyticsVisitCommand command, CancellationToken cancellationToken)
    {
        try
        {
            await visitUseCase.ExecuteAsync(command, cancellationToken);

            if (command.PoiId.HasValue && command.EventType == "narration")
            {
                var deviceId = string.IsNullOrWhiteSpace(command.DeviceId) ? null : command.DeviceId;
                var alreadyExists = await dbContext.FreeTrialRecords
                    .AnyAsync(f => deviceId != null && f.DeviceId == deviceId && f.PoiId == command.PoiId.Value, cancellationToken);

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

            await PublishRealtimeUpdateAsync(cancellationToken);
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
        var (fromDate, toDate, error) = ParseRange(from, to);
        if (error is not null) return BadRequest(new { error });

        var query = dbContext.AnalyticsEvents.AsQueryable();
        if (fromDate.HasValue) query = query.Where(e => e.Timestamp >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(e => e.Timestamp <= toDate.Value);

        // Lọc bỏ dữ liệu từ các POI đã bị xoá
        query = query.Where(e => e.PoiId == null || dbContext.Pois.Any(p => p.Id == e.PoiId));

        var events = await query
            .Select(e => new HeatmapEvent { DeviceId = e.DeviceId, Latitude = e.Latitude, Longitude = e.Longitude, Timestamp = e.Timestamp })
            .ToListAsync(cancellationToken);

        var points = BuildHeatmapPoints(events, DateTime.UtcNow, useRecencyWeight: false);
        return Ok(new { points, total = points.Count });
    }

    [HttpGet("heatmap/daily")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetHeatmapByDay([FromQuery] string date, CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParse(date, out var day))
        {
            return BadRequest(new { error = "Ngày không hợp lệ. Dùng định dạng yyyy-MM-dd." });
        }

        var from = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = day.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var events = await dbContext.AnalyticsEvents
            .Where(e => e.Timestamp >= from && e.Timestamp <= to)
            .Where(e => e.PoiId == null || dbContext.Pois.Any(p => p.Id == e.PoiId)) // Filter deleted
            .Select(e => new HeatmapEvent { DeviceId = e.DeviceId, Latitude = e.Latitude, Longitude = e.Longitude, Timestamp = e.Timestamp })
            .ToListAsync(cancellationToken);

        var points = BuildHeatmapPoints(events, DateTime.UtcNow, useRecencyWeight: false);
        return Ok(new { day = day.ToString("yyyy-MM-dd"), points, total = points.Count });
    }

    [HttpGet("heatmap/history")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetHeatmapHistory(
        [FromQuery] string from,
        [FromQuery] string to,
        CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParse(from, out var fromDay) || !DateOnly.TryParse(to, out var toDay) || fromDay > toDay)
        {
            return BadRequest(new { error = "Khoảng ngày không hợp lệ. Dùng yyyy-MM-dd và from <= to." });
        }

        var fromDate = fromDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toDate = toDay.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var events = await dbContext.AnalyticsEvents
            .Where(e => e.Timestamp >= fromDate && e.Timestamp <= toDate)
            .Where(e => e.PoiId == null || dbContext.Pois.Any(p => p.Id == e.PoiId)) // Filter deleted
            .Select(e => new HeatmapEvent { DeviceId = e.DeviceId, Latitude = e.Latitude, Longitude = e.Longitude, Timestamp = e.Timestamp })
            .ToListAsync(cancellationToken);

        var grouped = events
            .GroupBy(e => DateOnly.FromDateTime(e.Timestamp))
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                day = g.Key.ToString("yyyy-MM-dd"),
                totalEvents = g.Count(),
                points = BuildHeatmapPoints(g.ToList(), DateTime.UtcNow, useRecencyWeight: false)
            })
            .ToList();

        return Ok(new { from = fromDay.ToString("yyyy-MM-dd"), to = toDay.ToString("yyyy-MM-dd"), days = grouped });
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
            var (fromDate, toDate, error) = ParseRange(from, to);
            if (error is not null) return BadRequest(new { error });

            var eventsQuery = dbContext.AnalyticsEvents.Where(e => e.PoiId.HasValue);
            if (fromDate.HasValue) eventsQuery = eventsQuery.Where(e => e.Timestamp >= fromDate.Value);
            if (toDate.HasValue) eventsQuery = eventsQuery.Where(e => e.Timestamp <= toDate.Value);

            // Chỉ lấy dữ liệu của các POI hiện còn tồn tại
            eventsQuery = eventsQuery.Where(e => dbContext.Pois.Any(p => p.Id == e.PoiId));

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

    private async Task<int> GetOnlineUserCountInternal(CancellationToken cancellationToken)
    {
        var threshold = DateTime.UtcNow.Subtract(OnlineThreshold);
        
        // Lấy tất cả sự kiện trong khoảng thời gian threshold
        var recentEvents = await dbContext.AnalyticsEvents
            .Where(e => e.Timestamp >= threshold)
            .OrderByDescending(e => e.Timestamp)
            .ThenByDescending(e => e.Id) // Id lớn hơn/mới hơn sẽ thắng nếu trùng giây
            .Select(e => new { e.DeviceId, e.EventType, e.Timestamp })
            .ToListAsync(cancellationToken);

        // Gom nhóm theo thiết bị, lấy sự kiện mới nhất (dựa trên sắp xếp ở trên), và đếm những thiết bị đang online
        return recentEvents
            .GroupBy(e => e.DeviceId)
            .Select(g => g.First()) // Đã OrderByDescending ở trên nên First() là cái mới nhất
            .Count(e => e.EventType != "app_offline");
    }

    private async Task<object> BuildRealtimePayloadAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var from = now.Subtract(RealtimeWindow);
        var onlineThreshold = now.Subtract(OnlineThreshold);

        // Lấy dữ liệu thô để lọc online
        var eventsRaw = await dbContext.AnalyticsEvents
            .Where(e => e.Timestamp >= from)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync(cancellationToken);

        // Xác định danh sách DeviceId đang thực sự online (trong ngưỡng threshold và không có app_offline mới nhất)
        var onlineDeviceIds = eventsRaw
            .GroupBy(e => e.DeviceId)
            .Select(g => g.First())
            .Where(e => e.Timestamp >= onlineThreshold && e.EventType != "app_offline")
            .Select(e => e.DeviceId)
            .ToHashSet();

        // Chỉ giữ lại dữ liệu heatmap của những người đang online
        var events = eventsRaw
            .Where(e => onlineDeviceIds.Contains(e.DeviceId))
            .Select(e => new HeatmapEvent { DeviceId = e.DeviceId, Latitude = e.Latitude, Longitude = e.Longitude, Timestamp = e.Timestamp })
            .ToList();

        var points = BuildHeatmapPoints(events, now, useRecencyWeight: true);
        var onlineCount = await GetOnlineUserCountInternal(cancellationToken);

        return new
        {
            windowMinutes = (int)RealtimeWindow.TotalMinutes,
            onlineCount,
            points,
            total = points.Count,
            measuredAt = now
        };
    }

    private async Task PublishRealtimeUpdateAsync(CancellationToken cancellationToken)
    {
        lock (_pushLock)
        {
            var now = DateTime.UtcNow;
            if (now - _lastRealtimePush < TimeSpan.FromSeconds(1))
            {
                return; // Throttled: Giảm tải cho server và dashboard
            }
            _lastRealtimePush = now;
        }

        var payload = await BuildRealtimePayloadAsync(cancellationToken);
        await analyticsHub.Clients.Group(AnalyticsHub.AdminGroup).SendAsync("analytics:realtime", payload, cancellationToken);
    }

    private static (DateTime? from, DateTime? to, string? error) ParseRange(string? from, string? to)
    {
        DateTime? fromDate = null;
        DateTime? toDate = null;

        if (!string.IsNullOrWhiteSpace(from))
        {
            if (!DateTime.TryParse(from, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedFrom))
                return (null, null, "Định dạng ngày không hợp lệ. Sử dụng ISO 8601 (VD: 2026-01-01T00:00:00Z)");
            fromDate = parsedFrom.ToUniversalTime();
        }

        if (!string.IsNullOrWhiteSpace(to))
        {
            if (!DateTime.TryParse(to, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedTo))
                return (null, null, "Định dạng ngày không hợp lệ. Sử dụng ISO 8601 (VD: 2026-01-01T00:00:00Z)");
            toDate = parsedTo.ToUniversalTime();
        }

        return (fromDate, toDate, null);
    }

    private static List<object> BuildHeatmapPoints(
        List<HeatmapEvent> events,
        DateTime now,
        bool useRecencyWeight)
    {
        // 4 decimal places of lat/lng is roughly 11m x 11m = 121 m2.
        // We will calculate density as People / 100m2.
        const double areaPerCell = 121.0; 

        // Bước 1: Gom nhóm theo DeviceId để mỗi người chỉ là 1 điểm duy nhất (tránh chồng lấn do nhảy GPS)
        var userPositions = events
            .Where(e => Math.Abs(e.Latitude) > 0.000001 && Math.Abs(e.Longitude) > 0.000001)
            .GroupBy(e => e.DeviceId)
            .Select(ug => new
            {
                DeviceId = ug.Key,
                Lat = ug.Average(e => e.Latitude),
                Lng = ug.Average(e => e.Longitude),
                EventCount = ug.Count()
            })
            .ToList();

        // Bước 2: Chia lưới và tính mật độ dựa trên danh sách người dùng đã được làm sạch
        return userPositions
            .GroupBy(u => (lat: Math.Round(u.Lat, 4), lng: Math.Round(u.Lng, 4)))
            .Select(g =>
            {
                // Áp dụng công thức: Density = Tổng (Trọng số của từng User)
                var userWeights = g.Sum(u => Math.Min(1.1, 1.0 + (u.EventCount - 1) * 0.05));

                // Intensity dùng cho visual: lấy số bản ghi thô để đảm bảo độ rực rỡ
                var intensity = userWeights; 

                // Density = Tổng trọng số / diện tích (quy đổi về 100m2)
                var density = (userWeights * 100.0) / areaPerCell;

                return new
                {
                    lat = g.Key.lat,
                    lng = g.Key.lng,
                    intensity = Math.Round(intensity, 2),
                    density = Math.Round(density, 2),
                    peopleCount = g.Count(),
                    weightedPeople = Math.Round(userWeights, 1)
                };
            })
            .OrderByDescending(p => p.density)
            .Take(HeatmapMaxPoints)
            .Cast<object>()
            .ToList();
    }
}
