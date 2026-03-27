using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VinhKhanh.Admin.Data;
using VinhKhanh.Shared.Models;

namespace VinhKhanh.Admin.Controllers;

[ApiController]
[Route("api")]
public class SyncController(AppDbContext db) : ControllerBase
{
    [HttpPost("sync")]
    public async Task<SyncResponse> Sync([FromBody] SyncRequest req)
    {
        var updated = await db.Pois
            .Where(p => p.UpdatedAt > req.LastSyncAt)
            .ToListAsync();

        return new SyncResponse
        {
            UpdatedPois = updated.Where(p => p.IsActive).ToList(),
            DeletedIds  = updated.Where(p => !p.IsActive).Select(p => p.Id).ToList()
        };
    }

    [HttpPost("narration-event")]
    public async Task<IActionResult> LogEvent([FromBody] NarrationEvent e)
    {
        db.NarrationEvents.Add(e);
        await db.SaveChangesAsync();
        return Ok();
    }
}

[ApiController]
[Route("api/pois")]
public class PoiController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public Task<List<Poi>> GetAll() =>
        db.Pois.OrderByDescending(p => p.Priority).ToListAsync();

    [HttpPost]
    public async Task<Poi> Create([FromBody] Poi poi)
    {
        db.Pois.Add(poi);
        await db.SaveChangesAsync();
        return poi;
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Poi poi)
    {
        if (id != poi.Id) return BadRequest();
        poi.UpdatedAt = DateTime.UtcNow;
        db.Pois.Update(poi);
        await db.SaveChangesAsync();
        return Ok(poi);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var poi = await db.Pois.FindAsync(id);
        if (poi is null) return NotFound();
        poi.IsActive = false;
        poi.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok();
    }
}
