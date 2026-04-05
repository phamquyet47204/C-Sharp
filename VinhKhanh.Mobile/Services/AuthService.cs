using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Maui.Storage;

namespace VinhKhanh.Mobile.Services;

public class AuthService
{
    private readonly HttpClient _httpClient;
    
    public AuthService()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(VinhKhanhFoodStreet.Configuration.AppConfig.BaseApiUrl)
        };
    }

    public async Task<(bool Success, string Message)> LoginAsync(string email, string password)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", new { email, password });
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var json = JsonDocument.Parse(content);
                var token = json.RootElement.GetProperty("token").GetString();
                
                if (!string.IsNullOrEmpty(token))
                {
                    await SecureStorage.SetAsync("jwt_token", token);
                    return (true, "Đăng nhập thành công!");
                }
            }
            return (false, "Sai tài khoản, mật khẩu hoặc đang chờ duyệt.");
        }
        catch (Exception ex)
        {
            // Logging
            return (false, $"Lỗi kết nối: {ex.Message}");
        }
    }

    public async Task<string?> GetTokenAsync()
    {
        return await SecureStorage.GetAsync("jwt_token");
    }

    public void Logout()
    {
        SecureStorage.Remove("jwt_token");
    }

    /// <summary>
    /// Kiểm tra bản quyền sử dụng ứng dụng. Trả về True nếu hợp lệ, False nếu hết hạn.
    /// Giới hạn dùng thử 7 ngày (Trial).
    /// </summary>
    public bool CheckLicensingTrialValid()
    {
        var activationDateStr = Preferences.Get("ActivationDate", string.Empty);
        DateTime activationDate;

        if (string.IsNullOrEmpty(activationDateStr))
        {
            // Lần đầu mở App -> Ghi nhận ngày kích hoạt
            activationDate = DateTime.UtcNow;
            Preferences.Set("ActivationDate", activationDate.ToString("O"));
            return true;
        }

        if (DateTime.TryParse(activationDateStr, out activationDate))
        {
            var daysUsed = (DateTime.UtcNow - activationDate).TotalDays;
            
            // Nếu > 7 ngày thì hết hạn Trial
            if (daysUsed > 7)
            {
                return false;
            }
        }
        
        return true;
    }
}
