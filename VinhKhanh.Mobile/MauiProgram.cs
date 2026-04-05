using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Maps.Handlers;
using VinhKhanh.Mobile.Services;
using VinhKhanh.Mobile.ViewModels;
using VinhKhanh.Mobile.Views;

namespace VinhKhanh.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiMaps()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "vinhkhanh.db");

        builder.Services
            .AddSingleton(_ => new LocalDatabase(dbPath))
            .AddSingleton<NarrationEngine>()
            .AddSingleton<GeofenceService>()
            .AddSingleton(sp =>
            {
                return new HttpClient
                {
                    BaseAddress = new Uri(VinhKhanhFoodStreet.Configuration.AppConfig.BaseApiUrl)
                };
            })
            .AddSingleton<SyncService>()
            .AddSingleton<AccessControlService>()
            .AddSingleton<AuthService>()
            .AddTransient<MapViewModel>()
            .AddTransient<MapPage>();

#if ANDROID
        MapHandler.Mapper.AppendToMapping("MoveMyLocationButton", (handler, _) =>
        {
            Platforms.Android.MapUiCustomizer.Configure(handler);
        });
#endif

        return builder.Build();
    }
}
