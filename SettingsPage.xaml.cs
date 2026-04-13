using System;
using Microsoft.Maui.Controls;
using VinhKhanhFoodStreet.Services;

namespace VinhKhanhFoodStreet;

public partial class SettingsPage : ContentPage
{
    private readonly AccessService _accessService;
    private readonly IAppLanguageService _appLanguageService;
    private bool _isInitialized;

    public SettingsPage(AccessService accessService, IAppLanguageService appLanguageService)
    {
        InitializeComponent();
        _accessService = accessService;
        _appLanguageService = appLanguageService;
        
        LoadSettings();
        UpdateAccessStatus();
        _isInitialized = true;

        LanguagePicker.SelectedIndexChanged += OnLanguageChanged;
    }

    private void LoadSettings()
    {
        var currentLang = _appLanguageService.GetEffectiveLanguage();
        LanguagePicker.SelectedIndex = currentLang switch
        {
            "vi" => 0,
            "en" => 1,
            "ja" => 2,
            _ => 0
        };
    }

    private void UpdateAccessStatus()
    {
        var hasPass = _accessService.HasActivePass();
        var expiry = _accessService.GetExpiryDate();

        if (hasPass)
        {
            StatusIconLabel.Text = "🔓";
            StatusTitleLabel.Text = "Đang kích hoạt";
            StatusTitleLabel.TextColor = Color.FromArgb("#10B981"); // Success green
            StatusDetailLabel.Text = $"Gói Access Pass còn hiệu lực đến: {expiry:dd/MM/yyyy}";
            BuyButton.Text = "Gia hạn thêm 7 ngày ($1)";
        }
        else
        {
            StatusIconLabel.Text = "🔒";
            StatusTitleLabel.Text = "Chưa kích hoạt";
            StatusTitleLabel.TextColor = Color.FromArgb("#EF4444"); // Error red
            StatusDetailLabel.Text = "Gói dùng thử 7 ngày đã hết hạn hoặc chưa dùng.";
            BuyButton.Text = "Mua Access Pass ($1/7 ngày)";
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

        _appLanguageService.SetPreferredLanguage(selected);
        // We'll notify the user that language will be applied on comeback or restart
        // Ideally, use a MessagingCenter or similar to notify MainPage
    }

    private async void OnBuyClicked(object? sender, EventArgs e)
    {
        var result = await DisplayAlert("Xác nhận", "Bạn có muốn mua gói Access Pass 7 ngày với giá $1 không? (Giả lập thanh toán)", "Đồng ý", "Hủy");
        if (result)
        {
            _accessService.PurchaseSuccess();
            UpdateAccessStatus();
            await DisplayAlert("Thành công", "Cảm ơn bạn! Gói Access Pass của bạn đã được kích hoạt.", "OK");
        }
    }
}
