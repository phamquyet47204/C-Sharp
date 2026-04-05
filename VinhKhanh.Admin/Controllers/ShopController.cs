using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VinhKhanh.Domain.Entities;
using VinhKhanh.Infrastructure.Data;

namespace VinhKhanh.Admin.Controllers;

[ApiController]
[Route("api/shop")]
[Authorize(Roles = "ShopOwner")]
public class ShopController(
    AppDbContext dbContext,
    IWebHostEnvironment env,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private async Task<bool> IsApprovedAsync(CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(CurrentUserId!);
        return user?.IsApproved == true;
    }

    private async Task<string?> UploadImageAsync(IFormFile? file, CancellationToken ct)
    {
        if (file == null || file.Length == 0) return null;
        var mediaFolder = Path.Combine(env.WebRootPath ?? "wwwroot", "media");
        if (!Directory.Exists(mediaFolder)) Directory.CreateDirectory(mediaFolder);
        var ext = Path.GetExtension(file.FileName);
        var newName = $"img_{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(mediaFolder, newName);
        await using var stream = new FileStream(path, FileMode.Create);
        await file.CopyToAsync(stream, ct);
        return $"/media/{newName}";
    }

    private static void UpsertLocalization(ICollection<PoiLocalization> localizations, string languageCode, string? name, string? description)
    {
        var existing = localizations.FirstOrDefault(l => l.LanguageCode == languageCode);
        if (existing is null)
        {
            localizations.Add(new PoiLocalization { LanguageCode = languageCode, Name = name ?? string.Empty, Description = description ?? string.Empty });
            return;
        }
        existing.Name = name ?? string.Empty;
        existing.Description = description ?? string.Empty;
    }

    [HttpGet("pois/{id:int}")]
    public async Task<IActionResult> GetMyPoi(int id, CancellationToken ct)
    {
        if (!await IsApprovedAsync(ct)) return StatusCode(403, "Tài khoản chưa được duyệt.");
        var poi = await dbContext.Pois.Include(p => p.Localizations)
            .FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == CurrentUserId, ct);
        if (poi is null) return NotFound("Không tìm thấy POI.");
        string Get(string lang, string field) => poi.Localizations
            .FirstOrDefault(l => l.LanguageCode == lang) is { } loc
            ? (field == "name" ? loc.Name : loc.Description) : string.Empty;
        return Ok(new {
            id = poi.Id, imageUrl = poi.ImageUrl,
            lat = poi.Latitude, lng = poi.Longitude, radius = poi.Radius,
            categoryCode = poi.CategoryCode,
            vi = new { name = Get("vi","name"), description = Get("vi","desc") },
            en = new { name = Get("en","name"), description = Get("en","desc") },
            ja = new { name = Get("ja","name"), description = Get("ja","desc") },
        });
    }

    [HttpGet("pois")]
    public async Task<IActionResult> GetMyPois(CancellationToken ct)
    {
        if (!await IsApprovedAsync(ct)) return StatusCode(403, "Tài khoản chưa được duyệt.");
        var userId = CurrentUserId;
        var pois = await dbContext.Pois.Include(p => p.Localizations)
            .Where(p => p.OwnerId == userId).OrderByDescending(p => p.Id).ToListAsync(ct);
        return Ok(pois.Select(p =>
        {
            var vi = p.Localizations.FirstOrDefault(l => l.LanguageCode == "vi");
            return new { id = p.Id, name = vi?.Name ?? string.Empty, status = p.Status.ToString(), isPremium = p.IsPremium, imageUrl = p.ImageUrl, lat = p.Latitude, lng = p.Longitude, rejectionReason = p.RejectionReason };
        }));
    }

    [HttpPost("pois")]
    public async Task<IActionResult> CreatePoi([FromForm] CreateShopPoiRequest request, CancellationToken ct)
    {
        if (!await IsApprovedAsync(ct)) return StatusCode(403, "Tài khoản chưa được duyệt.");
        var userId = CurrentUserId!;
        var poi = new Poi
        {
            BasePoiId = Guid.NewGuid().ToString("N")[..10].ToLower(),
            CategoryCode = request.CategoryCode ?? "FOOD_STREET",
            Latitude = request.Lat, Longitude = request.Lng,
            Radius = request.Radius > 0 ? request.Radius : 50,
            Status = PoiStatus.Draft, OwnerId = userId, IsApproved = false, UpdatedAt = DateTime.UtcNow
        };
        dbContext.Pois.Add(poi);
        await dbContext.SaveChangesAsync(ct);
        var imageUrl = await UploadImageAsync(request.Image, ct);
        if (!string.IsNullOrWhiteSpace(imageUrl)) { poi.ImageUrl = imageUrl; await dbContext.SaveChangesAsync(ct); }
        dbContext.PoiLocalizations.AddRange(
            new PoiLocalization { PoiId = poi.Id, LanguageCode = "vi", Name = request.NameVi ?? string.Empty, Description = request.DescVi ?? string.Empty },
            new PoiLocalization { PoiId = poi.Id, LanguageCode = "en", Name = request.NameEn ?? string.Empty, Description = request.DescEn ?? string.Empty },
            new PoiLocalization { PoiId = poi.Id, LanguageCode = "ja", Name = request.NameJa ?? string.Empty, Description = request.DescJa ?? string.Empty });
        await dbContext.SaveChangesAsync(ct);
        return Ok(new { success = true, poiId = poi.Id });
    }

    [HttpPut("pois/{id:int}")]
    public async Task<IActionResult> UpdatePoi(int id, [FromForm] CreateShopPoiRequest request, CancellationToken ct)
    {
        if (!await IsApprovedAsync(ct)) return StatusCode(403, "Tài khoản chưa được duyệt.");
        var poi = await dbContext.Pois.Include(p => p.Localizations).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (poi is null) return NotFound("Không tìm thấy POI.");
        if (poi.OwnerId != CurrentUserId) return StatusCode(403, "Bạn không có quyền chỉnh sửa POI này.");
        if (poi.Status == PoiStatus.Pending_Approval)
            return StatusCode(403, "Không thể chỉnh sửa POI đang chờ duyệt.");
        poi.Latitude = request.Lat; poi.Longitude = request.Lng;
        poi.Radius = request.Radius > 0 ? request.Radius : poi.Radius;
        poi.CategoryCode = request.CategoryCode ?? poi.CategoryCode;
        poi.UpdatedAt = DateTime.UtcNow;
        var imageUrl = await UploadImageAsync(request.Image, ct);
        if (!string.IsNullOrWhiteSpace(imageUrl)) poi.ImageUrl = imageUrl;
        UpsertLocalization(poi.Localizations, "vi", request.NameVi, request.DescVi);
        UpsertLocalization(poi.Localizations, "en", request.NameEn, request.DescEn);
        UpsertLocalization(poi.Localizations, "ja", request.NameJa, request.DescJa);
        await dbContext.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpDelete("pois/{id:int}")]
    public async Task<IActionResult> DeletePoi(int id, CancellationToken ct)
    {
        if (!await IsApprovedAsync(ct)) return StatusCode(403, "Tài khoản chưa được duyệt.");
        var poi = await dbContext.Pois.Include(p => p.Localizations)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (poi is null) return NotFound("Không tìm thấy POI.");
        if (poi.OwnerId != CurrentUserId) return StatusCode(403, "Bạn không có quyền xóa POI này.");
        if (poi.Status == PoiStatus.Pending_Approval)
            return StatusCode(403, "Không thể xóa POI đang chờ duyệt.");
        dbContext.Pois.Remove(poi);
        await dbContext.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpPost("pois/{id:int}/submit")]
    public async Task<IActionResult> SubmitPoi(int id, CancellationToken ct)
    {
        if (!await IsApprovedAsync(ct)) return StatusCode(403, "Tài khoản chưa được duyệt.");
        var poi = await dbContext.Pois.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (poi is null) return NotFound("Không tìm thấy POI.");
        if (poi.OwnerId != CurrentUserId) return StatusCode(403, "Bạn không có quyền gửi duyệt POI này.");
        if (poi.Status != PoiStatus.Draft) return BadRequest("Chỉ có thể gửi duyệt POI ở trạng thái Draft.");
        poi.Status = PoiStatus.Pending_Approval;
        poi.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpPost("ai/generate")]
    public async Task<IActionResult> GenerateAI([FromBody] ShopAiRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Description))
            return BadRequest("Thiếu tên hoặc mô tả tiếng Việt.");
        var gemini = HttpContext.RequestServices.GetRequiredService<VinhKhanh.Infrastructure.Services.GeminiAiService>();
        var result = await gemini.GenerateTranslationsAsync(request.Name, request.Description, ct);
        if (result == null) return StatusCode(500, "Gemini không trả về dữ liệu.");
        return Ok(result);
    }

    [HttpGet("analytics")]
    public async Task<IActionResult> GetAnalytics(CancellationToken ct)    {
        if (!await IsApprovedAsync(ct)) return StatusCode(403, "Tài khoản chưa được duyệt.");
        var userId = CurrentUserId;
        var since = DateTime.UtcNow.AddDays(-30);
        var myPois = await dbContext.Pois.Include(p => p.Localizations).Where(p => p.OwnerId == userId).ToListAsync(ct);
        var poiIds = myPois.Select(p => p.Id).ToHashSet();
        var events = await dbContext.AnalyticsEvents
            .Where(e => e.PoiId.HasValue && poiIds.Contains(e.PoiId.Value) && e.Timestamp >= since).ToListAsync(ct);
        return Ok(new
        {
            totalVisits = events.Count(e => e.EventType == "visit"),
            totalNarrations = events.Count(e => e.EventType == "narration"),
            pois = myPois.Select(p =>
            {
                var viName = p.Localizations.FirstOrDefault(l => l.LanguageCode == "vi")?.Name ?? string.Empty;
                var pe = events.Where(e => e.PoiId == p.Id).ToList();
                return new { poiId = p.Id, poiName = viName, visits = pe.Count(e => e.EventType == "visit"), narrations = pe.Count(e => e.EventType == "narration") };
            })
        });
    }
}

public class CreateShopPoiRequest
{
    public double Lat { get; set; }
    public double Lng { get; set; }
    public int Radius { get; set; }
    public string? CategoryCode { get; set; }
    public string? NameVi { get; set; }
    public string? DescVi { get; set; }
    public string? NameEn { get; set; }
    public string? DescEn { get; set; }
    public string? NameJa { get; set; }
    public string? DescJa { get; set; }
    public IFormFile? Image { get; set; }
}

public class ShopAiRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
