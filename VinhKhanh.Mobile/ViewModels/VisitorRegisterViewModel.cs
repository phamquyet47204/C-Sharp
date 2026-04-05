using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VinhKhanh.Mobile.ViewModels;

public partial class VisitorRegisterViewModel(HttpClient http) : ObservableObject
{
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _fullName = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    [RelayCommand]
    private async Task RegisterAsync()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(FullName))
        {
            ErrorMessage = "Vui lòng điền đầy đủ thông tin.";
            return;
        }

        if (Password.Length < 6)
        {
            ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự.";
            return;
        }

        IsBusy = true;
        try
        {
            var response = await http.PostAsJsonAsync("api/auth/register-visitor", new
            {
                email = Email,
                password = Password,
                fullName = FullName
            });

            if (response.IsSuccessStatusCode)
            {
                await Shell.Current.DisplayAlert("Thành công", "Đăng ký thành công! Vui lòng đăng nhập.", "OK");
                await Shell.Current.GoToAsync("..");
            }
            else if ((int)response.StatusCode == 409)
            {
                ErrorMessage = "Email đã được sử dụng.";
            }
            else
            {
                ErrorMessage = "Đăng ký thất bại. Vui lòng thử lại.";
            }
        }
        catch
        {
            ErrorMessage = "Không thể kết nối đến máy chủ.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
