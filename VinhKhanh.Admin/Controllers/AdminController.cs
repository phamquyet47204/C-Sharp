using Microsoft.AspNetCore.Authorization;
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
    GeminiAiService geminiAiService,
    AppDbContext dbContext,
    IWebHostEnvironment env,
    Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> userManager) : ControllerBase
{
    private static readonly HashSet<string> SupportedCategoryCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "FOOD_SNAIL",
        "FOOD_BBQ",
        "FOOD_STREET",
        "DRINK",
        "UTILITY"
    };

    /// <summary>
    /// POST /api/admin/approve-owner/{userId}
    /// Phê duyệt tài khoản Chủ quán (ShopOwner).
    /// </summary>
    [HttpPost("approve-owner/{userId}")]
    public async Task<IActionResult> ApproveOwner(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null) return NotFound("Không tìm thấy người dùng.");
        if (user.IsApproved) return BadRequest("Tài khoản đã được duyệt từ trước.");
        
        user.IsApproved = true;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded) return Problem("Có lỗi xảy ra khi duyệt tài khoản.");
        
        return Ok(new { success = true, message = "Đã duyệt ShopOwner thành công." });
    }

    /// <summary>
    /// GET /api/admin/users/owners
    /// Lấy danh sách toàn bộ Chủ quán.
    /// </summary>
    [HttpGet("users/owners")]
    public async Task<IActionResult> GetOwners()
    {
        var users = await userManager.GetUsersInRoleAsync("ShopOwner");
        var userIds = users.Select(u => u.Id).ToList();
        
        var pois = await dbContext.Pois
            .Where(p => userIds.Contains(p.OwnerId))
            .ToListAsync();

        var result = users.Select(u => {
            var poi = pois.FirstOrDefault(p => p.OwnerId == u.Id);
            return new
            {
                id = u.Id,
                fullName = u.FullName,
                email = u.Email,
                phoneNumber = u.PhoneNumber,
                isApproved = u.IsApproved,
                activationDate = u.ActivationDate,
                isPremium = poi?.IsPremium ?? false,
                premiumExpiryDate = poi?.PremiumExpiryDate
            };
        });
        return Ok(result);
    }

    /// <summary>
    /// GET /api/admin/users/pending-owners
    /// Lấy danh sách Chủ quán đang chờ duyệt.
    /// </summary>
    [HttpGet("users/pending-owners")]
    public async Task<IActionResult> GetPendingOwners()
    {
        var users = await userManager.GetUsersInRoleAsync("ShopOwner");
        var result = users.Where(u => !u.IsApproved).Select(u => new
        {
            id = u.Id,
            fullName = u.FullName,
            email = u.Email,
            phoneNumber = u.PhoneNumber,
            activationDate = u.ActivationDate
        });
        return Ok(result);
    }

    /// <summary>
    /// PUT /api/admin/users/{userId}
    /// Cập nhật thông tin Chủ quán.
    /// </summary>
    [HttpPut("users/{userId}")]
    public async Task<IActionResult> UpdateOwner(string userId, [FromBody] UpdateOwnerRequest request)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null) return NotFound("Không tìm thấy người dùng.");
        
        user.FullName = request.FullName ?? user.FullName;
        user.PhoneNumber = request.PhoneNumber ?? user.PhoneNumber;
        
        var userResult = await userManager.UpdateAsync(user);
        if (!userResult.Succeeded) return Problem("Lỗi khi cập nhật thông tin.");

        // Xử lý Premium nếu có tùy chọn
        if (request.PremiumOption != null)
        {
            var poi = await dbContext.Pois.FirstOrDefaultAsync(p => p.OwnerId == userId);
            if (poi != null)
            {
                if (request.PremiumOption == "None")
                {
                    poi.IsPremium = false;
                    poi.Priority = 0;
                    poi.PremiumExpiryDate = null;
                }
                else
                {
                    poi.IsPremium = true;
                    poi.Priority = 100;
                    var months = request.PremiumOption switch
                    {
                        "1Month" => 1,
                        "6Months" => 6,
                        "1Year" => 12,
                        _ => 0
                    };
                    
                    if (months > 0)
                    {
                        // Luôn gia hạn từ thời điểm hiện tại
                        poi.PremiumExpiryDate = DateTime.UtcNow.AddMonths(months);
                    }
                }
                poi.UpdatedAt = DateTime.UtcNow;
                int bonus = 0;
                await dbContext.SaveChangesAsync();
            }
        }
        
        return Ok(new { success = true });
    }

    /// <summary>
    /// POST /api/admin/users/{userId}/reject-owner
    /// Từ chối và xóa tài khoản chủ quán đang chờ duyệt.
    /// </summary>
    [HttpPost("users/{userId}/reject-owner")]
    public async Task<IActionResult> RejectOwner(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null) return NotFound("Không tìm thấy người dùng.");
        
        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded) return Problem("Lỗi khi xóa tài khoản.");
        
        return Ok(new { success = true });
    }

    /// <summary>
    /// POST /api/admin/users/{userId}/toggle-premium
    /// Bật/Tắt trạng thái Premium cho quán của Owner này.
    /// </summary>
    [HttpPost("users/{userId}/toggle-premium")]
    public async Task<IActionResult> TogglePremium(string userId)
    {
        var poi = await dbContext.Pois.FirstOrDefaultAsync(p => p.OwnerId == userId);
        if (poi == null) return NotFound("Chủ quán chưa có POI nào để nâng cấp Premium.");
        
        poi.IsPremium = !poi.IsPremium;
        poi.Priority = poi.IsPremium ? 100 : 0; // Tăng ưu tiên lên 100 nếu là Premium
        poi.UpdatedAt = DateTime.UtcNow;
        
        await dbContext.SaveChangesAsync();
        return Ok(new { success = true, isPremium = poi.IsPremium, priority = poi.Priority });
    }

    /// <summary>
    /// GET /api/admin/pois
    /// Lấy danh sách toàn bộ các địa điểm (POI) bao gồm thông tin chi tiết và ngôn ngữ.
    /// </summary>
    [HttpGet("pois")]
    public async Task<IActionResult> GetPois(CancellationToken cancellationToken)
    {
        var pois = await dbContext.Pois
            .Include(p => p.Localizations)
            .Include(p => p.Owner)
            .OrderByDescending(p => p.Id)
            .ToListAsync(cancellationToken);

        // Lấy thống kê lượt nghe (Số lượng thực tế)
        var stats = await dbContext.AnalyticsEvents
            .Where(e => e.PoiId.HasValue && e.EventType == "narration")
            .GroupBy(e => e.PoiId!.Value)
            .Select(g => new {
                poiId = g.Key,
                count = g.Count()
            })
            .ToDictionaryAsync(x => x.poiId, x => x.count, cancellationToken);

        var result = pois.Select(p =>
        {
            var viLocalization = p.Localizations.FirstOrDefault(l => l.LanguageCode == "vi");
            stats.TryGetValue(p.Id, out var narrationCount);

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
                ownerName = p.Owner?.FullName ?? string.Empty,
                totalNarrations = narrationCount
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
        // Tính toán mốc "Hôm nay" theo giờ Việt Nam (+7)
        var vnNow = DateTime.UtcNow.AddHours(7);
        var vnTodayStart = new DateTime(vnNow.Year, vnNow.Month, vnNow.Day, 0, 0, 0, DateTimeKind.Unspecified).AddHours(-7);
        // Biểu đồ 8 giờ qua
        var startHourUtc = DateTime.UtcNow.AddHours(-8);

        var poisCount = await dbContext.Pois.CountAsync(cancellationToken);
        
        // Đếm tổng lượt truy cập duy nhất (DeviceId + Ngày)
        var visitCount = await dbContext.AnalyticsEvents
            .Select(e => new { e.DeviceId, Date = e.Timestamp.Date })
            .Distinct()
            .CountAsync(cancellationToken);

        var narrationCount = await dbContext.AnalyticsEvents.CountAsync(e => e.EventType == "narration", cancellationToken);

        // Đếm số người online (active trong 5 phút qua)
        var onlineThreshold = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(5));
        var onlineCount = await dbContext.AnalyticsEvents
            .Where(e => e.Timestamp >= onlineThreshold)
            .Select(e => e.DeviceId)
            .Distinct()
            .CountAsync(cancellationToken);

        var hourlyActivity = await dbContext.AnalyticsEvents
            .Where(e => e.Timestamp >= startHourUtc)
            .GroupBy(e => e.Timestamp.Hour)
            .Select(g => new
            {
                hour = g.Key,
                count = g.Select(e => e.DeviceId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);

        var activityMap = hourlyActivity.ToDictionary(item => item.hour, item => item.count);
        var activitySeries = Enumerable.Range(0, 8)
            .Select(offset =>
            {
                var hourUtc = startHourUtc.AddHours(offset);
                var hourLocal = hourUtc.AddHours(7).Hour;
                return new
                {
                    time = $"{hourLocal:00}:00",
                    count = activityMap.TryGetValue(hourUtc.Hour, out var count) ? count : 0
                };
            })
            .ToList();

        var visitsToday = await dbContext.AnalyticsEvents
            .Where(e => e.Timestamp >= vnTodayStart)
            .Select(e => e.DeviceId)
            .Distinct()
            .CountAsync(cancellationToken);
            
        var narrationCountToday = await dbContext.AnalyticsEvents.CountAsync(e => e.EventType == "narration" && e.Timestamp >= vnTodayStart, cancellationToken);
        
        // Thống kê chủ quán
        var totalShops = await userManager.GetUsersInRoleAsync("ShopOwner");
        var pendingOwnersCount = totalShops.Count(u => !u.IsApproved);

        return Ok(new
        {
            poisCount,
            visitCount,
            narrationCount,
            narrationCountToday,
            visitsToday,
            onlineCount,
            totalShopsCount = totalShops.Count,
            pendingOwnersCount,
            activitySeries
        });
    }

    /// <summary>
    /// POST /api/admin/pois/{poiId}/approve
    /// Phê duyệt một POI đang ở trạng thái Nháp (Draft).
    /// </summary>
    [HttpPost("approve/{poiId:int}")]  // Đường dẫn cũ để tương thích với FE
    [HttpPost("pois/{poiId:int}/approve")]
    public async Task<IActionResult> Approve(int poiId, CancellationToken cancellationToken)
    {
        try
        {
            var poi = await dbContext.Pois.FirstOrDefaultAsync(p => p.Id == poiId, cancellationToken);
            if (poi is null) return NotFound("POI không tồn tại.");
            poi.Status = PoiStatus.Approved;
            poi.IsApproved = true;
            poi.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
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
                QrToken = $"poi-{Guid.NewGuid():N}"[..20].ToLowerInvariant(),
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

            dbContext.PoiLocalizations.AddRange(
                new PoiLocalization { PoiId = poi.Id, LanguageCode = "vi", Name = request.NameVi ?? "", Description = request.DescVi ?? "" },
                new PoiLocalization { PoiId = poi.Id, LanguageCode = "en", Name = request.NameEn ?? "", Description = request.DescEn ?? "" },
                new PoiLocalization { PoiId = poi.Id, LanguageCode = "ja", Name = request.NameJa ?? "", Description = request.DescJa ?? "" }
            );

            await dbContext.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                success = true,
                message = "Thêm POI thành công!",
                poiId = poi.Id,
                qrToken = poi.QrToken,
                qrLink = BuildQrLink(poi.QrToken)
            });
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

        var narrationCount = await dbContext.AnalyticsEvents
            .Where(e => e.PoiId == poiId && e.EventType == "narration")
            .CountAsync(cancellationToken);

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
            qrToken = poi.QrToken,
            qrLink = BuildQrLink(poi.QrToken),
            totalNarrations = narrationCount,
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

    private static void UpsertLocalization(ICollection<PoiLocalization> localizations, string languageCode, string? name, string? description)
    {
        var existing = localizations.FirstOrDefault(l => l.LanguageCode == languageCode);
        if (existing is null)
        {
            localizations.Add(new PoiLocalization
            {
                LanguageCode = languageCode,
                Name = name ?? string.Empty,
                Description = description ?? string.Empty
            });
            return;
        }

        existing.Name = name ?? string.Empty;
        existing.Description = description ?? string.Empty;
    }

    private string? BuildQrLink(string? qrToken)
    {
        if (string.IsNullOrWhiteSpace(qrToken))
        {
            return null;
        }

        return $"{Request.Scheme}://{Request.Host}/qr/{Uri.EscapeDataString(qrToken)}";
    }

    /// <summary>
    /// POST /api/admin/pois/{poiId}/reset-qr
    /// Hủy mã QR cũ và sinh ra một mã QR mới cho dự án.
    /// </summary>
    [HttpPost("pois/{poiId:int}/reset-qr")]
    public async Task<IActionResult> ResetQrToken(int poiId, CancellationToken cancellationToken)
    {
        var poi = await dbContext.Pois.FirstOrDefaultAsync(p => p.Id == poiId, cancellationToken);
        if (poi is null) return NotFound("POI không tồn tại.");
        
        string token;
        do
        {
            token = $"poi-{Guid.NewGuid():N}"[..20].ToLowerInvariant();
        }
        while (await dbContext.Pois.AnyAsync(p => p.QrToken == token, cancellationToken));
        
        poi.QrToken = token;
        poi.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        
        return Ok(new { success = true, newQrToken = token, newQrLink = BuildQrLink(token) });
    }

    /// <summary>
    /// DELETE /api/admin/pois/{poiId}
    /// Xóa hoàn toàn một POI và các dữ liệu liên quan (Localizations, Ratings).
    /// </summary>
    [HttpDelete("pois/{poiId:int}")]
    public async Task<IActionResult> DeletePoi(int poiId, CancellationToken cancellationToken)
    {
        try
        {
            var poi = await dbContext.Pois.FirstOrDefaultAsync(p => p.Id == poiId, cancellationToken);
            if (poi is null) return NotFound("POI không tồn tại.");

            // Do DB đã cấu hình DeleteBehavior.Cascade cho Localizations và Ratings trong AppDbContext,
            // nên ta chỉ cần xóa thực thể Poi chính.
            dbContext.Pois.Remove(poi);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Ok(new { success = true, message = "Đã xóa POI thành công." });
        }
        catch (Exception ex)
        {
            return Problem($"Lỗi khi xóa POI: {ex.Message}");
        }
    }
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

public class UpdateOwnerRequest
{
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? PremiumOption { get; set; } // None, 1Month, 6Months, 1Year
}
