using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using VinhKhanhFoodStreet.Models;

namespace VinhKhanhFoodStreet.ViewModels;

/// <summary>
/// ViewModel chua danh sach POI duy nhat cho MainPage.
/// Muc tieu la cap nhat text theo ngon ngu tai cho de UI muot, khong clear/reload toan bo list.
/// </summary>
public partial class MainPageViewModel : ObservableObject
{
    public ObservableCollection<POI> DisplayPois { get; } = new();

    /// <summary>
    /// Nap moi danh sach hien thi trong cac truong hop khoi tao, filter, search.
    /// </summary>
    public void ReplaceDisplayPois(IEnumerable<POI> pois)
    {
        DisplayPois.Clear();
        foreach (var poi in pois)
        {
            DisplayPois.Add(poi);
        }
    }

    /// <summary>
    /// Chi cap nhat noi dung text/audio theo ngon ngu moi tren danh sach dang hien thi.
    /// </summary>
    public void UpdateLocalizedTextsInPlace(IReadOnlyList<POI> localizedPois)
    {
        var localizedByAggregateId = localizedPois
            .GroupBy(p => p.AggregateId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var existing in DisplayPois)
        {
            if (!localizedByAggregateId.TryGetValue(existing.AggregateId, out var localized))
            {
                continue;
            }

            existing.Name = localized.Name;
            existing.Description = localized.Description;
            existing.AudioPath = localized.AudioPath;
            existing.LanguageCode = localized.LanguageCode;
            existing.ImagePath = localized.ImagePath;
        }
    }
}
