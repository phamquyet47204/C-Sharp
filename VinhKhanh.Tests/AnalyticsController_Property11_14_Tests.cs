// Feature: vinh-khanh-tts-missing-features, Property 11: Heatmap filter theo thời gian — chỉ trả về events trong khoảng
// Feature: vinh-khanh-tts-missing-features, Property 12: Heatmap không vượt quá 500 điểm
// Feature: vinh-khanh-tts-missing-features, Property 13: Content Performance được sắp xếp giảm dần theo lượt nghe
// Feature: vinh-khanh-tts-missing-features, Property 14: Content Performance limit được tuân thủ

using System.Security.Claims;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using VinhKhanh.Admin.Controllers;
using VinhKhanh.Application.UseCases;
using VinhKhanh.Domain.Entities;
using VinhKhanh.Domain.Interfaces;
using VinhKhanh.Infrastructure.Data;

namespace VinhKhanh.Tests;

public class AnalyticsController_Property11_14_Tests
{
    private static AppDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    private static AnalyticsController CreateController(AppDbContext dbContext)
    {
        var repoMock = new Mock<IAnalyticsRepository>();
        var visitUseCase = new AnalyticsVisitUseCase(repoMock.Object);

        var controller = new AnalyticsController(visitUseCase, dbContext);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "admin-user"),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    /// <summary>
    /// Property 11: Heatmap only returns events within the [from, to] time range.
    /// Validates: Requirements 8.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Heatmap_OnlyReturnsEventsWithinTimeRange()
    {
        var arb = Arb.ToArbitrary(
            from fromDaysAgo in Gen.Choose(5, 30)
            from toDaysAgo in Gen.Choose(1, 4)
            from insideCount in Gen.Choose(0, 10)
            from outsideCount in Gen.Choose(0, 10)
            select (fromDaysAgo, toDaysAgo, insideCount, outsideCount));

        return Prop.ForAll(arb, scenario =>
        {
            var (fromDaysAgo, toDaysAgo, insideCount, outsideCount) = scenario;
            var dbName = $"prop11_{Guid.NewGuid()}";
            using var dbContext = CreateDbContext(dbName);

            var now = DateTime.UtcNow;
            var fromDate = now.AddDays(-fromDaysAgo);
            var toDate = now.AddDays(-toDaysAgo);

            for (int i = 0; i < insideCount; i++)
            {
                var midpoint = fromDate.AddSeconds((toDate - fromDate).TotalSeconds / 2 + i);
                dbContext.AnalyticsEvents.Add(new AnalyticsEvent
                {
                    Latitude = 10.77 + i * 0.001,
                    Longitude = 106.70 + i * 0.001,
                    Timestamp = midpoint,
                    DeviceId = $"device-in-{i}"
                });
            }

            for (int i = 0; i < outsideCount; i++)
            {
                dbContext.AnalyticsEvents.Add(new AnalyticsEvent
                {
                    Latitude = 10.80 + i * 0.001,
                    Longitude = 106.80 + i * 0.001,
                    Timestamp = fromDate.AddDays(-1 - i),
                    DeviceId = $"device-out-{i}"
                });
            }

            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            var result = controller.GetHeatmap(fromDate.ToString("O"), toDate.ToString("O"), CancellationToken.None)
                .GetAwaiter().GetResult();

            if (result is not OkObjectResult okResult)
                return Prop.Label(false, $"Expected OkObjectResult but got {result.GetType().Name}");

            var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var pointCount = doc.RootElement.GetProperty("points").GetArrayLength();

            if (pointCount > insideCount)
                return Prop.Label(false,
                    $"Heatmap returned {pointCount} points but only {insideCount} events were in range. outsideCount={outsideCount}");

            return Prop.Label(true, $"OK: insideCount={insideCount}, outsideCount={outsideCount}, pointCount={pointCount}");
        });
    }

    /// <summary>
    /// Property 12: Heatmap never returns more than 500 points.
    /// Validates: Requirements 8.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Heatmap_NeverExceeds500Points()
    {
        var arb = Arb.ToArbitrary(Gen.Choose(400, 700));

        return Prop.ForAll(arb, distinctCount =>
        {
            var dbName = $"prop12_{Guid.NewGuid()}";
            using var dbContext = CreateDbContext(dbName);

            for (int i = 0; i < distinctCount; i++)
            {
                dbContext.AnalyticsEvents.Add(new AnalyticsEvent
                {
                    Latitude = Math.Round(10.0 + i * 0.0001, 4),
                    Longitude = Math.Round(106.0 + i * 0.0001, 4),
                    Timestamp = DateTime.UtcNow,
                    DeviceId = $"device-{i}"
                });
            }

            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            var result = controller.GetHeatmap(null, null, CancellationToken.None)
                .GetAwaiter().GetResult();

            if (result is not OkObjectResult okResult)
                return Prop.Label(false, $"Expected OkObjectResult but got {result.GetType().Name}");

            var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var pointCount = doc.RootElement.GetProperty("points").GetArrayLength();

            if (pointCount > 500)
                return Prop.Label(false, $"Heatmap returned {pointCount} points which exceeds 500. distinctCount={distinctCount}");

            return Prop.Label(true, $"OK: distinctCount={distinctCount}, pointCount={pointCount}");
        });
    }

