using System.Net.Http.Json;
using VinhKhanhFoodStreet.Configuration;

namespace VinhKhanhFoodStreet;

public partial class RegisterShopPage : ContentPage
{
    private readonly HttpClient _httpClient;

    public RegisterShopPage()
    {
        InitializeComponent();
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(AppConfig.BaseApiUrl),
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    private async void OnSubmitClicked(object sender, EventArgs e)
    {
        var fullName = FullNameEntry.Text?.Trim();
        var email = EmailEntry.Text?.Trim();
        var password = PasswordEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Thông báo", "Vui lòng nhập đầy đủ thông tin.", "Đồng ý");
            return;
        }

        if (password.Length < 6)
        {
            await DisplayAlert("Thông báo", "Mật khẩu phải có ít nhất 6 ký tự.", "Đồng ý");
            return;
        }

        try
        {
            SetLoading(true);

            var response = await _httpClient.PostAsJsonAsync("api/auth/register-shop", new
            {
                fullName,
                email,
                password
            });

            if (response.IsSuccessStatusCode)
            {
                await DisplayAlert("Thành công", "Đăng ký thành công! Vui lòng chờ Admin phê duyệt tài khoản của bạn.", "Đồng ý");
                await Navigation.PopAsync();
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                await DisplayAlert("Lỗi", string.IsNullOrWhiteSpace(error) ? "Không thể đăng ký lúc này." : error, "Đồng ý");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi kết nối", "Không thể kết nối tới máy chủ: " + ex.Message, "Đồng ý");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SetLoading(bool isLoading)
    {
        SubmitButton.IsEnabled = !isLoading;
        LoadingIndicator.IsRunning = isLoading;
        LoadingIndicator.IsVisible = isLoading;
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
