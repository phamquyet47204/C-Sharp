using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Maps;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps.Handlers;
using VinhKhanhFoodStreet.Extensions;
using VinhKhanhFoodStreet.Services;

namespace VinhKhanhFoodStreet;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		
		// Tinh duong dan SQLite database theo platform.
		var databasePath = Path.Combine(
			FileSystem.AppDataDirectory,
			"vinhkhanh_foodstreet.db3");

		builder
			.UseMauiApp<App>()

#if WINDOWS
			.UseMauiCommunityToolkitMaps(string.Empty)
#else
			.UseMauiMaps()
#endif
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			})
			// Dang ky cac service dung dependency injection.
			.Services
			.AddDatabaseServices(databasePath)
			.AddLocationServices()
			.AddGeofenceEngine()
			.AddSingleton<IAppLanguageService, AppLanguageService>()
			.AddSingleton<IAudioQueueManager, AudioQueueManager>()
			.AddSingleton<INarrationService, NarrationService>()
			.AddSingleton<MainPage>();

#if ANDROID
		if (OperatingSystem.IsAndroidVersionAtLeast(26))
		{
			builder.UseMauiCommunityToolkitMediaElement(true);
		}
#else
		builder.UseMauiCommunityToolkitMediaElement(true);
#endif

#if ANDROID
		MapHandler.Mapper.AppendToMapping("MoveMyLocationButton", (handler, _) =>
		{
			Platforms.Android.MapUiCustomizer.Configure(handler);
		});
#endif

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
