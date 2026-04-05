using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VinhKhanh.Mobile.Services;

namespace VinhKhanh.Mobile.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _authService;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    public LoginViewModel(AuthService authService)
    {
        _authService = authService;
    }

    [RelayCommand]
    public async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Vui lòng nhập Email và Mật khẩu!";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;

        var (success, msg) = await _authService.LoginAsync(Email, Password);

        IsBusy = false;

        if (success)
        {
            // Kiểm tra License
            if (!_authService.CheckLicensingTrialValid())
            {
                var page = global::VinhKhanh.Mobile.App.Current?.MainPage;
                if (page is not null)
                {
                    await page.DisplayAlert("Hết hạn dùng thử", "Phiên bản miễn phí 7 ngày đã kết thúc. Vui lòng thanh toán $1 để tiếp tục sử dụng ứng dụng.", "OK");
                }
                return;
            }
        }
        else
        {
            ErrorMessage = msg;
        }
    }
}
