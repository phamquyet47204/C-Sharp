using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VinhKhanh.Mobile.Models;
using VinhKhanh.Mobile.Services;

namespace VinhKhanh.Mobile.ViewModels;

public partial class PoiListViewModel(LocalDatabase db) : ObservableObject
{
    [ObservableProperty] private List<PoiRecord> _pois = [];
    [ObservableProperty] private List<PoiRecord> _filteredPois = [];
    [ObservableProperty] private string _searchText = string.Empty;

    [RelayCommand]
    public async Task LoadAsync()
    {
        Pois = await db.GetActivePoisAsync();
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredPois = Pois;
        }
        else
        {
            var lower = SearchText.ToLowerInvariant();
            FilteredPois = Pois
                .Where(p => p.Name.ToLowerInvariant().Contains(lower) || 
                            p.Description.ToLowerInvariant().Contains(lower) || 
                            p.CategoryDisplayName.ToLowerInvariant().Contains(lower))
                .ToList();
        }
    }
}
