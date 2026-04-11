using Microsoft.AspNetCore.Authorization;
<<<<<<< HEAD
=======
using Microsoft.AspNetCore.Identity;
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
using Microsoft.AspNetCore.Mvc;
using VinhKhanh.Application.UseCases;
using VinhKhanh.Infrastructure.Services;
using VinhKhanh.Infrastructure.Data;
using VinhKhanh.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace VinhKhanh.Admin.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController(
    AdminApproveUseCase approveUseCase, 
    GeminiAiService geminiAiService,
    AppDbContext dbContext,
<<<<<<< HEAD
    IWebHostEnvironment env) : ControllerBase
=======
    IWebHostEnvironment env,
    UserManager<ApplicationUser> userManager) : ControllerBase
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
{
    private static readonly HashSet<string> SupportedCategoryCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "FOOD_SNAIL",
        "FOOD_BBQ",
        "FOOD_STREET",
        "DRINK",
        "UTILITY"
    };

    [HttpGet("pois")]
    public async Task<IActionResult> GetPois(CancellationToken cancellationToken)
    {
        var pois = await dbContext.Pois
            .Include(p => p.Localizations)
            .Include(p => p.Owner)
            .OrderByDescending(p => p.Id)
            .ToListAsync(cancellationToken);

        var result = pois.Select(p =>
        {
            var viLocalization = p.Localizations.FirstOrDefault(l => l.LanguageCode == "vi");

            return new
            {
                id = p.Id,
                name = viLocalization?.Name ?? "Chưa có tên",
                categoryCode = NormalizeCategoryCode(p.CategoryCode, viLocalization?.Name, viLocalization?.Description),
                category = NormalizeCategoryCode(p.CategoryCode, viLocalization?.Name, viLocalization?.Description),
                imageUrl = p.ImageUrl,
                lat = p.Latitude,
                lng = p.Longitude,
                isApproved = p.IsApproved,
                status = p.Status.ToString(),
                isPremium = p.IsPremium,
                ownerName = p.Owner?.FullName ?? string.Empty
            };
        });

        return Ok(result);
    }

    private static string InferCategory(string? name, string? description)
    {
        var source = $"{name} {description}".Trim().ToLowerInvariant();

        if (source.Contains("oc") || source.Contains("ốc") || source.Contains("oyster") || source.Contains("snail") || source.Contains("hai san"))
        {
            return "FOOD_SNAIL";
        }

        if (source.Contains("bbq") || source.Contains("nuong") || source.Contains("nướng") || source.Contains("lau") || source.Contains("lẩu") || source.Contains("hotpot"))
        {
            return "FOOD_BBQ";
        }

        if (source.Contains("coffee") || source.Contains("ca phe") || source.Contains("cà phê") || source.Contains("drink") || source.Contains("beverage") || source.Contains("tra sua") || source.Contains("trà sữa"))
        {
            return "DRINK";
        }

        return "FOOD_STREET";
    }

    private static string NormalizeCategoryCode(string? categoryCode, string? nameFallback = null, string? descriptionFallback = null)
    {
        if (!string.IsNullOrWhiteSpace(categoryCode) && SupportedCategoryCodes.Contains(categoryCode))
        {
            return categoryCode.ToUpperInvariant();
        }

        return InferCategory(nameFallback, descriptionFallback);
    }

    [HttpGet("dashboard-summary")]
    public async Task<IActionResult> GetDashboardSummary(CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var todayUtc = nowUtc.Date;
        var startHourUtc = nowUtc.AddHours(-7).Date.AddHours(nowUtc.Hour - 7);

        var poisCount = await dbContext.Pois.CountAsync(cancellationToken);
        var visitCount = await dbContext.AnalyticsEvents.CountAsync(cancellationToken);
        var narrationCount = await dbContext.AnalyticsEvents.CountAsync(e => e.EventType == "narration", cancellationToken);

        var hourlyActivity = await dbContext.AnalyticsEvents
            .Where(e => e.Timestamp >= startHourUtc)
            .GroupBy(e => e.Timestamp.Hour)
            .Select(g => new
            {
                hour = g.Key,
                count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var activityMap = hourlyActivity.ToDictionary(item => item.hour, item => item.count);
        var activitySeries = Enumerable.Range(0, 8)
            .Select(offset =>
            {
                var hour = startHourUtc.AddHours(offset).Hour;
                return new
                {
                    time = $"{hour:00}:00",
                    count = activityMap.TryGetValue(hour, out var count) ? count : 0
                };
            })
            .ToList();

        var visitsToday = await dbContext.AnalyticsEvents.CountAsync(e => e.Timestamp >= todayUtc, cancellationToken);

        return Ok(new
        {
            poisCount,
            visitCount,
            narrationCount,
            visitsToday,
            activitySeries
        });
    }

    [HttpPost("approve/{poiId:int}")]  // legacy route
    [HttpPost("pois/{poiId:int}/approve")]
    public async Task<IActionResult> Approve(int poiId, CancellationToken cancellationToken)
    {
        try
        {
<<<<<<< HEAD
            var poi = await dbContext.Pois.FirstOrDefaultAsync(p => p.Id == poiId, cancellationToken);
            if (poi is null) return NotFound("POI không tồn tại.");
            poi.Status = PoiStatus.Approved;
            poi.IsApproved = true;
            poi.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
=======
            var success = await approveUseCase.ExecuteAsync(poiId, cancellationToken);
            if (!success) return NotFound("POI không tồn tại.");
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
            return Ok(new { success = true, message = "Đã duyệt thành công." });
        }
        catch (Exception ex)
        {
            return Problem($"Lỗi khi duyệt POI: {ex.Message}");
        }
    }

    [HttpGet("pois/pending")]
    public async Task<IActionResult> GetPendingPois(CancellationToken cancellationToken)
    {
        var pois = await dbContext.Pois
            .Include(p => p.Localizations)
            .Include(p => p.Owner)
            .Where(p => p.Status == PoiStatus.Pending_Approval)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        var result = pois.Select(p =>
        {
            var vi = p.Localizations.FirstOrDefault(l => l.LanguageCode == "vi");
            return new
            {
                id = p.Id,
                name = vi?.Name ?? "Chưa có tên",
                description = vi?.Description ?? string.Empty,
                imageUrl = p.ImageUrl,
                lat = p.Latitude,
                lng = p.Longitude,
                ownerName = p.Owner?.FullName ?? string.Empty,
                createdAt = p.CreatedAt
            };
        });

        return Ok(result);
    }

    [HttpPost("pois/{poiId:int}/reject")]
    public async Task<IActionResult> RejectPoi(int poiId, [FromBody] RejectPoiRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length < 10)
            return BadRequest(new { error = "Lý do từ chối phải có ít nhất 10 ký tự." });

        var poi = await dbContext.Pois.FirstOrDefaultAsync(p => p.Id == poiId, cancellationToken);
        if (poi is null) return NotFound("Không tìm thấy POI.");

        poi.Status = PoiStatus.Rejected;
        poi.RejectionReason = request.Reason;
        poi.IsApproved = false;
        poi.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true });
    }

    [HttpPost("pois/{poiId:int}/hide")]
    public async Task<IActionResult> HidePoi(int poiId, CancellationToken cancellationToken)
    {
        var poi = await dbContext.Pois.FirstOrDefaultAsync(p => p.Id == poiId, cancellationToken);
        if (poi is null) return NotFound("Không tìm thấy POI.");

        poi.Status = PoiStatus.Hidden;
        poi.IsApproved = false;
        poi.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true });
    }

    [HttpPost("ai/generate")]
    [Authorize(Roles = "Admin,ShopOwner")]
    public async Task<IActionResult> GenerateTranslations([FromBody] AiTranslationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Description))
            return BadRequest("Thiếu thông tin tiếng Việt để dịch.");

        try
        {
            var result = await geminiAiService.GenerateTranslationsAsync(request.Name, request.Description, cancellationToken);
            if (result == null)
            {
                return StatusCode(500, "Gemini không trả về dữ liệu dịch.");
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Lỗi AI dịch thuật: {ex.Message}");
        }
    }

    [HttpPost("pois")]
    public async Task<IActionResult> CreatePoi([FromForm] CreatePoiRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var mediaFolder = Path.Combine(env.WebRootPath ?? "wwwroot", "media");
            if (!Directory.Exists(mediaFolder)) Directory.CreateDirectory(mediaFolder);

            var poi = new Poi
            {
                BasePoiId = Guid.NewGuid().ToString("N").Substring(0, 10).ToLower(),
                CategoryCode = NormalizeCategoryCode(request.CategoryCode, request.NameVi, request.DescVi),
                Latitude = request.Lat,
                Longitude = request.Lng,
                Radius = request.Radius > 0 ? request.Radius : 50,
                ImageUrl = null,
                Priority = 0,
                IsApproved = true,
                Status = PoiStatus.Approved,
                OwnerId = request.OwnerId,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.Pois.Add(poi);
            await dbContext.SaveChangesAsync(cancellationToken);

            async Task<string?> UploadFileAsync(IFormFile? file, string prefix)
            {
                if (file == null || file.Length == 0) return null;
                var ext = Path.GetExtension(file.FileName);
                var newName = $"{prefix}_{Guid.NewGuid():N}{ext}";
                var path = Path.Combine(mediaFolder, newName);
                using var stream = new FileStream(path, FileMode.Create);
                await file.CopyToAsync(stream, cancellationToken);
                return $"/media/{newName}";
            }

            var imageUrl = await UploadFileAsync(request.Image, "img");
            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                poi.ImageUrl = imageUrl;
                dbContext.Pois.Update(poi);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var localizations = new List<PoiLocalization>
            {
                new PoiLocalization { PoiId = poi.Id, LanguageCode = "vi", Name = request.NameVi ?? "", Description = request.DescVi ?? "", AudioUrl = null },
                new PoiLocalization { PoiId = poi.Id, LanguageCode = "en", Name = request.NameEn ?? "", Description = request.DescEn ?? "", AudioUrl = null },
                new PoiLocalization { PoiId = poi.Id, LanguageCode = "ja", Name = request.NameJa ?? "", Description = request.DescJa ?? "", AudioUrl = null }
            };

            dbContext.PoiLocalizations.AddRange(localizations);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Ok(new { success = true, message = "Thêm POI thành công!", poiId = poi.Id });
        }
        catch (Exception ex)
        {
            return Problem(ex.Message);
        }
    }

    [HttpGet("pois/{poiId:int}")]
    public async Task<IActionResult> GetPoiById(int poiId, CancellationToken cancellationToken)
    {
        var poi = await dbContext.Pois
            .Include(p => p.Localizations)
            .FirstOrDefaultAsync(p => p.Id == poiId, cancellationToken);

        if (poi is null)
        {
            return NotFound("Không tìm thấy POI.");
        }

        string GetName(string languageCode) => poi.Localizations
            .FirstOrDefault(l => l.LanguageCode == languageCode)?.Name ?? string.Empty;

        string GetDescription(string languageCode) => poi.Localizations
            .FirstOrDefault(l => l.LanguageCode == languageCode)?.Description ?? string.Empty;

        return Ok(new
        {
            id = poi.Id,
            categoryCode = NormalizeCategoryCode(poi.CategoryCode, GetName("vi"), GetDescription("vi")),
            lat = poi.Latitude,
            lng = poi.Longitude,
            radius = poi.Radius,
            imageUrl = poi.ImageUrl,
            vi = new { name = GetName("vi"), description = GetDescription("vi") },
            en = new { name = GetName("en"), description = GetDescription("en") },
            ja = new { name = GetName("ja"), description = GetDescription("ja") }
        });
    }

    [HttpPut("pois/{poiId:int}")]
    public async Task<IActionResult> UpdatePoi(int poiId, [FromForm] CreatePoiRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var poi = await dbContext.Pois
                .Include(p => p.Localizations)
                .FirstOrDefaultAsync(p => p.Id == poiId, cancellationToken);

            if (poi is null)
            {
                return NotFound("Không tìm thấy POI để cập nhật.");
            }

            var mediaFolder = Path.Combine(env.WebRootPath ?? "wwwroot", "media");
            if (!Directory.Exists(mediaFolder))
            {
                Directory.CreateDirectory(mediaFolder);
            }

            async Task<string?> UploadFileAsync(IFormFile? file, string prefix)
            {
                if (file == null || file.Length == 0)
                {
                    return null;
                }

                var ext = Path.GetExtension(file.FileName);
                var newName = $"{prefix}_{Guid.NewGuid():N}{ext}";
                var path = Path.Combine(mediaFolder, newName);
                await using var stream = new FileStream(path, FileMode.Create);
                await file.CopyToAsync(stream, cancellationToken);
                return $"/media/{newName}";
            }

            poi.Latitude = request.Lat;
            poi.Longitude = request.Lng;
            poi.Radius = request.Radius > 0 ? request.Radius : poi.Radius;
            poi.CategoryCode = NormalizeCategoryCode(request.CategoryCode, request.NameVi, request.DescVi);
            poi.UpdatedAt = DateTime.UtcNow;

            var imageUrl = await UploadFileAsync(request.Image, "img");
            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                poi.ImageUrl = imageUrl;
            }

            UpsertLocalization(poi.Localizations, "vi", request.NameVi, request.DescVi);
            UpsertLocalization(poi.Localizations, "en", request.NameEn, request.DescEn);
            UpsertLocalization(poi.Localizations, "ja", request.NameJa, request.DescJa);

            await dbContext.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true, message = "Cập nhật POI thành công!" });
        }
        catch (Exception ex)
        {
            return Problem(ex.Message);
        }
    }

