// Feature: vinh-khanh-tts-missing-features, Property 10: GET /api/shop/pois chỉ trả về POI của ShopOwner đang đăng nhập

using System.Security.Claims;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using VinhKhanh.Admin.Controllers;
using VinhKhanh.Domain.Entities;
using VinhKhanh.Infrastructure.Data;

namespace VinhKhanh.Tests;

/// <summary>
/// Property 10: GET /api/shop/pois chỉ trả về POI của ShopOwner đang đăng nhập
/// Validates: Yêu cầu 5.1
/// </summary>
public class ShopController_Property10_Tests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static AppDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    private static UserManager<ApplicationUser> CreateApprovedUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var optionsAccessor = new Mock<IOptions<IdentityOptions>>();
        optionsAccessor.Setup(o => o.Value).Returns(new IdentityOptions());
        var passwordHasher = new Mock<IPasswordHasher<ApplicationUser>>();
        var userValidators = new List<IUserValidator<ApplicationUser>>();
        var passwordValidators = new List<IPasswordValidator<ApplicationUser>>();
        var keyNormalizer = new Mock<ILookupNormalizer>();
        var errors = new Mock<IdentityErrorDescriber>();
        var services = new Mock<IServiceProvider>();
        var logger = new Mock<ILogger<UserManager<ApplicationUser>>>();

        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            store.Object, optionsAccessor.Object, passwordHasher.Object,
            userValidators, passwordValidators, keyNormalizer.Object,
            errors.Object, services.Object, logger.Object);

        userManagerMock
            .Setup(m => m.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => new ApplicationUser
            {
                Id = id,
                UserName = $"user_{id}",
                IsApproved = true
            });

        return userManagerMock.Object;
    }

    private static ShopController CreateController(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        string currentUserId)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.WebRootPath).Returns(Path.GetTempPath());

        var controller = new ShopController(dbContext, env.Object, userManager);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, currentUserId),
            new Claim(ClaimTypes.Role, "ShopOwner")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    // ── generators ───────────────────────────────────────────────────────────

    // All possible owner IDs used in tests
    private static readonly string[] AllOwnerIds =
        { "owner-A", "owner-B", "owner-C", "owner-D", "owner-E" };

    // All possible PoiStatus values
    private static readonly PoiStatus[] AllStatuses =
        { PoiStatus.Draft, PoiStatus.Pending_Approval, PoiStatus.Approved, PoiStatus.Rejected, PoiStatus.Hidden };

    /// <summary>
    /// Generates: (currentUserId, list of (ownerId, status) pairs for POIs to seed)
    /// The list contains a mix of POIs owned by currentUserId and by other owners,
    /// with random statuses.
    /// </summary>
    private static readonly Gen<(string ownerId, PoiStatus status)> PoiSpecGen =
        from ownerId in Gen.Elements(AllOwnerIds)
        from status in Gen.Elements(AllStatuses)
        select (ownerId, status);

    private static readonly Arbitrary<(string currentUserId, List<(string ownerId, PoiStatus status)> poiSpecs)> ScenarioArb =
        Arb.ToArbitrary(
            from currentUserId in Gen.Elements(AllOwnerIds)
            from count in Gen.Choose(0, 8)
            from poiSpecs in PoiSpecGen.ListOf(count)
            select (currentUserId, poiSpecs.ToList()));

    // ── Property 10 ──────────────────────────────────────────────────────────

    /// <summary>
    /// For any authenticated ShopOwner and any database state (mix of POIs with
    /// various OwnerId values and statuses), GET /api/shop/pois must only return
    /// POIs where OwnerId == currentUserId.
    ///
    /// **Validates: Requirements 5.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetMyPois_OnlyReturnsPoisBelongingToCurrentShopOwner()
    {
        return Prop.ForAll(ScenarioArb, scenario =>
        {
            var (currentUserId, poiSpecs) = scenario;
            var dbName = $"prop10_{Guid.NewGuid()}";
            using var dbContext = CreateDbContext(dbName);

            // Seed POIs with various owners and statuses
            var seededPois = new List<Poi>();
            foreach (var (ownerId, status) in poiSpecs)
            {
                var poi = new Poi
                {
                    BasePoiId = Guid.NewGuid().ToString("N")[..10],
                    CategoryCode = "FOOD_STREET",
                    Latitude = 10.77,
                    Longitude = 106.70,
                    Radius = 50,
                    Status = status,
                    OwnerId = ownerId,
                    IsApproved = status == PoiStatus.Approved,
                    UpdatedAt = DateTime.UtcNow
                };
                seededPois.Add(poi);
                dbContext.Pois.Add(poi);
            }
            dbContext.SaveChanges();

            var expectedCount = seededPois.Count(p => p.OwnerId == currentUserId);

            var userManager = CreateApprovedUserManager();
            var controller = CreateController(dbContext, userManager, currentUserId);

            var result = controller.GetMyPois(CancellationToken.None)
                .GetAwaiter().GetResult();

            // Must be OkObjectResult
            if (result is not OkObjectResult okResult)
                return Prop.Label(false, $"Expected OkObjectResult but got {result.GetType().Name}");

            // Deserialize the anonymous list via JSON round-trip
            var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            var items = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(json)!;

            // Verify count matches expected
            if (items.Count != expectedCount)
                return Prop.Label(false,
                    $"Expected {expectedCount} POIs for owner '{currentUserId}' but got {items.Count}. " +
                    $"Total seeded: {seededPois.Count}");

            // Verify every returned item has no OwnerId from another user
            // (the response doesn't expose ownerId directly, so we verify via count
            //  and by checking the DB that returned IDs all belong to currentUserId)
            // We cross-check by re-querying the DB for the expected set
            var expectedIds = seededPois
                .Where(p => p.OwnerId == currentUserId)
                .Select(p => p.Id)
                .ToHashSet();

            foreach (var item in items)
            {
                if (!item.TryGetProperty("id", out var idProp))
                    return Prop.Label(false, "Response item missing 'id' field");

                var returnedId = idProp.GetInt32();
                if (!expectedIds.Contains(returnedId))
                    return Prop.Label(false,
                        $"Returned POI id={returnedId} does not belong to currentUserId='{currentUserId}'");
            }

            return Prop.Label(true,
                $"OK: owner='{currentUserId}', seeded={seededPois.Count}, returned={items.Count}");
        });
    }
}
