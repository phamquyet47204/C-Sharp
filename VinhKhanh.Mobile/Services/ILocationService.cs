using System;
using System.Threading.Tasks;
using Microsoft.Maui.Devices.Sensors;

namespace VinhKhanhFoodStreet.Services;

/// <summary>
/// Interface cho module theo doi vi tri.
/// Tach interface de de mock khi unit test va de dang thay the implementation.
/// </summary>
public interface ILocationService
{
    /// <summary>
    /// Su kien duoc ban ra khi vi tri thay doi dat nguong theo bo loc.
    /// </summary>
    event Action<Location>? LocationChanged;

    /// <summary>
    /// Bat dau lang nghe vi tri.
    /// </summary>
    Task StartListeningAsync();

    /// <summary>
    /// Dung lang nghe vi tri.
    /// </summary>
    Task StopListeningAsync();
}