<<<<<<< HEAD
=======
    [HttpDelete("pois/{poiId:int}")]
    public async Task<IActionResult> DeletePoi(int poiId, CancellationToken cancellationToken)
    {
        var poi = await dbContext.Pois
            .Include(p => p.Localizations)
            .FirstOrDefaultAsync(p => p.Id == poiId, cancellationToken);

        if (poi is null) return NotFound(new { error = "Không tìm thấy POI." });

        dbContext.Pois.Remove(poi);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { success = true, message = "Đã xóa POI thành công." });
    }

    /// <summary>
    /// Admin tạo tài khoản chủ quán trực tiếp, IsApproved = true luôn.
    /// </summary>
    [HttpPost("shop-owners")]
    public async Task<IActionResult> CreateShopOwner([FromBody] CreateShopOwnerRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest(new { error = "Email, mật khẩu và họ tên không được để trống." });

        if (request.Password.Length < 6)
            return BadRequest(new { error = "Mật khẩu phải có ít nhất 6 ký tự." });

        var existing = await userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
            return Conflict(new { error = "Email này đã được sử dụng." });

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            IsApproved = true, // Admin tạo → duyệt luôn
            SecurityStamp = Guid.NewGuid().ToString()
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            return BadRequest(new { error = "Tạo tài khoản thất bại.", details = errors });
        }

        await userManager.AddToRoleAsync(user, "ShopOwner");

        return Ok(new { success = true, message = $"Đã tạo tài khoản chủ quán cho {request.Email}.", userId = user.Id });
    }

    /// <summary>
    /// Lấy danh sách tất cả chủ quán (đã duyệt + chờ duyệt).
    /// </summary>
    [HttpGet("shop-owners")]
    public async Task<IActionResult> GetShopOwners(CancellationToken cancellationToken)
    {
        var shopOwners = await userManager.GetUsersInRoleAsync("ShopOwner");
        var result = shopOwners
            .OrderByDescending(u => u.IsApproved)
            .ThenBy(u => u.FullName)
            .Select(u => new { u.Id, u.Email, u.FullName, u.PhoneNumber, u.IsApproved })
            .ToList();
        return Ok(result);
    }

    /// <summary>
    /// Admin duyệt tài khoản chủ quán đang chờ.
    /// </summary>
    [HttpPost("shop-owners/{userId}/approve")]
    public async Task<IActionResult> ApproveShopOwner(string userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return NotFound(new { error = "Không tìm thấy tài khoản." });

        if (user.IsApproved) return Conflict(new { error = "Tài khoản đã được duyệt." });

        user.IsApproved = true;
        await userManager.UpdateAsync(user);
        return Ok(new { success = true, message = $"Đã duyệt tài khoản {user.Email}." });
    }

    /// <summary>
    /// Admin xóa tài khoản chủ quán.
    /// </summary>
    [HttpDelete("shop-owners/{userId}")]
    public async Task<IActionResult> DeleteShopOwner(string userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return NotFound(new { error = "Không tìm thấy tài khoản." });

        var roles = await userManager.GetRolesAsync(user);
        if (!roles.Contains("ShopOwner"))
            return BadRequest(new { error = "Tài khoản này không phải chủ quán." });

        await userManager.DeleteAsync(user);
        return Ok(new { success = true, message = $"Đã xóa tài khoản {user.Email}." });
    }

