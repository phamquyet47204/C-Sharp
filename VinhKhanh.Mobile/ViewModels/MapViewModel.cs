using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VinhKhanh.Mobile.Services;
using VinhKhanh.Shared.Models;

namespace VinhKhanh.Mobile.ViewModels;

public partial class MapViewModel(LocalDatabase db, GeofenceService geofence, SyncService sync)
    : ObservableObject
{
    [ObservableProperty] private List<Poi> _pois = [];
    [ObservableProperty] private string _selectedLanguage = "vi-VN";
    [ObservableProperty] private bool _isUnlocked;

    public List<string> Languages { get; } = ["vi-VN", "en-US", "ja-JP", "zh-CN", "ko-KR"];

    [RelayCommand]
    async Task LoadAsync()
    {
        await sync.SyncIfConnectedAsync();
        Pois = await db.GetActivePoisAsync();
        IsUnlocked = Preferences.Get("unlocked", false);
        geofence.Start();
    }

    [RelayCommand]
    void ChangeLanguage(string lang)
    {
        SelectedLanguage = lang;
        Preferences.Set("language", lang);
    }

    [RelayCommand]
    async Task UnlockAsync()
    {
        // Integrate with In-App Purchase (1 USD / 7 days)
        // Placeholder: simulate purchase
        Preferences.Set("unlocked", true);
        Preferences.Set("unlock_expiry", DateTime.UtcNow.AddDays(7).Ticks);
        IsUnlocked = true;
        await Shell.Current.DisplayAlert("Cảm ơn!", "Đã mở khóa 7 ngày.", "OK");
    }
}
