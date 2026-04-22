using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using VinhKhanh.Domain.Entities;
using VinhKhanh.Infrastructure.Data;

namespace VinhKhanh.Admin.Controllers;

[ApiController]
[Route("api/qr")]
public class QrController(AppDbContext dbContext, IConfiguration configuration) : ControllerBase
{
    [HttpGet("/qr/{token}")]
    public async Task<IActionResult> OpenPublicPoiPage(string token, CancellationToken cancellationToken)
    {
        var webBaseUrl = await GetSystemSettingAsync("web.app.baseUrl", cancellationToken)
            ?? configuration["VITE_WEB_BASE_URL"]
            ?? Environment.GetEnvironmentVariable("VITE_WEB_BASE_URL")
            ?? "http://localhost:3000";

        if (string.IsNullOrWhiteSpace(webBaseUrl) || webBaseUrl.Contains(Request.Host.Value!))
        {
            return Content("Mã QR không hoạt động vì Backend chưa được cấu hình đường dẫn tới Frontend.", "text/plain");
        }

        return Redirect($"{webBaseUrl.TrimEnd('/')}/poi/qr/{Uri.EscapeDataString(token)}");
    }

    [HttpGet("{token}")]
    public async Task<IActionResult> Resolve(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new { error = "Không tìm thấy token của mã QR." });
        }

        var poi = await dbContext.Pois
            .Include(p => p.Localizations)
            .FirstOrDefaultAsync(
                p => p.QrToken == token && p.Status == PoiStatus.Approved,
                cancellationToken);

        if (poi is null)
        {
            return NotFound(new { error = "Không tìm thấy POI nào khớp với mã QR này." });
        }

        var androidStore = await GetSystemSettingAsync("mobile.download.android", cancellationToken)
            ?? "https://play.google.com/store";
        var iosStore = await GetSystemSettingAsync("mobile.download.ios", cancellationToken)
            ?? "https://www.apple.com/app-store/";

        var webBaseUrl = await GetSystemSettingAsync("web.app.baseUrl", cancellationToken)
            ?? Environment.GetEnvironmentVariable("VITE_WEB_BASE_URL")
            ?? $"{Request.Scheme}://{Request.Host}";
        var webPoiUrl = $"{webBaseUrl.TrimEnd('/')}/poi/qr/{poi.QrToken}";
        var deepLink = $"vinhkhanh://poi/{poi.Id}?token={Uri.EscapeDataString(poi.QrToken ?? string.Empty)}";

        return Ok(new
        {
            poiId = poi.Id,
            basePoiId = poi.BasePoiId,
            qrToken = poi.QrToken,
            lat = poi.Latitude,
            lng = poi.Longitude,
            radius = poi.Radius,
            imageUrl = poi.ImageUrl,
            webPoiUrl,
            deepLink,
            appLinks = new
            {
                android = androidStore,
                ios = iosStore
            },
            localizations = poi.Localizations
                .OrderBy(l => l.LanguageCode)
                .Select(l => new
                {
                    languageCode = l.LanguageCode,
                    name = l.Name,
                    description = l.Description
                })
        });
    }

    [HttpGet("{token}/png")]
    public async Task<IActionResult> GetQrPng(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new { error = "Token không hợp lệ." });
        }

        var poi = await dbContext.Pois
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.QrToken == token, cancellationToken);

        if (poi is null)
        {
            return NotFound(new { error = "Không tìm thấy POI cho mã QR." });
        }

        // CHIEN THUAT LAY BASE URL SIEU MANH:
        // 1. Uu tien 'Referer' (Địa chỉ browser đang mở) - day la cach chinh xac nhat de lay link Ngrok
        var referer = Request.Headers["Referer"].ToString();
        var origin = Request.Headers["Origin"].ToString();
        
        string baseUrl = "";
        if (!string.IsNullOrWhiteSpace(referer))
        {
            try {
                var uri = new Uri(referer);
                baseUrl = $"{uri.Scheme}://{uri.Host}{(uri.IsDefaultPort ? "" : ":" + uri.Port)}";
            } catch { }
        }
        
        if (string.IsNullOrWhiteSpace(baseUrl) && !string.IsNullOrWhiteSpace(origin))
        {
            baseUrl = origin.TrimEnd('/');
        }

        // 2. Neu Header không co hoac van la localhost, moi dung Request.Host (đã được UseForwardedHeaders xử lý)
        if (string.IsNullOrWhiteSpace(baseUrl) || baseUrl.Contains("localhost") || baseUrl.Contains("127.0.0.1"))
        {
             baseUrl = $"{Request.Scheme}://{Request.Host}";
        }

        // 3. Neu van la localhost, ta moi dung fallback cuoi cung tu DB/Config
        if (baseUrl.Contains("localhost") || baseUrl.Contains("127.0.0.1"))
        {
             var webBaseUrl = await GetSystemSettingAsync("web.app.baseUrl", cancellationToken)
                ?? configuration["VITE_WEB_BASE_URL"]
                ?? Environment.GetEnvironmentVariable("VITE_WEB_BASE_URL");
             
             if (!string.IsNullOrWhiteSpace(webBaseUrl))
             {
                 baseUrl = webBaseUrl.TrimEnd('/');
             }
        }

        var qrContent = $"{baseUrl.TrimEnd('/')}/poi/qr/{token}";

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(qrContent, QRCodeGenerator.ECCLevel.Q);
        var pngQrCode = new PngByteQRCode(data);
        var qrBytes = pngQrCode.GetGraphic(20);

        return File(qrBytes, "image/png", $"poi-{poi.Id}-qr.png");
    }

    private async Task<string?> GetSystemSettingAsync(string key, CancellationToken cancellationToken)
    {
        return await dbContext.SystemSettings
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
