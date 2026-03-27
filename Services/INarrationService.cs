using System.Threading.Tasks;
using CommunityToolkit.Maui.Views;

namespace VinhKhanhFoodStreet.Services;

/// <summary>
/// Service thuyet minh: ho tro TTS va phat file am thanh.
/// </summary>
public interface INarrationService
{
    void RegisterMediaElement(MediaElement? mediaElement);
    Task SpeakAsync(string text, string lang);
    Task PlayAudioAsync(string filePath);
    void StopAll();
}
