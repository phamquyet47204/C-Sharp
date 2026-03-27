using System.Collections.Generic;
using System.Threading.Tasks;
using VinhKhanhFoodStreet.Models;

namespace VinhKhanhFoodStreet.Services;

/// <summary>
/// Interface định nghĩa các thao tác dữ liệu POI cho SQLite.
/// Việc tách interface giúp dễ test và tuân thủ Dependency Inversion.
/// </summary>
public interface IDatabaseService
{
    Task InitializeAsync();
    Task<int> AddPoiAsync(POI poi);
    Task<int> UpdatePoiAsync(POI poi);
    Task<int> DeletePoiAsync(int poiId);
    Task<List<POI>> GetAllPoisAsync();
    Task<List<POI>> GetPoisByLanguage(string lang);
}
