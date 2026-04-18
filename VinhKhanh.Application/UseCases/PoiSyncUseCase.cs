using VinhKhanh.Domain.Entities;
using VinhKhanh.Domain.Interfaces;
using VinhKhanh.Shared.Models; // Các DTO dùng chung cho nhiều project
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
<<<<<<< Updated upstream
                LanguageCode = l.LanguageCode,
                Name = l.Name,
                Description = l.Description,
                AudioFile = request.IncludeAudio ? l.AudioUrl : null
            }).ToList()
=======
                Id = e.Id,
                BasePoiId = e.BasePoiId,
                Latitude = e.Latitude,
                Longitude = e.Longitude,
                Radius = e.Radius,
                ImageUrl = e.ImageUrl,
                Priority = e.Priority,
                IsActive = e.Status == VinhKhanh.Domain.Entities.PoiStatus.Approved,
                IsPremium = e.IsPremium,
                CategoryCode = NormalizeCategoryCode(e.CategoryCode, vi?.Name, vi?.Description),
                UpdatedAt = e.UpdatedAt,
                Localizations = e.Localizations.Select(l => new PoiLocalizationDto
                {
                    LanguageCode = l.LanguageCode,
                    Name = l.Name,
                    Description = l.Description
                }).ToList()
            };
>>>>>>> Stashed changes
        }).ToList();

        return new SyncResponse
        {
            UpdatedPois = mapped,
            DeletedIds = [], 
            ServerTime = DateTime.UtcNow
        };
    }
<<<<<<< Updated upstream
=======

    private static string NormalizeCategoryCode(string? categoryCode, string? name, string? description)
    {
        var supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "FOOD_SNAIL", "FOOD_BBQ", "FOOD_STREET", "DRINK", "UTILITY" };
        if (!string.IsNullOrWhiteSpace(categoryCode) && supported.Contains(categoryCode))
            return categoryCode.ToUpperInvariant();

        return InferCategory(name, description);
    }

    private static string InferCategory(string? name, string? description)
    {
        var source = $"{name} {description}".ToLowerInvariant();
        if (source.Contains("oc") || source.Contains("oyster") || source.Contains("snail") || source.Contains("hai san"))
            return "FOOD_SNAIL";
        if (source.Contains("bbq") || source.Contains("nuong") || source.Contains("lau") || source.Contains("hotpot"))
            return "FOOD_BBQ";
        if (source.Contains("coffee") || source.Contains("ca phe") || source.Contains("drink") || source.Contains("beverage") || source.Contains("tra sua"))
            return "DRINK";
        if (source.Contains("toilet") || source.Contains("wc") || source.Contains("parking") || source.Contains("tiên ích"))
            return "UTILITY";

        return "FOOD_STREET"; // Mặc định là Ẩm thực đường phố thay vì ALL (Tất cả)
    }
>>>>>>> Stashed changes
}
