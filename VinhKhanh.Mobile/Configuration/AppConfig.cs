using Microsoft.Maui.Devices;

namespace VinhKhanhFoodStreet.Configuration;

public static class AppConfig
{
    // Mot diem cau hinh duy nhat cho backend API.
    public static string BaseApiUrl =>
#if DEBUG
        DeviceInfo.Current.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:5000/"
            : "http://localhost:5000/";
#else
        "https://enormitpham.me/";
#endif
}
