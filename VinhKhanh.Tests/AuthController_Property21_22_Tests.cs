// Feature: vinh-khanh-tts-missing-features, Property 21: Đăng ký Visitor tạo tài khoản với role Visitor và IsApproved=true
// Feature: vinh-khanh-tts-missing-features, Property 22: Email trùng lặp khi đăng ký Visitor trả về HTTP 409

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using VinhKhanh.Admin.Controllers;
using VinhKhanh.Domain.Entities;
using VinhKhanh.Infrastructure.Data;

namespace VinhKhanh.Tests;

/// <summary>
/// Property 21: Đăng ký Visitor tạo tài khoản đúng role
/// Property 22: Email trùng trả về 409
/// Validates: Requirements liên quan đến đăng ký Visitor
/// </summary>
public class AuthController_Property21_22_Tests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static (AppDbContext db, UserManager<ApplicationUser> userManager) CreateRealUserManager(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var dbContext = new AppDbContext(options);
        dbContext.Database.EnsureCreated();

        var userStore = new UserStore<ApplicationUser>(dbContext);

        var identityOptions = new IdentityOptions
        {
            Password = new PasswordOptions
            {
                RequireDigit = false,
                RequiredLength = 6,
                RequireLowercase = false,
                RequireNonAlphanumeric = false,
                RequireUppercase = false
            }
        };
        var optionsAccessor = Options.Create(identityOptions);
        var passwordHasher = new PasswordHasher<ApplicationUser>();
        var userValidators = new List<IUserValidator<ApplicationUser>> { new UserValidator<ApplicationUser>() };
        var passwordValidators = new List<IPasswordValidator<ApplicationUser>> { new PasswordValidator<ApplicationUser>() };
        var keyNormalizer = new UpperInvariantLookupNormalizer();
        var errors = new IdentityErrorDescriber();
        var services = new Mock<IServiceProvider>().Object;
        var logger = new Mock<ILogger<UserManager<ApplicationUser>>>().Object;

        var userManager = new UserManager<ApplicationUser>(
            userStore, optionsAccessor, passwordHasher,
            userValidators, passwordValidators, keyNormalizer,
            errors, services, logger);

        // Seed the "Visitor" role so AddToRoleAsync works
        var roleStore = new RoleStore<IdentityRole>(dbContext);
        var roleManager = new RoleManager<IdentityRole>(
            roleStore,
            new List<IRoleValidator<IdentityRole>> { new RoleValidator<IdentityRole>() },
            keyNormalizer,
            errors,
            new Mock<ILogger<RoleManager<IdentityRole>>>().Object);

        if (!roleManager.RoleExistsAsync("Visitor").GetAwaiter().GetResult())
            roleManager.CreateAsync(new IdentityRole("Visitor")).GetAwaiter().GetResult();

        return (dbContext, userManager);
    }

    private static AuthController CreateController(UserManager<ApplicationUser> userManager)
    {
        var configMock = new Mock<IConfiguration>();
        return new AuthController(userManager, configMock.Object);
    }

    // ── generators ───────────────────────────────────────────────────────────

    private static readonly Gen<int> IndexGen = Gen.Choose(1, 999_999);

    private static readonly char[] PasswordChars = "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();
    private static readonly char[] NameChars = "abcdefghijklmnopqrstuvwxyz".ToCharArray();

    private static readonly Gen<string> PasswordGen =
        from len in Gen.Choose(6, 20)
        from chars in Gen.Elements(PasswordChars).ArrayOf(len)
        select new string(chars);

    private static readonly Gen<string> FullNameGen =
        from n in Gen.Choose(1, 5)
        from chars in Gen.Elements(NameChars).ArrayOf(n * 3)
        select new string(chars);

    private static readonly Arbitrary<(string email, string password, string fullName)> ValidRegistrationArb =
        Arb.ToArbitrary(
            from idx in IndexGen
            from password in PasswordGen
            from fullName in FullNameGen
            select ($"user{idx}@test.com", password, fullName));

    // ── Property 21 ──────────────────────────────────────────────────────────

    /// <summary>
    /// For any valid registration request (unique email, password ≥6 chars, non-empty fullName),
    /// after calling RegisterVisitor the created user must have IsApproved=true,
    /// role="Visitor", and ActivationDate set (not default).
    ///
    /// **Validates: Requirements 21**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RegisterVisitor_CreatesUserWithCorrectRoleAndIsApproved()
    {
        return Prop.ForAll(ValidRegistrationArb, input =>
        {
            var (email, password, fullName) = input;
            var dbName = $"prop21_{Guid.NewGuid()}";
            var (dbContext, userManager) = CreateRealUserManager(dbName);
            using (dbContext)
            {
                var controller = CreateController(userManager);

                var request = new RegisterVisitorRequest
                {
                    Email = email,
                    Password = password,
                    FullName = fullName
                };

                var result = controller.RegisterVisitor(request).GetAwaiter().GetResult();

                // Must return 200 OK
                if (result is not OkObjectResult)
                    return Prop.Label(false, $"Expected OkObjectResult but got {result.GetType().Name} for email={email}");

                // Verify user was created with IsApproved=true
                var user = userManager.FindByEmailAsync(email).GetAwaiter().GetResult();
                if (user == null)
                    return Prop.Label(false, $"User not found after registration: {email}");

                if (!user.IsApproved)
                    return Prop.Label(false, $"IsApproved should be true but was false for {email}");

                // Verify ActivationDate is set (not default DateTime)
                if (user.ActivationDate == default)
                    return Prop.Label(false, $"ActivationDate should be set but was default for {email}");

                // Verify role is "Visitor"
                var roles = userManager.GetRolesAsync(user).GetAwaiter().GetResult();
                if (!roles.Contains("Visitor"))
                    return Prop.Label(false, $"Expected role 'Visitor' but got [{string.Join(", ", roles)}] for {email}");

                return Prop.Label(true, $"OK: email={email}, IsApproved=true, role=Visitor, ActivationDate set");
            }
        });
    }

    // ── Property 22 ──────────────────────────────────────────────────────────

    /// <summary>
    /// For any email that already exists in the system, calling RegisterVisitor
    /// with that email must return ConflictObjectResult (HTTP 409).
    ///
    /// **Validates: Requirements 22**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RegisterVisitor_DuplicateEmail_Returns409Conflict()
    {
        return Prop.ForAll(ValidRegistrationArb, input =>
        {
            var (email, password, fullName) = input;
            var dbName = $"prop22_{Guid.NewGuid()}";
            var (dbContext, userManager) = CreateRealUserManager(dbName);
            using (dbContext)
            {
                var controller = CreateController(userManager);

                var request = new RegisterVisitorRequest
                {
                    Email = email,
                    Password = password,
                    FullName = fullName
                };

                // First registration — must succeed
                var firstResult = controller.RegisterVisitor(request).GetAwaiter().GetResult();
                if (firstResult is not OkObjectResult)
                    return Prop.Label(false, $"First registration failed unexpectedly for {email}: {firstResult.GetType().Name}");

                // Second registration with same email — must return 409
                var secondResult = controller.RegisterVisitor(request).GetAwaiter().GetResult();
                if (secondResult is not ConflictObjectResult)
                    return Prop.Label(false,
                        $"Expected ConflictObjectResult (409) on duplicate email={email} but got {secondResult.GetType().Name}");

                return Prop.Label(true, $"OK: duplicate email={email} correctly returned 409 Conflict");
            }
        });
    }
}
