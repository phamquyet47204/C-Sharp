using Microsoft.Extensions.DependencyInjection;
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
            .AddSingleton<SyncService>()
            .AddHttpClient<SyncService>()
            .Services
            .AddTransient<MapViewModel>()
            .AddTransient<MapPage>();

        return builder.Build();
    }
}
