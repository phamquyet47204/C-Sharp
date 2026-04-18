using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VinhKhanh.Domain.Entities;
using VinhKhanh.Infrastructure.Data;

namespace VinhKhanh.Admin.Controllers;

[ApiController]
[Route("api/admin/settings")]
[Authorize(Roles = "Admin")]
public class SettingsController(AppDbContext dbContext) : ControllerBase
{
    /// <summary>
    /// GET /api/admin/settings
    /// Lấy toàn bộ các biến cấu hình hệ thống dưới dạng Key-Value.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var settings = await dbContext.SystemSettings.ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        return Ok(settings);
    }

    /// <summary>
    /// PUT /api/admin/settings
    /// Cập nhật hoặc lưu mới cấu hình hệ thống.
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> UpdateSettings([FromBody] Dictionary<string, string> settings, CancellationToken ct)
    {
        foreach (var kvp in settings)
        {
            var setting = await dbContext.SystemSettings.FirstOrDefaultAsync(s => s.Key == kvp.Key, ct);
            if (setting is null)
            {
                dbContext.SystemSettings.Add(new SystemSetting { Key = kvp.Key, Value = kvp.Value });
            }
            else
            {
                setting.Value = kvp.Value;
            }
        }
        await dbContext.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }
}
