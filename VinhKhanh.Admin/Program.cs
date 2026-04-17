using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using VinhKhanh.Application.UseCases;
using VinhKhanh.Domain.Entities;
using VinhKhanh.Domain.Interfaces;
using VinhKhanh.Infrastructure.Data;
using VinhKhanh.Infrastructure.Repositories;
using VinhKhanh.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

// 1. DATABASE SETUP
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// 2. IDENTITY SETUP
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => {
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// 3. REPOSITORIES & SECURITY (INFRASTRUCTURE LAYER)
builder.Services.AddScoped<IPoiRepository, PoiRepository>();
builder.Services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
builder.Services.AddSingleton(new EncryptionUtility(builder.Configuration["Jwt:Key"] ?? "default_key_fallback"));

// 4. USE CASES & SERVICES (APPLICATION LAYER)
builder.Services.AddHttpClient<VinhKhanh.Infrastructure.Services.GeminiAiService>();
builder.Services.AddScoped<PoiSyncUseCase>();
builder.Services.AddScoped<AnalyticsVisitUseCase>();
builder.Services.AddScoped<AdminApproveUseCase>();

// 5. API CONFIGURATION & CORS
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy => {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// 6. JWT SECURITY
var jwtKey = builder.Configuration["Jwt:Key"] ?? "VinhKhanh_CleanArchitecture_Super_Secret_Key_2026";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "VinhKhanh.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "VinhKhanh.Clients";

builder.Services.AddAuthentication(options => {
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/", () => Results.Ok(new { status = "ok", message = "VinhKhanh.Admin backend is running" }));
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapControllers();

// SEED DATA: Identity Roles & Default Admin
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    try
    {
        // Retry kết nối DB tối đa 5 lần (SQL Server Docker cần thời gian khởi động)
        var connected = false;
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                // Dùng OpenConnection thay vì CanConnectAsync để lấy error message chi tiết
                await dbContext.Database.OpenConnectionAsync();
                await dbContext.Database.CloseConnectionAsync();
                connected = true;
                break;
            }
            catch (Exception retryEx)
            {
                Console.WriteLine($"[DB] Attempt {attempt}/5 failed: {retryEx.GetType().Name}: {retryEx.Message}");
                if (retryEx.InnerException != null)
                    Console.WriteLine($"[DB] Inner: {retryEx.InnerException.GetType().Name}: {retryEx.InnerException.Message}");
                if (retryEx.InnerException?.InnerException != null)
                    Console.WriteLine($"[DB] Inner2: {retryEx.InnerException.InnerException.Message}");
            }
            await Task.Delay(5000);
        }

        if (!connected)
        {
            throw new InvalidOperationException(
                "Không thể kết nối đến database SQL Server. Kiểm tra lại connection string, instance SQLEXPRESS và trạng thái dịch vụ SQL Server.");
        }

        dbContext.Database.Migrate(); // Tự động áp dụng Migration

        dbContext.Database.Migrate(); // Tự động áp dụng Migration
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[Database Startup Error] {ex.Message}");
        Console.ResetColor();
        throw;
    }

    string[] roles = { "Admin", "ShopOwner", "Visitor" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    var admin = await userManager.FindByEmailAsync("admin@vinhkhanh.vn");
    if (admin == null)
    {
        admin = new ApplicationUser 
        { 
            UserName = "admin@vinhkhanh.vn", 
            Email = "admin@vinhkhanh.vn", 
            FullName = "Admin Tổng", 
            IsApproved = true 
        };
        await userManager.CreateAsync(admin, "Admin123!");
        await userManager.AddToRoleAsync(admin, "Admin");
    }
}

app.Run();