    /// <summary>
    /// Property 13: Content Performance items are sorted by totalNarrations descending.
    /// Validates: Requirements 9.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ContentPerformance_IsSortedByNarrationsDescending()
    {
        var poiSpecGen =
            from poiId in Gen.Choose(1, 20)
            from visits in Gen.Choose(0, 10)
            from narrations in Gen.Choose(0, 10)
            select (poiId, visits, narrations);

        var arb = Arb.ToArbitrary(
            from count in Gen.Choose(1, 8)
            from specs in poiSpecGen.ListOf(count)
            let deduped = specs.GroupBy(s => s.poiId).Select(g => g.First()).ToList()
            where deduped.Count >= 1
            select deduped);

        return Prop.ForAll(arb, poiSpecs =>
        {
            var dbName = $"prop13_{Guid.NewGuid()}";
            using var dbContext = CreateDbContext(dbName);

            foreach (var (poiId, visits, narrations) in poiSpecs)
            {
                for (int v = 0; v < visits; v++)
                    dbContext.AnalyticsEvents.Add(new AnalyticsEvent
                    {
                        Latitude = 10.77, Longitude = 106.70,
                        Timestamp = DateTime.UtcNow,
                        DeviceId = $"d-{poiId}-v{v}",
                        PoiId = poiId,
                        EventType = "visit"
                    });

                for (int n = 0; n < narrations; n++)
                    dbContext.AnalyticsEvents.Add(new AnalyticsEvent
                    {
                        Latitude = 10.77, Longitude = 106.70,
                        Timestamp = DateTime.UtcNow,
                        DeviceId = $"d-{poiId}-n{n}",
                        PoiId = poiId,
                        EventType = "narration"
                    });
            }

            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            var result = controller.GetContentPerformance(50, null, null, CancellationToken.None)
                .GetAwaiter().GetResult();

            if (result is not OkObjectResult okResult)
                return Prop.Label(false, $"Expected OkObjectResult but got {result.GetType().Name}");

            var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var items = doc.RootElement.GetProperty("items").EnumerateArray().ToList();

            if (items.Count <= 1)
                return Prop.Label(true, $"OK: only {items.Count} item(s), trivially sorted");

            for (int i = 0; i < items.Count - 1; i++)
            {
                var current = items[i].GetProperty("totalNarrations").GetInt32();
                var next = items[i + 1].GetProperty("totalNarrations").GetInt32();
                if (current < next)
                    return Prop.Label(false,
                        $"Not sorted descending at index {i}: totalNarrations[{i}]={current} < totalNarrations[{i + 1}]={next}");
            }

            return Prop.Label(true, $"OK: {items.Count} items sorted descending by totalNarrations");
        });
    }

    /// <summary>
    /// Property 14: Content Performance respects the limit parameter (1-50).
    /// Validates: Requirements 9.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ContentPerformance_RespectsLimit()
    {
        var arb = Arb.ToArbitrary(
            from limit in Gen.Choose(1, 50)
            from extraPois in Gen.Choose(0, 10)
            select (limit, totalPois: limit + extraPois));

        return Prop.ForAll(arb, scenario =>
        {
            var (limit, totalPois) = scenario;
            var dbName = $"prop14_{Guid.NewGuid()}";
            using var dbContext = CreateDbContext(dbName);

            for (int i = 1; i <= totalPois; i++)
            {
                dbContext.AnalyticsEvents.Add(new AnalyticsEvent
                {
                    Latitude = 10.77, Longitude = 106.70,
                    Timestamp = DateTime.UtcNow,
                    DeviceId = $"device-{i}",
                    PoiId = i,
                    EventType = "narration"
                });
            }

            dbContext.SaveChanges();

            var controller = CreateController(dbContext);
            var result = controller.GetContentPerformance(limit, null, null, CancellationToken.None)
                .GetAwaiter().GetResult();

            if (result is not OkObjectResult okResult)
                return Prop.Label(false, $"Expected OkObjectResult but got {result.GetType().Name}");

            var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var itemCount = doc.RootElement.GetProperty("items").GetArrayLength();

            if (itemCount > limit)
                return Prop.Label(false, $"Returned {itemCount} items but limit={limit}. totalPois={totalPois}");

            return Prop.Label(true, $"OK: limit={limit}, totalPois={totalPois}, returned={itemCount}");
        });
    }
}
