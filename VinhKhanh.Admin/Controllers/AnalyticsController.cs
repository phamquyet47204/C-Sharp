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

        var poiRefs = await GetPoiReferencesAsync(cancellationToken);
        var points = BuildHeatmapPoints(events, DateTime.UtcNow, useRecencyWeight: false, poiRefs);
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

        var poiRefs = await GetPoiReferencesAsync(cancellationToken);
        var points = BuildHeatmapPoints(events, DateTime.UtcNow, useRecencyWeight: false, poiRefs);
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

        var poiRefs = await GetPoiReferencesAsync(cancellationToken);

        // Gom nhóm sự kiện theo ngày
        var groupedByDay = events
            .GroupBy(e => DateOnly.FromDateTime(e.Timestamp))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Tạo danh sách liên tục các ngày từ fromDay đến toDay
        var allDays = new List<object>();
        for (var current = fromDay; current <= toDay; current = current.AddDays(1))
        {
            var dayEvents = groupedByDay.TryGetValue(current, out var evts) ? evts : new List<HeatmapEvent>();
            allDays.Add(new
            {
                day = current.ToString("yyyy-MM-dd"),
                totalEvents = dayEvents.Count,
                points = BuildHeatmapPoints(dayEvents, DateTime.UtcNow, useRecencyWeight: false, poiRefs)
            });
        }

        return Ok(new { from = fromDay.ToString("yyyy-MM-dd"), to = toDay.ToString("yyyy-MM-dd"), days = allDays });
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

        var poiRefs = await GetPoiReferencesAsync(cancellationToken);
        var points = BuildHeatmapPoints(events, now, useRecencyWeight: true, poiRefs);
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

    private async Task<List<(double Lat, double Lng, string Name, double Radius)>> GetPoiReferencesAsync(CancellationToken ct)
    {
        var pois = await dbContext.Pois
            .Include(p => p.Localizations)
            .Select(p => new
            {
                p.Latitude,
                p.Longitude,
                Radius = p.Radius > 0 ? p.Radius : 50.0,
                Name = p.Localizations.Where(l => l.LanguageCode == "vi").Select(l => l.Name).FirstOrDefault()
            })
            .ToListAsync(ct);

        return pois.Select(p => (p.Latitude, p.Longitude, p.Name ?? "Khu vực lân cận", p.Radius)).ToList();
    }

    private static List<object> BuildHeatmapPoints(
        List<HeatmapEvent> events,
        DateTime now,
        bool useRecencyWeight,
        List<(double Lat, double Lng, string Name, double Radius)>? poiRefs = null)
    {
        const double areaPerCell = 121.0; 

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

        // Bước 1: Xác định mỗi User thuộc POI nào (nếu nằm trong bán kính)
        var userPoiAssignments = userPositions.Select(u =>
        {
            var nearestPoi = poiRefs?
                .Select(p => new { p.Lat, p.Lng, p.Name, p.Radius, Dist = CalculateDistance(p.Lat, p.Lng, u.Lat, u.Lng) })
                .Where(p => p.Dist <= (p.Radius / 1000.0)) // Trong bán kính (mét -> km)
                .OrderBy(p => p.Dist)
                .FirstOrDefault();

            return new { u.Lat, u.Lng, u.EventCount, Poi = nearestPoi };
        }).ToList();

        // Bước 2: Gom nhóm. Những người thuộc POI sẽ bị "hút" về tâm POI.
        // Những người không thuộc POI sẽ gom theo lưới 44m như cũ.
        var finalPoints = userPoiAssignments
            .GroupBy(x => x.Poi != null 
                ? $"poi:{x.Poi.Name}" 
                : $"grid:{Math.Round(x.Lat * 2500.0) / 2500.0}:{Math.Round(x.Lng * 2500.0) / 2500.0}")
            .Select(g =>
            {
                var first = g.First();
                var lat = first.Poi?.Lat ?? Math.Round(first.Lat * 2500.0) / 2500.0;
                var lng = first.Poi?.Lng ?? Math.Round(first.Lng * 2500.0) / 2500.0;
                var poiName = first.Poi?.Name;

                var userWeights = g.Sum(x => Math.Min(1.1, 1.0 + (x.EventCount - 1) * 0.05));
                var intensity = userWeights;
                var density = (userWeights * 100.0) / areaPerCell;

                return new
                {
                    lat,
                    lng,
                    intensity = Math.Round(intensity, 2),
                    density = Math.Round(density, 2),
                    peopleCount = g.Count(),
                    weightedPeople = Math.Round(userWeights, 1),
                    poiName
                };
            })
            .OrderByDescending(p => p.peopleCount)
            .Take(HeatmapMaxPoints)
            .Cast<object>()
            .ToList();

        return finalPoints;
    }

    private static double CalculateDistance(double lat1, double lng1, double lat2, double lng2)
    {
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLng = (lng2 - lng1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return 6371 * c; // Km
    }
}
