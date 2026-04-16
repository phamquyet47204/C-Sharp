// Feature: vinh-khanh-tts-missing-features, Property 1: POI mới tạo bởi ShopOwner luôn có Status = Draft
// Feature: vinh-khanh-tts-missing-features, Property 4: OwnerId tự động gán khi ShopOwner tạo POI

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
/// Property 1: POI mới tạo bởi ShopOwner luôn có Status = Draft — Validates: Yêu cầu 1.4
/// Property 4: OwnerId tự động gán khi ShopOwner tạo POI — Validates: Yêu cầu 2.4
/// </summary>
public class ShopController_Property1_4_Tests
{
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

    private static readonly string[] AllOwnerIds =
        { "owner-A", "owner-B", "owner-C", "owner-D", "owner-E" };

    private static readonly string?[] AllCategoryCodes =
        { "FOOD_STREET", "RESTAURANT", "CAFE", "BAKERY", "STREET_FOOD", null };

    private static readonly Arbitrary<(string userId, CreateShopPoiRequest request)> CreatePoiArb =
        Arb.ToArbitrary(
            from userId in Gen.Elements(AllOwnerIds)
            from lat in Gen.Choose(-90, 90).Select(v => (double)v)
            from lng in Gen.Choose(-180, 180).Select(v => (double)v)
            from radius in Gen.Choose(0, 500)
            from categoryCode in Gen.Elements(AllCategoryCodes)
            select (userId, new CreateShopPoiRequest
            {
                Lat = lat,
                Lng = lng,
                Radius = radius,
                CategoryCode = categoryCode,
                NameVi = "Test", DescVi = "Test desc",
                NameEn = "Test", DescEn = "Test desc",
                NameJa = "テスト", DescJa = "テスト説明"
            }));

    /// <summary>
    /// For any valid CreateShopPoiRequest and any ShopOwner userId,
    /// after calling POST /api/shop/pois, the created POI must have Status = PoiStatus.Draft.
    /// Validates: Requirements 1.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CreatePoi_NewPoiAlwaysHasStatusDraft()
    {
        return Prop.ForAll(CreatePoiArb, scenario =>
        {
            var (userId, request) = scenario;
            var dbName = $"prop1_{Guid.NewGuid()}";
            using var dbContext = CreateDbContext(dbName);

            var userManager = CreateApprovedUserManager();
            var controller = CreateController(dbContext, userManager, userId);

            var result = controller.CreatePoi(request, CancellationToken.None)
                .GetAwaiter().GetResult();

            if (result is not OkObjectResult okResult)
                return Prop.Label(false, $"Expected OkObjectResult but got {result.GetType().Name}");

            var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("poiId", out var poiIdProp))
                return Prop.Label(false, "Response missing 'poiId' field");

            var poiId = poiIdProp.GetInt32();
            var poi = dbContext.Pois.FirstOrDefault(p => p.Id == poiId);
            if (poi is null)
                return Prop.Label(false, $"POI with id={poiId} not found in database");

            if (poi.Status != PoiStatus.Draft)
                return Prop.Label(false,
                    $"Expected Status=Draft but got Status={poi.Status} for userId='{userId}'");

            return Prop.Label(true, $"OK: userId='{userId}', poiId={poiId}, Status={poi.Status}");
        });
    }

    /// <summary>
    /// For any valid CreateShopPoiRequest and any ShopOwner userId,
    /// after calling POST /api/shop/pois, the created POI must have OwnerId == currentUserId.
    /// Validates: Requirements 2.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CreatePoi_OwnerIdIsAutomaticallyAssignedToCurrentUser()
    {
        return Prop.ForAll(CreatePoiArb, scenario =>
        {
            var (userId, request) = scenario;
            var dbName = $"prop4_{Guid.NewGuid()}";
            using var dbContext = CreateDbContext(dbName);

            var userManager = CreateApprovedUserManager();
            var controller = CreateController(dbContext, userManager, userId);

            var result = controller.CreatePoi(request, CancellationToken.None)
                .GetAwaiter().GetResult();

            if (result is not OkObjectResult okResult)
                return Prop.Label(false, $"Expected OkObjectResult but got {result.GetType().Name}");

            var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("poiId", out var poiIdProp))
                return Prop.Label(false, "Response missing 'poiId' field");

            var poiId = poiIdProp.GetInt32();
            var poi = dbContext.Pois.FirstOrDefault(p => p.Id == poiId);
            if (poi is null)
                return Prop.Label(false, $"POI with id={poiId} not found in database");

            if (poi.OwnerId != userId)
                return Prop.Label(false,
                    $"Expected OwnerId='{userId}' but got OwnerId='{poi.OwnerId}'");

            return Prop.Label(true, $"OK: userId='{userId}', poiId={poiId}, OwnerId='{poi.OwnerId}'");
        });
    }
}
