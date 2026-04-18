using Microsoft.Maui.Devices;

namespace VinhKhanhFoodStreet.Configuration;

public static class AppConfig
{
    // IP LAN cua may dev de dien thoai that trong cung Wi-Fi co the truy cap backend.
    // Co the doi nhanh ma khong can sua code bang bien moi truong VINHKHANH_API_BASE_URL.
    private const string DevLanIp = "172.20.10.4";

    // Mot diem cau hinh duy nhat cho backend API.
    public static string BaseApiUrl
    {
        get
        {
#if DEBUG
            var overrideUrl = Environment.GetEnvironmentVariable("VINHKHANH_API_BASE_URL");
            if (!string.IsNullOrWhiteSpace(overrideUrl))
            {
                return EnsureTrailingSlash(overrideUrl);
            }

            // Android emulator map localhost cua host qua 10.0.2.2.
            if (DeviceInfo.Current.Platform == DevicePlatform.Android && DeviceInfo.Current.DeviceType == DeviceType.Virtual)
            {
                return "http://10.0.2.2:5000/";
            }

            // Thiet bi that (Android/iOS) se goi backend qua IP LAN cua may dev.
            if (DeviceInfo.Current.DeviceType == DeviceType.Physical)
            {
                return $"http://{DevLanIp}:5000/";
            }

            // Cac truong hop con lai (Windows/Mac local debug) giu localhost.
            return "http://localhost:5000/";
#else
            return "https://enormitpham.me/";
#endif
        }
    }

    private static string EnsureTrailingSlash(string baseUrl)
    {
        return baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/";
    }
}
