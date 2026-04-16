using Microsoft.AspNetCore.Mvc;
using VinhKhanh.Application.UseCases;
using VinhKhanh.Shared.Models;

namespace VinhKhanh.Admin.Controllers;

[ApiController]
[Route("api/pois")]
public class PoisController(PoiSyncUseCase syncUseCase) : ControllerBase
{
    [HttpGet("updates")]
    public async Task<ActionResult<SyncResponse>> GetUpdates([FromQuery] DateTime lastSync, [FromQuery] bool includeAudio = true, CancellationToken cancellationToken = default)
    {
        try
        {
            var req = new SyncRequest
            {
                LastSyncAt = DateTime.SpecifyKind(lastSync, DateTimeKind.Utc),
                IncludeAudio = includeAudio
            };

            var result = await syncUseCase.ExecuteAsync(req, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return Problem($"Lỗi khi lấy dữ liệu đồng bộ: {ex.Message}");
        }
    }

    [HttpGet("sync")]
    public Task<ActionResult<SyncResponse>> Sync([FromQuery] DateTime lastSync, [FromQuery] bool includeAudio = true, CancellationToken cancellationToken = default)
        => GetUpdates(lastSync, includeAudio, cancellationToken);
}
