using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VinhKhanh.Domain.Entities;
using VinhKhanh.Infrastructure.Data;

namespace VinhKhanh.Admin.Controllers;

[ApiController]
[Route("api/qr")]
public class QrController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet("{token}")]
    public async Task<IActionResult> Resolve(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new { error = "Qr token is required." });
        }

        var poi = await dbContext.Pois
            .Include(p => p.Localizations)
            .FirstOrDefaultAsync(
                p => p.QrToken == token && p.Status == PoiStatus.Approved,
                cancellationToken);

        if (poi is null)
        {
            return NotFound(new { error = "POI not found for this QR token." });
        }

        return Ok(new
        {
            poiId = poi.Id,
            basePoiId = poi.BasePoiId,
            qrToken = poi.QrToken,
            lat = poi.Latitude,
            lng = poi.Longitude,
            radius = poi.Radius,
            imageUrl = poi.ImageUrl,
            localizations = poi.Localizations
                .OrderBy(l => l.LanguageCode)
                .Select(l => new
                {
                    languageCode = l.LanguageCode,
                    name = l.Name,
                    description = l.Description,
                    audioUrl = l.AudioUrl
                })
        });
    }
}
