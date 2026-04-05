using VinhKhanh.Domain.Entities;
using VinhKhanh.Domain.Interfaces;
using VinhKhanh.Shared.Models; // Shared DTOs
using Poi = VinhKhanh.Shared.Models.Poi;

namespace VinhKhanh.Application.UseCases;

public class PoiSyncUseCase(IPoiRepository repository)
{
    public async Task<SyncResponse> ExecuteAsync(SyncRequest request, CancellationToken cancellationToken = default)
    {
        var entities = await repository.GetSyncPoisAsync(request.LastSyncAt, cancellationToken);
        
        var mapped = entities.Select(e => new Poi
        {
            Id = e.Id,
            BasePoiId = e.BasePoiId,
            Latitude = e.Latitude,
            Longitude = e.Longitude,
            Radius = e.Radius,
            ImageUrl = e.ImageUrl,
            Priority = e.Priority,
            IsActive = e.Status == VinhKhanh.Domain.Entities.PoiStatus.Approved,
            IsPremium = e.IsPremium,
            UpdatedAt = e.UpdatedAt,
            // Giữ nguyên cấu trúc Đa ngôn ngữ để MAUI tải về SQLite và tự chọn
            Localizations = e.Localizations.Select(l => new PoiLocalizationDto
            {
                LanguageCode = l.LanguageCode,
                Name = l.Name,
                Description = l.Description,
                AudioFile = request.IncludeAudio ? l.AudioUrl : null
            }).ToList()
        }).ToList();

        return new SyncResponse
        {
            UpdatedPois = mapped,
            DeletedIds = [], 
            ServerTime = DateTime.UtcNow
        };
    }
}
