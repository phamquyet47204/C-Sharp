// Feature: vinh-khanh-tts-missing-features, Property 7: TransactionId phải là duy nhất — duplicate trả về 409

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
/// Property 7: TransactionId phải là duy nhất — duplicate trả về 409
/// Validates: Yêu cầu 3.6
/// </summary>
public class PaymentController_Property7_Tests
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

    /// <summary>
    /// Generates non-empty alphanumeric TransactionId strings.
    /// </summary>
    private static readonly Arbitrary<string> TransactionIdArb =
        Arb.ToArbitrary(
            from n in Gen.Choose(8, 20)
            select Guid.NewGuid().ToString("N")[..n]
        );

    // ── Property 7 ───────────────────────────────────────────────────────────

    /// <summary>
    /// For any non-empty TransactionId, if a Payment with that TransactionId already
    /// exists in the DB, calling POST /api/payments/initiate with the same TransactionId
    /// must return HTTP 409 Conflict.
    ///
    /// **Validates: Requirements 3.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Initiate_DuplicateTransactionId_Returns409()
    {
        return Prop.ForAll(TransactionIdArb, transactionId =>
        {
            var dbName = $"prop7_dup_{Guid.NewGuid()}";
            using var dbContext = CreateDbContext(dbName);

            // Seed an existing Payment with this TransactionId
            dbContext.Payments.Add(new Payment
            {
                TransactionId = transactionId,
                UserId = "existing-user",
                Amount = 1.00m,
                Type = PaymentType.AccessPass,
                Status = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext, "test-user");
            var ct = CancellationToken.None;

            var result = controller.Initiate(
                new InitiatePaymentRequest { TransactionId = transactionId }, ct)
                .GetAwaiter().GetResult();

            if (result is not ConflictObjectResult)
                return Prop.Label(false,
                    $"Expected ConflictObjectResult (409) but got {result.GetType().Name} " +
                    $"for duplicate TransactionId='{transactionId}'");

            return Prop.Label(true, $"OK: duplicate TransactionId='{transactionId}' → 409");
        });
    }

    /// <summary>
    /// For any non-empty TransactionId that does NOT exist in the DB,
    /// calling POST /api/payments/initiate must return HTTP 200 OK.
    ///
    /// **Validates: Requirements 3.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Initiate_FreshTransactionId_Returns200()
    {
        return Prop.ForAll(TransactionIdArb, transactionId =>
        {
            var dbName = $"prop7_fresh_{Guid.NewGuid()}";
            using var dbContext = CreateDbContext(dbName);

            // DB is empty — no existing payment with this TransactionId
            var controller = CreateController(dbContext, "test-user");
            var ct = CancellationToken.None;

            var result = controller.Initiate(
                new InitiatePaymentRequest { TransactionId = transactionId }, ct)
                .GetAwaiter().GetResult();

            if (result is not OkObjectResult)
                return Prop.Label(false,
                    $"Expected OkObjectResult (200) but got {result.GetType().Name} " +
                    $"for fresh TransactionId='{transactionId}'");

            return Prop.Label(true, $"OK: fresh TransactionId='{transactionId}' → 200");
        });
    }
}
