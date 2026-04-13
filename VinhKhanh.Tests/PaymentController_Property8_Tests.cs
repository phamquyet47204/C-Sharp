// Feature: vinh-khanh-tts-missing-features, Property 8: hasActivePass phản ánh đúng trạng thái hết hạn

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
/// Property 8: hasActivePass phản ánh đúng trạng thái hết hạn
/// Validates: Yêu cầu 3.7, 3.8
/// </summary>
public class PaymentController_Property8_Tests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static AppDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    private static PaymentController CreateController(AppDbContext dbContext, string currentUserId)
    {
        var controller = new PaymentController(dbContext);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, currentUserId)
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

    private static readonly Gen<int> ExpiryOffsetDaysGen = Gen.Choose(-30, 30);

    private static readonly Gen<string> UserIdGen =
        from n in Gen.Choose(1, 1000)
        select $"user-{n}";

    // ── Property 8a: Completed + ExpiryDate > now → hasActivePass = true ─────

    private static readonly Arbitrary<(string userId, int offsetDays)> ActivePassArb =
        Arb.ToArbitrary(
            from userId in UserIdGen
            from offsetDays in Gen.Choose(1, 30)
            select (userId, offsetDays));

    /// <summary>
    /// For any user with a Completed payment whose ExpiryDate is in the future,
    /// GetStatus must return hasActivePass = true.
    /// Validates: Requirements 3.7
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CompletedPayment_FutureExpiry_HasActivePassIsTrue()
    {
        return Prop.ForAll(ActivePassArb, scenario =>
        {
            var (userId, offsetDays) = scenario;
            var dbName = $"prop8a_{Guid.NewGuid()}";
            using var dbContext = CreateDbContext(dbName);

            var now = DateTime.UtcNow;
            dbContext.Payments.Add(new Payment
            {
                TransactionId = Guid.NewGuid().ToString(),
                UserId = userId,
                Amount = 1.00m,
                Type = PaymentType.AccessPass,
                Status = PaymentStatus.Completed,
                ExpiryDate = now.AddDays(offsetDays),
                CreatedAt = now.AddDays(-1)
            });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext, userId);
            var result = controller.GetStatus(CancellationToken.None).GetAwaiter().GetResult();

            if (result is not OkObjectResult okResult)
                return Prop.Label(false, $"Expected OkObjectResult but got {result.GetType().Name}");

            var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            var doc = System.Text.Json.JsonDocument.Parse(json).RootElement;

            if (!doc.TryGetProperty("hasActivePass", out var hasActivePassProp))
                return Prop.Label(false, "Response missing 'hasActivePass' field");

            var hasActivePass = hasActivePassProp.GetBoolean();
            return Prop.Label(hasActivePass,
                $"userId={userId}, offsetDays=+{offsetDays}, hasActivePass={hasActivePass} (expected true)");
        });
    }

    // ── Property 8b: Completed + ExpiryDate <= now → hasActivePass = false ───

    private static readonly Arbitrary<(string userId, int offsetDays)> ExpiredPassArb =
        Arb.ToArbitrary(
            from userId in UserIdGen
            from offsetDays in Gen.Choose(0, 30)
            select (userId, offsetDays));

    /// <summary>
    /// For any user with a Completed payment whose ExpiryDate is in the past (or exactly now),
    /// GetStatus must return hasActivePass = false.
    /// Validates: Requirements 3.8
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CompletedPayment_ExpiredExpiry_HasActivePassIsFalse()
    {
        return Prop.ForAll(ExpiredPassArb, scenario =>
        {
            var (userId, offsetDays) = scenario;
            var dbName = $"prop8b_{Guid.NewGuid()}";
            using var dbContext = CreateDbContext(dbName);

            var now = DateTime.UtcNow;
            dbContext.Payments.Add(new Payment
            {
                TransactionId = Guid.NewGuid().ToString(),
                UserId = userId,
                Amount = 1.00m,
                Type = PaymentType.AccessPass,
                Status = PaymentStatus.Completed,
                ExpiryDate = now.AddDays(-offsetDays),
                CreatedAt = now.AddDays(-(offsetDays + 1))
            });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext, userId);
            var result = controller.GetStatus(CancellationToken.None).GetAwaiter().GetResult();

            if (result is not OkObjectResult okResult)
                return Prop.Label(false, $"Expected OkObjectResult but got {result.GetType().Name}");

            var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            var doc = System.Text.Json.JsonDocument.Parse(json).RootElement;

            if (!doc.TryGetProperty("hasActivePass", out var hasActivePassProp))
                return Prop.Label(false, "Response missing 'hasActivePass' field");

            var hasActivePass = hasActivePassProp.GetBoolean();
            return Prop.Label(!hasActivePass,
                $"userId={userId}, offsetDays=-{offsetDays}, hasActivePass={hasActivePass} (expected false)");
        });
    }

    // ── Property 8c: Pending payment (any ExpiryDate) → hasActivePass = false ─

    private static readonly Arbitrary<(string userId, int offsetDays)> PendingPassArb =
        Arb.ToArbitrary(
            from userId in UserIdGen
            from offsetDays in ExpiryOffsetDaysGen
            select (userId, offsetDays));

    /// <summary>
    /// For any user with only a Pending payment (regardless of ExpiryDate),
    /// GetStatus must return hasActivePass = false.
    /// Validates: Requirements 3.7
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PendingPayment_AnyExpiry_HasActivePassIsFalse()
    {
        return Prop.ForAll(PendingPassArb, scenario =>
        {
            var (userId, offsetDays) = scenario;
            var dbName = $"prop8c_{Guid.NewGuid()}";
            using var dbContext = CreateDbContext(dbName);

            var now = DateTime.UtcNow;
            dbContext.Payments.Add(new Payment
            {
                TransactionId = Guid.NewGuid().ToString(),
                UserId = userId,
                Amount = 1.00m,
                Type = PaymentType.AccessPass,
                Status = PaymentStatus.Pending,
                ExpiryDate = now.AddDays(offsetDays),
                CreatedAt = now.AddDays(-1)
            });
            dbContext.SaveChanges();

            var controller = CreateController(dbContext, userId);
            var result = controller.GetStatus(CancellationToken.None).GetAwaiter().GetResult();

            if (result is not OkObjectResult okResult)
                return Prop.Label(false, $"Expected OkObjectResult but got {result.GetType().Name}");

            var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            var doc = System.Text.Json.JsonDocument.Parse(json).RootElement;

            if (!doc.TryGetProperty("hasActivePass", out var hasActivePassProp))
                return Prop.Label(false, "Response missing 'hasActivePass' field");

            var hasActivePass = hasActivePassProp.GetBoolean();
            return Prop.Label(!hasActivePass,
                $"userId={userId}, offsetDays={offsetDays}, status=Pending, hasActivePass={hasActivePass} (expected false)");
        });
    }

    // ── Property 8d: No payments at all → hasActivePass = false ──────────────

    private static readonly Arbitrary<string> NoPaymentArb = Arb.ToArbitrary(UserIdGen);

    /// <summary>
    /// For any user with no payments in the database,
    /// GetStatus must return hasActivePass = false.
    /// Validates: Requirements 3.7
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoPayments_HasActivePassIsFalse()
    {
        return Prop.ForAll(NoPaymentArb, userId =>
        {
            var dbName = $"prop8d_{Guid.NewGuid()}";
            using var dbContext = CreateDbContext(dbName);

            var controller = CreateController(dbContext, userId);
            var result = controller.GetStatus(CancellationToken.None).GetAwaiter().GetResult();

            if (result is not OkObjectResult okResult)
                return Prop.Label(false, $"Expected OkObjectResult but got {result.GetType().Name}");

            var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
            var doc = System.Text.Json.JsonDocument.Parse(json).RootElement;

            if (!doc.TryGetProperty("hasActivePass", out var hasActivePassProp))
                return Prop.Label(false, "Response missing 'hasActivePass' field");

            var hasActivePass = hasActivePassProp.GetBoolean();
            return Prop.Label(!hasActivePass,
                $"userId={userId}, no payments, hasActivePass={hasActivePass} (expected false)");
        });
    }
}
