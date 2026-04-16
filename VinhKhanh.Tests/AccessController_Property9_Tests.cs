// Feature: vinh-khanh-tts-missing-features, Property 9: Free Trial boundary — đúng 3 POI duy nhất

using System.Security.Claims;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VinhKhanh.Admin.Controllers;
using VinhKhanh.Domain.Entities;
using VinhKhanh.Infrastructure.Data;

namespace VinhKhanh.Tests;

/// <summary>
/// Property 9: Free Trial boundary — đúng 3 POI duy nhất
/// Validates: Yêu cầu 4.2, 4.3
/// </summary>
public class AccessController_Property9_Tests
{
    private static AppDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    private static AccessController CreateControllerWithUser(AppDbContext dbContext, string? userId)
    {
        var controller = new AccessController(dbContext);
        var claims = new List<Claim>();
        if (userId is not null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        var identity = new ClaimsIdentity(claims, userId is not null ? "TestAuth" : "");
        var principal = new ClaimsPrincipal(identity);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return controller;
    }

    private static readonly Gen<int> PoiIdGen = Gen.Choose(1, 5);

    private static readonly Gen<string> UserIdGen =
        from prefix in Gen.Elements("user-A", "user-B", "user-C")
        from suffix in Gen.Choose(1, 99)
        select $"{prefix}-{suffix}";

    private static readonly Gen<string> DeviceIdGen =
        from prefix in Gen.Elements("dev-X", "dev-Y", "dev-Z")
        from suffix in Gen.Choose(1, 99)
        select $"{prefix}-{suffix}";

    private static readonly Arbitrary<(string userId, List<int> poiIds)> UserScenarioArb =
        Arb.ToArbitrary(
            from userId in UserIdGen
            from count in Gen.Choose(0, 10)
            from poiIds in PoiIdGen.ListOf(count)
            select (userId, poiIds.ToList()));

    private static readonly Arbitrary<(string deviceId, List<int> poiIds)> DeviceScenarioArb =
        Arb.ToArbitrary(
            from deviceId in DeviceIdGen
            from count in Gen.Choose(0, 10)
            from poiIds in PoiIdGen.ListOf(count)
            select (deviceId, poiIds.ToList()));

    /// <summary>
    /// For any authenticated user with any list of FreeTrialRecords,
    /// freeTrialUsed must equal the count of distinct PoiId values — not total records.
    /// Validates: Requirements 4.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FreeTrialUsed_EqualsDistinctPoiCount_ForAuthenticatedUser()
    {
        return Prop.ForAll(UserScenarioArb, scenario =>
        {
            var (userId, poiIds) = scenario;
            var dbName = $"prop9a_{Guid.NewGuid()}";
            using var dbContext = CreateDbContext(dbName);

            foreach (var poiId in poiIds.Distinct())
            {
                dbContext.FreeTrialRecords.Add(new FreeTrialRecord
                {
                    UserId = userId,
                    PoiId = poiId,
                    FirstHeardAt = DateTime.UtcNow
                });
            }
            dbContext.SaveChanges();

            var expectedDistinct = poiIds.Distinct().Count();
            var controller = CreateControllerWithUser(dbContext, userId);

            var result = controller.Check(deviceId: null, ct: CancellationToken.None)
                .GetAwaiter().GetResult();

            if (result is not OkObjectResult okResult)
                return Prop.Label(false, $"Expected OkObjectResult but got {result.GetType().Name}");

            var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            var doc = System.Text.Json.JsonDocument.Parse(json).RootElement;

            var freeTrialUsed = doc.GetProperty("freeTrialUsed").GetInt32();

            if (freeTrialUsed != expectedDistinct)
                return Prop.Label(false,
                    $"freeTrialUsed={freeTrialUsed} but expected distinct={expectedDistinct} " +
                    $"(total records={poiIds.Count}, userId='{userId}')");

            return Prop.Label(true,
                $"OK: userId='{userId}', totalRecords={poiIds.Count}, distinctPois={expectedDistinct}");
        });
    }

    /// <summary>
    /// Regardless of how many FreeTrialRecords exist, freeTrialLimit must always be 3.
    /// Validates: Requirements 4.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FreeTrialLimit_IsAlways3()
    {
        return Prop.ForAll(UserScenarioArb, scenario =>
        {
            var (userId, poiIds) = scenario;
            var dbName = $"prop9b_{Guid.NewGuid()}";
            using var dbContext = CreateDbContext(dbName);

            foreach (var poiId in poiIds.Distinct())
            {
                dbContext.FreeTrialRecords.Add(new FreeTrialRecord
                {
                    UserId = userId,
                    PoiId = poiId,
                    FirstHeardAt = DateTime.UtcNow
                });
            }
            dbContext.SaveChanges();

            var controller = CreateControllerWithUser(dbContext, userId);
            var result = controller.Check(deviceId: null, ct: CancellationToken.None)
                .GetAwaiter().GetResult();

            if (result is not OkObjectResult okResult)
                return Prop.Label(false, $"Expected OkObjectResult but got {result.GetType().Name}");

            var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            var doc = System.Text.Json.JsonDocument.Parse(json).RootElement;

            var freeTrialLimit = doc.GetProperty("freeTrialLimit").GetInt32();

            if (freeTrialLimit != 3)
                return Prop.Label(false, $"freeTrialLimit={freeTrialLimit} but expected 3");

            return Prop.Label(true, $"OK: freeTrialLimit=3, records={poiIds.Count}");
        });
    }

    /// <summary>
    /// When no userId is provided (anonymous), freeTrialUsed counts distinct POI IDs by DeviceId.
    /// Validates: Requirements 4.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FreeTrialUsed_EqualsDistinctPoiCount_ForAnonymousDevice()
    {
        return Prop.ForAll(DeviceScenarioArb, scenario =>
        {
            var (deviceId, poiIds) = scenario;
            var dbName = $"prop9c_{Guid.NewGuid()}";
            using var dbContext = CreateDbContext(dbName);

            foreach (var poiId in poiIds.Distinct())
            {
                dbContext.FreeTrialRecords.Add(new FreeTrialRecord
                {
                    DeviceId = deviceId,
                    PoiId = poiId,
                    FirstHeardAt = DateTime.UtcNow
                });
            }
            dbContext.SaveChanges();

            var expectedDistinct = poiIds.Distinct().Count();
            var controller = CreateControllerWithUser(dbContext, userId: null);

            var result = controller.Check(deviceId: deviceId, ct: CancellationToken.None)
                .GetAwaiter().GetResult();

            if (result is not OkObjectResult okResult)
                return Prop.Label(false, $"Expected OkObjectResult but got {result.GetType().Name}");

            var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            var doc = System.Text.Json.JsonDocument.Parse(json).RootElement;

            var freeTrialUsed = doc.GetProperty("freeTrialUsed").GetInt32();

            if (freeTrialUsed != expectedDistinct)
                return Prop.Label(false,
                    $"freeTrialUsed={freeTrialUsed} but expected distinct={expectedDistinct} " +
                    $"(deviceId='{deviceId}')");

            return Prop.Label(true,
                $"OK: deviceId='{deviceId}', distinctPois={expectedDistinct}");
        });
    }
}
