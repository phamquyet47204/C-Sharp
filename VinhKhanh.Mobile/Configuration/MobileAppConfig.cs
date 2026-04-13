using Microsoft.Maui.Devices;

namespace VinhKhanh.Mobile.Configuration;

public static class MobileAppConfig
{
    public static string BaseApiUrl =>
#if DEBUG
        DeviceInfo.Current.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:5000/" 
            : "http://localhost:5000/";
#else
        "https://enormitpham.me/";
#endif
}
