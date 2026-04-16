// Feature: vinh-khanh-tts-missing-features, Property 3: API đồng bộ mobile chỉ trả về POI Approved
// Feature: vinh-khanh-tts-missing-features, Property 23: Sync response phản ánh đúng IsPremium của từng POI

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using VinhKhanh.Domain.Entities;
using VinhKhanh.Infrastructure.Data;
using VinhKhanh.Infrastructure.Repositories;

namespace VinhKhanh.Tests;

/// <summary>
/// Property 3: API đồng bộ mobile chỉ trả về POI Approved — Validates: Yêu cầu 1.9
/// Property 23: Sync response phản ánh đúng IsPremium của từng POI — Validates: Yêu cầu 14.1
/// </summary>
public class PoisController_Property3_23_Tests
{
    private static AppDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    private static readonly PoiStatus[] AllStatuses =
        { PoiStatus.Draft, PoiStatus.Pending_Approval, PoiStatus.Approved, PoiStatus.Rejected, PoiStatus.Hidden };

    private static readonly Gen<bool> BoolGen = Gen.Elements(true, false);

    private static readonly Gen<(PoiStatus status, bool isPremium)> PoiSpecGen =
        from status in Gen.Elements(AllStatuses)
        from isPremium in BoolGen
        select (status, isPremium);

    private static readonly Arbitrary<List<(PoiStatus status, bool isPremium)>> PoiSpecListArb =
        Arb.ToArbitrary(
            from count in Gen.Choose(1, 10)
            from specs in PoiSpecGen.ListOf(count)
            select specs.ToList());

    /// <summary>
    /// For any mix of POIs with various statuses, GetSyncPoisAsync must only return POIs with Status = Approved.
    /// Validates: Yêu cầu 1.9
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetSyncPois_OnlyReturnsApprovedPois()
    {
        return Prop.ForAll(PoiSpecListArb, poiSpecs =>
        {
            var dbName = $"prop3_{Guid.NewGuid()}";
            using var dbContext = CreateDbContext(dbName);

            var seededPois = new List<Poi>();
            foreach (var (status, isPremium) in poiSpecs)
            {
                var poi = new Poi
                {
                    BasePoiId = Guid.NewGuid().ToString("N")[..10],
                    CategoryCode = "FOOD_STREET",
                    Latitude = 10.77,
                    Longitude = 106.70,
                    Radius = 50,
                    Status = status,
                    IsPremium = isPremium,
                    IsApproved = status == PoiStatus.Approved,
                    UpdatedAt = DateTime.UtcNow
                };
                seededPois.Add(poi);
                dbContext.Pois.Add(poi);
            }
            dbContext.SaveChanges();

            var repo = new PoiRepository(dbContext);
            var results = repo.GetSyncPoisAsync(DateTime.MinValue, CancellationToken.None)
                .GetAwaiter().GetResult()
                .ToList();

            var nonApproved = results.Where(p => p.Status != PoiStatus.Approved).ToList();
            if (nonApproved.Any())
                return Prop.Label(false,
                    $"Returned {nonApproved.Count} non-Approved POI(s): " +
                    string.Join(", ", nonApproved.Select(p => p.Status)));

            var expectedCount = seededPois.Count(p => p.Status == PoiStatus.Approved);
            if (results.Count != expectedCount)
                return Prop.Label(false,
                    $"Expected {expectedCount} Approved POIs but got {results.Count}. Total seeded: {seededPois.Count}");

            return Prop.Label(true,
                $"OK: seeded={seededPois.Count}, approved={expectedCount}, returned={results.Count}");
        });
    }

    /// <summary>
    /// For any mix of Approved POIs with IsPremium = true/false, the sync response must
    /// correctly reflect each POI's IsPremium value.
    /// Validates: Yêu cầu 14.1
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetSyncPois_ReflectsIsPremiumCorrectly()
    {
        var approvedPoiArb = Arb.ToArbitrary(
            from count in Gen.Choose(1, 10)
            from isPremiumList in BoolGen.ListOf(count)
            select isPremiumList.ToList());

        return Prop.ForAll(approvedPoiArb, isPremiumList =>
        {
            var dbName = $"prop23_{Guid.NewGuid()}";
            using var dbContext = CreateDbContext(dbName);

            var seededPois = new List<Poi>();
            foreach (var isPremium in isPremiumList)
            {
                var poi = new Poi
                {
                    BasePoiId = Guid.NewGuid().ToString("N")[..10],
                    CategoryCode = "FOOD_STREET",
                    Latitude = 10.77,
                    Longitude = 106.70,
                    Radius = 50,
                    Status = PoiStatus.Approved,
                    IsPremium = isPremium,
                    IsApproved = true,
                    UpdatedAt = DateTime.UtcNow
                };
                seededPois.Add(poi);
                dbContext.Pois.Add(poi);
            }
            dbContext.SaveChanges();

            var repo = new PoiRepository(dbContext);
            var results = repo.GetSyncPoisAsync(DateTime.MinValue, CancellationToken.None)
                .GetAwaiter().GetResult()
                .ToList();

            var seededById = seededPois.ToDictionary(p => p.Id, p => p.IsPremium);

            foreach (var result in results)
            {
                if (!seededById.TryGetValue(result.Id, out var expectedIsPremium))
                    return Prop.Label(false, $"Returned POI id={result.Id} was not in seeded set");

                if (result.IsPremium != expectedIsPremium)
                    return Prop.Label(false,
                        $"POI id={result.Id}: expected IsPremium={expectedIsPremium} but got {result.IsPremium}");
            }

            if (results.Count != seededPois.Count)
                return Prop.Label(false, $"Expected {seededPois.Count} results but got {results.Count}");

            return Prop.Label(true,
                $"OK: seeded={seededPois.Count}, premium={results.Count(p => p.IsPremium)}, free={results.Count(p => !p.IsPremium)}");
        });
    }
}
