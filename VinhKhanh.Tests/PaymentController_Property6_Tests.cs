// Feature: vinh-khanh-tts-missing-features, Property 6: ExpiryDate của Access Pass = CreatedAt + 7 ngày

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
/// Property 6: ExpiryDate của Access Pass = CreatedAt + 7 ngày
/// Validates: Yêu cầu 3.4
/// </summary>
public class PaymentController_Property6_Tests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static AppDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    private static PaymentController CreateController(AppDbContext dbContext, string userId)
    {
        var controller = new PaymentController(dbContext);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId)
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

    // Generate random DateTime values within the last 2 years
    private static readonly Gen<DateTime> CreatedAtGen =
        from offsetDays in Gen.Choose(0, 730)
        from offsetSeconds in Gen.Choose(0, 86399)
        select DateTime.UtcNow.Date.AddDays(-offsetDays).AddSeconds(offsetSeconds);

    private static readonly Arbitrary<DateTime> CreatedAtArb =
        Arb.ToArbitrary(CreatedAtGen);

    // ── Property 6 ───────────────────────────────────────────────────────────

    /// <summary>
    /// For any Payment with Status=Pending and any CreatedAt datetime,
    /// after calling POST /api/payments/callback, ExpiryDate must equal exactly CreatedAt + 7 days.
    ///
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Callback_ExpiryDate_EqualsCreatedAtPlusSeven()
    {
        return Prop.ForAll(CreatedAtArb, createdAt =>
        {
            var dbName = $"prop6_{Guid.NewGuid()}";
            using var dbContext = CreateDbContext(dbName);

            var transactionId = $"txn-{Guid.NewGuid():N}";
            var userId = "user-test-001";

            // Seed a Payment with Status=Pending and the generated CreatedAt
            var payment = new Payment
            {
                TransactionId = transactionId,
                UserId = userId,
                Amount = 1.00m,
                Type = PaymentType.AccessPass,
                Status = PaymentStatus.Pending,
                CreatedAt = createdAt
            };
            dbContext.Payments.Add(payment);
            dbContext.SaveChanges();

            var controller = CreateController(dbContext, userId);

            var request = new PaymentCallbackRequest { TransactionId = transactionId };
            var result = controller.Callback(request, CancellationToken.None)
                .GetAwaiter().GetResult();

            // Must be OkObjectResult
            if (result is not OkObjectResult)
                return Prop.Label(false, $"Expected OkObjectResult but got {result.GetType().Name}");

            // Reload from DB to verify persisted value
            var updated = dbContext.Payments.First(p => p.TransactionId == transactionId);

            if (updated.ExpiryDate is null)
                return Prop.Label(false, "ExpiryDate is null after callback");

            var expected = createdAt.AddDays(7);
            if (updated.ExpiryDate.Value != expected)
                return Prop.Label(false,
                    $"ExpiryDate mismatch: expected {expected:O} but got {updated.ExpiryDate.Value:O} " +
                    $"(CreatedAt={createdAt:O})");

            return Prop.Label(true,
                $"OK: CreatedAt={createdAt:O}, ExpiryDate={updated.ExpiryDate.Value:O}");
        });
    }
}
