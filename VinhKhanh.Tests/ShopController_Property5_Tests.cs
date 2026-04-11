// Feature: vinh-khanh-tts-missing-features, Property 5: ShopOwner không thể sửa POI của người khác hoặc POI đang duyệt

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
/// Property 5: ShopOwner không thể sửa POI của người khác hoặc POI đang duyệt
/// Validates: Yêu cầu 2.7, 5.6, 5.7
/// </summary>
public class ShopController_Property5_Tests
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

        // Return an approved user for any userId lookup
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

    private static CreateShopPoiRequest MakeRequest() => new CreateShopPoiRequest
    {
        Lat = 10.77, Lng = 106.70, Radius = 50,
        CategoryCode = "FOOD_STREET",
        NameVi = "Test", DescVi = "Test desc",
        NameEn = "Test", DescEn = "Test desc",
        NameJa = "テスト", DescJa = "テスト説明"
    };

    // ── generators ───────────────────────────────────────────────────────────

    // Pair of distinct user IDs: (ownerId, requesterId)
    // ownerId comes from set A, requesterId from set B — always distinct
    private static readonly Arbitrary<(string ownerId, string requesterId)> DistinctUserIdPairArb =
        Arb.ToArbitrary(
            from id1 in Gen.Elements(new[] { "user-A", "user-B", "user-C", "user-D", "user-E" })
            from id2 in Gen.Elements(new[] { "user-1", "user-2", "user-3", "user-4", "user-5" })
            select (id1, id2));

    // Blocking statuses: Pending_Approval or Approved
    private static readonly Arbitrary<(string userId, PoiStatus status)> OwnerWithBlockingStatusArb =
        Arb.ToArbitrary(
            from userId in Gen.Elements(new[] { "owner-X", "owner-Y", "owner-Z" })
            from status in Gen.Elements(new[] { PoiStatus.Pending_Approval, PoiStatus.Approved })
            select (userId, status));

    // ── Property 5a: OwnerId ≠ currentUserId → HTTP 403 ─────────────────────

    /// <summary>
    /// For any ShopOwner A and any POI where OwnerId ≠ A.Id,
    /// PUT /api/shop/pois/{id} must return HTTP 403.
    /// Validates: Yêu cầu 2.7, 5.6
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ShopOwner_CannotEdit_OtherOwnerPoi_Returns403()
    {
        return Prop.ForAll(DistinctUserIdPairArb, pair =>
        {
            var (differentOwnerId, requesterId) = pair;
            var dbName = $"prop5a_{Guid.NewGuid()}";
            using var dbContext = CreateDbContext(dbName);

            // Seed a POI owned by differentOwnerId with an editable status (Draft)
            var poi = new Poi
            {
                BasePoiId = "test001",
                CategoryCode = "FOOD_STREET",
                Latitude = 10.77, Longitude = 106.70, Radius = 50,
                Status = PoiStatus.Draft,
                OwnerId = differentOwnerId,
                IsApproved = false,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Pois.Add(poi);
            dbContext.SaveChanges();

            var userManager = CreateApprovedUserManager();
            var controller = CreateController(dbContext, userManager, requesterId);

            var result = controller.UpdatePoi(poi.Id, MakeRequest(), CancellationToken.None)
                .GetAwaiter().GetResult();

            var statusCode = result is ObjectResult objResult ? objResult.StatusCode : null;
            return Prop.Label(
                statusCode == 403,
                $"Expected 403 but got {statusCode} (ownerId={differentOwnerId}, requesterId={requesterId})");
        });
    }

    // ── Property 5b: Status ∈ {Pending_Approval, Approved} → HTTP 403 ───────

    /// <summary>
    /// For any ShopOwner A and any POI where Status ∈ {Pending_Approval, Approved}
    /// (even when OwnerId == A.Id), PUT /api/shop/pois/{id} must return HTTP 403.
    /// Validates: Yêu cầu 5.7
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ShopOwner_CannotEdit_PendingOrApprovedPoi_Returns403()
    {
        return Prop.ForAll(OwnerWithBlockingStatusArb, tuple =>
        {
            var (userId, status) = tuple;
            var dbName = $"prop5b_{Guid.NewGuid()}";
            using var dbContext = CreateDbContext(dbName);

            // Seed a POI owned by the same user but with a blocking status
            var poi = new Poi
            {
                BasePoiId = "test002",
                CategoryCode = "FOOD_STREET",
                Latitude = 10.77, Longitude = 106.70, Radius = 50,
                Status = status,
                OwnerId = userId,
                IsApproved = status == PoiStatus.Approved,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.Pois.Add(poi);
            dbContext.SaveChanges();

            var userManager = CreateApprovedUserManager();
            var controller = CreateController(dbContext, userManager, userId);

            var result = controller.UpdatePoi(poi.Id, MakeRequest(), CancellationToken.None)
                .GetAwaiter().GetResult();

            var statusCode = result is ObjectResult objResult ? objResult.StatusCode : null;
            return Prop.Label(
                statusCode == 403,
                $"Expected 403 but got {statusCode} (userId={userId}, status={status})");
        });
    }
}