>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
    private static void UpsertLocalization(ICollection<PoiLocalization> localizations, string languageCode, string? name, string? description)
    {
        var existing = localizations.FirstOrDefault(l => l.LanguageCode == languageCode);
        if (existing is null)
        {
            localizations.Add(new PoiLocalization
            {
                LanguageCode = languageCode,
                Name = name ?? string.Empty,
                Description = description ?? string.Empty,
                AudioUrl = null
            });
            return;
        }

        existing.Name = name ?? string.Empty;
        existing.Description = description ?? string.Empty;
    }
<<<<<<< HEAD
=======

    /// <summary>
    /// Admin reset mật khẩu cho bất kỳ user nào (không cần mật khẩu cũ).
    /// </summary>
    [HttpPost("users/{userId}/reset-password")]
    public async Task<IActionResult> ResetUserPassword(string userId, [FromBody] AdminResetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            return BadRequest(new { error = "Mật khẩu mới phải có ít nhất 6 ký tự." });

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return NotFound(new { error = "Không tìm thấy tài khoản." });

        // Dùng token reset để bypass mật khẩu cũ
        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);

        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            return BadRequest(new { error = "Reset mật khẩu thất bại.", details = errors });
        }

        return Ok(new { success = true, message = $"Đã reset mật khẩu cho {user.Email}." });
    }

    /// <summary>
    /// Lấy danh sách tất cả users (Admin + ShopOwner) để Admin quản lý.
    /// </summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        var shopOwners = await userManager.GetUsersInRoleAsync("ShopOwner");
        var admins = await userManager.GetUsersInRoleAsync("Admin");

        var allUsers = shopOwners.Select(u => new { u.Id, u.Email, u.FullName, u.IsApproved, role = "ShopOwner" })
            .Concat(admins.Select(u => new { u.Id, u.Email, u.FullName, u.IsApproved, role = "Admin" }))
            .OrderBy(u => u.role)
            .ThenBy(u => u.Email)
            .ToList();

        return Ok(allUsers);
    }
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
}

public class AiTranslationRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class RejectPoiRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class CreatePoiRequest
{
    public double Lat { get; set; }
    public double Lng { get; set; }
    public int Radius { get; set; }
    public string? CategoryCode { get; set; }
    public string? OwnerId { get; set; }

    public string? NameVi { get; set; }
    public string? DescVi { get; set; }
    public string? NameEn { get; set; }
    public string? DescEn { get; set; }
    public string? NameJa { get; set; }
    public string? DescJa { get; set; }

    public IFormFile? Image { get; set; }
}
<<<<<<< HEAD
=======

public class AdminResetPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}

public class CreateShopOwnerRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
}
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
