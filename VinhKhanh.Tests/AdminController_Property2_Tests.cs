// Feature: vinh-khanh-tts-missing-features, Property 2: Vòng đời trạng thái POI (Draft → Pending → Approved/Rejected)

using System.Security.Claims;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using VinhKhanh.Admin.Controllers;
using VinhKhanh.Application.UseCases;
using VinhKhanh.Domain.Entities;
using VinhKhanh.Infrastructure.Data;
using VinhKhanh.Infrastructure.Services;

namespace VinhKhanh.Tests;

/// <summary>
/// Property 2: Vòng đời trạng thái POI (Draft → Pending → Approved/Rejected)
/// Validates: Yêu cầu 1.5, 1.6, 1.7
/// </summary>
public class AdminController_Property2_Tests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static AppDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    private static AdminController CreateController(AppDbContext dbContext)
    {
        // AdminApproveUseCase depends on IPoiRepository (interface) — mock it
        var poiRepoMock = new Mock<VinhKhanh.Domain.Interfaces.IPoiRepository>();
        var approveUseCase = new AdminApproveUseCase(poiRepoMock.Object);

        // GeminiAiService is a concrete class — create with real HttpClient and mocked config/logger
        var configMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        configMock.Setup(c => c["Gemini:ApiKey"]).Returns("test-key");
        var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<GeminiAiService>>();
        var geminiService = new GeminiAiService(new System.Net.Http.HttpClient(), configMock.Object, loggerMock.Object);

        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.WebRootPath).Returns(Path.GetTempPath());

        var controller = new AdminController(
            approveUseCase,
            geminiService,
            dbContext,
            envMock.Object);

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

    private static Poi SeedPoi(AppDbContext dbContext, PoiStatus initialStatus)
    {
        var poi = new Poi
        {
            BasePoiId = Guid.NewGuid().ToString("N")[..10],
            CategoryCode = "FOOD_STREET",
            Latitude = 10.77,
            Longitude = 106.70,
            Radius = 50,
            Status = initialStatus,
            IsApproved = initialStatus == PoiStatus.Approved,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.Pois.Add(poi);
        dbContext.SaveChanges();
        return poi;
    }

    // ── generators ───────────────────────────────────────────────────────────

    private static readonly PoiStatus[] AllStatuses =
        { PoiStatus.Draft, PoiStatus.Pending_Approval, PoiStatus.Approved, PoiStatus.Rejected, PoiStatus.Hidden };

    private static readonly Arbitrary<PoiStatus> AnyStatus =
        Arb.ToArbitrary(Gen.Elements(AllStatuses));

    // Generator for valid rejection reasons (length >= 10)
    private static readonly Arbitrary<string> ValidReasonArb =
        Arb.ToArbitrary(
            from suffix in Gen.Choose(0, 999999)
            from extra in Gen.Choose(0, 50)
            let baseStr = $"Lý do từ chối hợp lệ {suffix}"
            select baseStr.PadRight(10 + extra, 'x'));

    private static readonly char[] LowerAlpha =
        "abcdefghijklmnopqrstuvwxyz".ToCharArray();

    // Generator for short/invalid rejection reasons (length < 10)
    private static readonly Arbitrary<string> ShortReasonArb =
        Arb.ToArbitrary(
            from len in Gen.Choose(0, 9)
            from chars in Gen.Elements(LowerAlpha).ArrayOf(len)
            select new string(chars));

    // ── Property 2a: Approve transitions any status → Approved ───────────────

    /// <summary>
    /// For any POI with any initial status, calling Approve(poiId) sets
    /// Status = Approved and IsApproved = true.
    ///
    /// **Validates: Requirements 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Approve_AnyInitialStatus_SetsApprovedAndIsApprovedTrue()
    {
        return Prop.ForAll(AnyStatus, initialStatus =>
        {
            var dbName = $"prop2a_{Guid.NewGuid()}";
            using var dbContext = CreateDbContext(dbName);
            var poi = SeedPoi(dbContext, initialStatus);
            var controller = CreateController(dbContext);

            var result = controller.Approve(poi.Id, CancellationToken.None)
                .GetAwaiter().GetResult();

            if (result is not OkObjectResult)
                return Prop.Label(false,
                    $"Expected OkObjectResult but got {result.GetType().Name} for initialStatus={initialStatus}");

            var updatedPoi = dbContext.Pois.Find(poi.Id)!;

            if (updatedPoi.Status != PoiStatus.Approved)
                return Prop.Label(false,
                    $"Expected Status=Approved but got {updatedPoi.Status} (initialStatus={initialStatus})");

            if (!updatedPoi.IsApproved)
                return Prop.Label(false,
                    $"Expected IsApproved=true but got false (initialStatus={initialStatus})");

            return Prop.Label(true,
                $"OK: initialStatus={initialStatus} → Approved, IsApproved=true");
        });
    }

    // ── Property 2b: Reject with valid reason transitions any status → Rejected ──

    /// <summary>
    /// For any POI with any initial status, calling RejectPoi(poiId, reason) with
    /// reason >= 10 chars sets Status = Rejected, IsApproved = false, and saves RejectionReason.
    ///
    /// **Validates: Requirements 1.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RejectWithValidReason_AnyInitialStatus_SetsRejectedAndSavesReason()
    {
        var arb = Arb.ToArbitrary(
            from status in Gen.Elements(AllStatuses)
            from suffix in Gen.Choose(0, 999999)
            let reason = $"Lý do từ chối hợp lệ {suffix}".PadRight(15, 'x')
            select (status, reason));

        return Prop.ForAll(arb, input =>
        {
            var (initialStatus, reason) = input;
            var dbName = $"prop2b_{Guid.NewGuid()}";
            using var dbContext = CreateDbContext(dbName);
            var poi = SeedPoi(dbContext, initialStatus);
            var controller = CreateController(dbContext);

            var request = new RejectPoiRequest { Reason = reason };
            var result = controller.RejectPoi(poi.Id, request, CancellationToken.None)
                .GetAwaiter().GetResult();

            if (result is not OkObjectResult)
                return Prop.Label(false,
                    $"Expected OkObjectResult but got {result.GetType().Name} " +
                    $"(initialStatus={initialStatus}, reason.Length={reason.Length})");

            var updatedPoi = dbContext.Pois.Find(poi.Id)!;

            if (updatedPoi.Status != PoiStatus.Rejected)
                return Prop.Label(false,
                    $"Expected Status=Rejected but got {updatedPoi.Status} (initialStatus={initialStatus})");

            if (updatedPoi.IsApproved)
                return Prop.Label(false,
                    $"Expected IsApproved=false but got true (initialStatus={initialStatus})");

            if (updatedPoi.RejectionReason != reason)
                return Prop.Label(false,
                    $"Expected RejectionReason='{reason}' but got '{updatedPoi.RejectionReason}'");

            return Prop.Label(true,
                $"OK: initialStatus={initialStatus} → Rejected, reason saved");
        });
    }

    // ── Property 2c: Reject with short reason returns 400 ────────────────────

    /// <summary>
    /// For any reason with length < 10, RejectPoi returns BadRequestObjectResult.
    ///
    /// **Validates: Requirements 1.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RejectWithShortReason_ReturnsBadRequest()
    {
        var arb = Arb.ToArbitrary(
            from status in Gen.Elements(AllStatuses)
            from len in Gen.Choose(0, 9)
            from chars in Gen.Elements(LowerAlpha).ArrayOf(len)
            select (status, new string(chars)));

        return Prop.ForAll(arb, input =>
        {
            var (initialStatus, shortReason) = input;
            var dbName = $"prop2c_{Guid.NewGuid()}";
            using var dbContext = CreateDbContext(dbName);
            var poi = SeedPoi(dbContext, initialStatus);
            var controller = CreateController(dbContext);

            var request = new RejectPoiRequest { Reason = shortReason };
            var result = controller.RejectPoi(poi.Id, request, CancellationToken.None)
                .GetAwaiter().GetResult();

            if (result is not BadRequestObjectResult)
                return Prop.Label(false,
                    $"Expected BadRequestObjectResult but got {result.GetType().Name} " +
                    $"(reason='{shortReason}', length={shortReason.Length})");

            return Prop.Label(true,
                $"OK: shortReason.Length={shortReason.Length} → 400 BadRequest");
        });
    }

    // ── Property 2d: Hide transitions any status → Hidden ────────────────────

    /// <summary>
    /// For any POI with any initial status, calling HidePoi(poiId) sets Status = Hidden.
    ///
    /// **Validates: Requirements 1.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property HidePoi_AnyInitialStatus_SetsHidden()
    {
        return Prop.ForAll(AnyStatus, initialStatus =>
        {
            var dbName = $"prop2d_{Guid.NewGuid()}";
            using var dbContext = CreateDbContext(dbName);
            var poi = SeedPoi(dbContext, initialStatus);
            var controller = CreateController(dbContext);

            var result = controller.HidePoi(poi.Id, CancellationToken.None)
                .GetAwaiter().GetResult();

            if (result is not OkObjectResult)
                return Prop.Label(false,
                    $"Expected OkObjectResult but got {result.GetType().Name} for initialStatus={initialStatus}");

            var updatedPoi = dbContext.Pois.Find(poi.Id)!;

            if (updatedPoi.Status != PoiStatus.Hidden)
                return Prop.Label(false,
                    $"Expected Status=Hidden but got {updatedPoi.Status} (initialStatus={initialStatus})");

            return Prop.Label(true,
                $"OK: initialStatus={initialStatus} → Hidden");
        });
    }
}
