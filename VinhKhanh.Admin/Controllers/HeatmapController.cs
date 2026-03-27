using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VinhKhanh.Admin.Data;

namespace VinhKhanh.Admin.Controllers;

[ApiController]
[Route("api/heatmap")]
public class HeatmapController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var stats = await db.NarrationEvents
            .GroupBy(e => e.PoiId)
            .Select(g => new
            {
                PoiId = g.Key,
                Count = g.Count(),
                LastTriggered = g.Max(e => e.TriggeredAt)
            })
            .ToListAsync();

        var poiNames = await db.Pois
            .Where(p => stats.Select(s => s.PoiId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name);

        var result = stats.Select(s => new
        {
            s.PoiId,
            PoiName = poiNames.GetValueOrDefault(s.PoiId, $"POI #{s.PoiId}"),
            s.Count,
            s.LastTriggered
        });

        return Ok(result);
    }
}
