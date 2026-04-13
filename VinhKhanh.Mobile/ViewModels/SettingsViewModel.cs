using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VinhKhanh.Mobile.Services;

namespace VinhKhanh.Mobile.ViewModels;

public partial class SettingsViewModel(AccessControlService accessControl) : ObservableObject
{
    [ObservableProperty] private string _selectedLanguage = Preferences.Get("language", "vi-VN");
    [ObservableProperty] private string _accessStatusText = "Đang kiểm tra...";
    [ObservableProperty] private bool _hasActivePass;

    public List<string> Languages { get; } = ["vi-VN", "en-US", "ja-JP"];

    partial void OnSelectedLanguageChanged(string value)
    {
        Preferences.Set("language", value);
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var status = await accessControl.GetAccessStatusAsync();
        HasActivePass = status.HasActivePass;
        AccessStatusText = status.HasActivePass 
            ? $"Access Pass còn hạn đến: {status.PassExpiryDate:dd/MM/yyyy}" 
            : $"Bạn đã dùng {status.FreeTrialUsed}/{status.FreeTrialLimit} lượt thử miễn phí.";
    }

    [RelayCommand]
    public async Task PurchaseAsync()
    {
        var success = await accessControl.PurchaseAccessPassAsync();
        if (success)
        {
            await LoadAsync();
            var page = Application.Current?.MainPage;
            if (page is not null)
                await page.DisplayAlert("Thành công", "Bạn đã mua Access Pass thành công! (7 ngày)", "OK");
        }
    }
}
