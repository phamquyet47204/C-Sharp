using VinhKhanh.Domain.Entities;
using VinhKhanh.Domain.Interfaces;
using VinhKhanh.Shared.Models;
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
            IsActive = e.Status == PoiStatus.Approved,
            IsPremium = e.IsPremium,
            CategoryCode = NormalizeCategoryCode(
                e.CategoryCode,
                e.Localizations.FirstOrDefault(l => l.LanguageCode == "vi")?.Name,
                e.Localizations.FirstOrDefault(l => l.LanguageCode == "vi")?.Description),
            UpdatedAt = e.UpdatedAt,
            Localizations = e.Localizations.Select(l => new PoiLocalizationDto
            {
                LanguageCode = l.LanguageCode,
                Name = l.Name,
                Description = l.Description
            }).ToList()
        }).ToList();

        var activeIds = await repository.GetAllActiveBaseIdsAsync(cancellationToken);
        
        return new SyncResponse
        {
            UpdatedPois = mapped,
            DeletedIds = new List<int>(),
            ActiveBasePoiIds = activeIds,
            ServerTime = DateTime.UtcNow
        };
    }

    private static string NormalizeCategoryCode(string? categoryCode, string? name, string? description)
    {
        var supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "FOOD_SNAIL",
            "FOOD_BBQ",
            "FOOD_STREET",
            "DRINK",
            "UTILITY"
        };

        if (!string.IsNullOrWhiteSpace(categoryCode) && supported.Contains(categoryCode))
        {
            return categoryCode.ToUpperInvariant();
        }

        return InferCategory(name, description);
    }

    private static string InferCategory(string? name, string? description)
    {
        var source = $"{name} {description}".ToLowerInvariant();

        if (source.Contains("oc") || source.Contains("oyster") || source.Contains("snail") || source.Contains("hai san") || source.Contains("hải sản"))
            return "FOOD_SNAIL";

        if (source.Contains("bbq") || source.Contains("nuong") || source.Contains("nướng") || source.Contains("lau") || source.Contains("lẩu") || source.Contains("hotpot"))
            return "FOOD_BBQ";

        if (source.Contains("coffee") || source.Contains("ca phe") || source.Contains("cà phê") || source.Contains("drink") || source.Contains("beverage") || source.Contains("tra sua") || source.Contains("trà sữa"))
            return "DRINK";

        if (source.Contains("toilet") || source.Contains("wc") || source.Contains("parking") || source.Contains("tien ich") || source.Contains("tiện ích"))
            return "UTILITY";

        return "FOOD_STREET";
    }
}
