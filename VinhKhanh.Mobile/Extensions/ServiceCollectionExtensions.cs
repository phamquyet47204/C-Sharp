using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Devices.Sensors;
using VinhKhanhFoodStreet.Services;

namespace VinhKhanhFoodStreet.Extensions;

/// <summary>
/// Extension tập trung đăng ký dịch vụ vào DI container.
/// Mục tiêu: giữ MauiProgram gọn, dễ mở rộng và bảo trì.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Đăng ký DatabaseService theo Singleton để toàn app dùng chung 1 instance.
    /// </summary>
    /// <param name="services">Service collection của MAUI.</param>
    /// <param name="databasePath">Đường dẫn file SQLite.</param>
    public static IServiceCollection AddDatabaseServices(
        this IServiceCollection services,
        string databasePath)
    {
        services.AddSingleton<IDatabaseService>(_ => new DatabaseService(databasePath));
        return services;
    }

    /// <summary>
    /// Dang ky module Location Engine theo singleton.
    /// IGeolocation lay tu MAUI de de mock/test khi can.
    /// </summary>
    public static IServiceCollection AddLocationServices(this IServiceCollection services)
    {
        services.AddSingleton<IGeolocation>(_ => Geolocation.Default);
        services.AddSingleton<ILocationService, LocationService>();
        return services;
    }

    /// <summary>
    /// Dang ky geofence engine de xu ly vao/ra POI theo vi tri.
    /// </summary>
    public static IServiceCollection AddGeofenceEngine(this IServiceCollection services)
    {
        services.AddSingleton<IGeofenceEngine, GeofenceEngine>();
        return services;
    }
}
