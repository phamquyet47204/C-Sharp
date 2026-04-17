using System;
using Microsoft.Maui.Controls;
using VinhKhanhFoodStreet.Services;

namespace VinhKhanhFoodStreet;

public partial class SettingsPage : ContentPage
{
    private readonly AccessService _accessService;
    private readonly IAppLanguageService _appLanguageService;
    private string _currentLanguage = "vi";
    private bool _isInitialized;

    public SettingsPage(AccessService accessService, IAppLanguageService appLanguageService)
    {
        InitializeComponent();
        _accessService = accessService;
        _appLanguageService = appLanguageService;
        
        _currentLanguage = _appLanguageService.GetEffectiveLanguage();
        LoadSettings();
        UpdateUiCulture();
        UpdateAccessStatus();
        _isInitialized = true;

        LanguagePicker.SelectedIndexChanged += OnLanguageChanged;
    }

    private string T(string key) => _appLanguageService.T(key, _currentLanguage);

    private void LoadSettings()
    {
        LanguagePicker.SelectedIndex = _currentLanguage switch
        {
            "vi" => 0,
            "en" => 1,
            "ja" => 2,
            _ => 0
        };
    }

    private void UpdateUiCulture()
    {
        Title = T("SettingsTitle");
        LanguageSectionLabel.Text = T("LanguageSection");
        AccessPassSectionLabel.Text = T("AccessPassSection");
        UnlockDetailLabel.Text = T("UnlockAllMessage");
    }

    private void UpdateAccessStatus()
    {
        var hasPass = _accessService.HasActivePass();
        var expiry = _accessService.GetExpiryDate();
        var remainingDays = _accessService.GetRemainingDays();

        if (hasPass)
        {
            StatusIconLabel.Text = "🔓";
            StatusTitleLabel.TextColor = Color.FromArgb("#10B981"); // Success green
            
            // Determine if it's a paid pass or a trial
            // Trial is usually 7 days, let's assume if it was a trial it would be marked or we just show remaining
            if (remainingDays > 0 && remainingDays <= 7) 
            {
                StatusTitleLabel.Text = T("StatusTrialActive");
                StatusDetailLabel.Text = $"{remainingDays} {T("DaysRemaining")}";
            }
            else
            {
                StatusTitleLabel.Text = T("StatusActive");
                StatusDetailLabel.Text = $"{T("StatusExpiryDate")}{expiry:dd/MM/yyyy}";
            }
            
            BuyButton.Text = T("RenewPassButton");
        }
        else
        {
            StatusIconLabel.Text = "🔒";
            StatusTitleLabel.Text = T("StatusInactive");
            StatusTitleLabel.TextColor = Color.FromArgb("#EF4444"); // Error red
            StatusDetailLabel.Text = T("TrialExpired");
            BuyButton.Text = T("BuyPassButton");
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (!_isInitialized) return;

        var selected = LanguagePicker.SelectedIndex switch
        {
            0 => "vi",
            1 => "en",
            2 => "ja",
            _ => "vi"
        };

        if (selected != _currentLanguage)
        {
            _currentLanguage = selected;
            _appLanguageService.SetPreferredLanguage(selected);
            UpdateUiCulture();
            UpdateAccessStatus();
        }
    }

    private async void OnBuyClicked(object? sender, EventArgs e)
    {
        var result = await DisplayAlert(T("Confirm"), T("BuyPrompt"), T("Ok"), T("Cancel"));
        if (result)
        {
            _accessService.PurchaseSuccess();
            UpdateAccessStatus();
            await DisplayAlert(T("Success"), T("BuySuccess"), T("Ok"));
        }
    }

    private async void OnRegisterShopClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new RegisterShopPage());
    }
}
