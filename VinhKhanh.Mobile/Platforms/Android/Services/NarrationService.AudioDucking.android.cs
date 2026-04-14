#if ANDROID
using System.Threading.Tasks;
using Android.Media;
using Microsoft.Maui.ApplicationModel;

namespace VinhKhanhFoodStreet.Services;

public partial class NarrationService
{
    private AudioManager? _audioManager;
    private AudioFocusChangeListener? _audioFocusListener;

    private partial Task BeginAudioDuckingAsync()
    {
        try
        {
            _audioManager ??= (AudioManager?)Platform.AppContext.GetSystemService(Android.Content.Context.AudioService);
            if (_audioManager is null)
            {
                return Task.CompletedTask;
            }

            // Dung focus transient de uu tien narration, han che meo tieng do ducking.
            _audioFocusListener ??= new AudioFocusChangeListener();
            var result = _audioManager.RequestAudioFocus(_audioFocusListener, Android.Media.Stream.Music, AudioFocus.GainTransient);
            
            if (result == AudioFocusRequest.Granted)
            {
                System.Diagnostics.Debug.WriteLine("[NarrationService] Da cap audio focus cho narration");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[NarrationService] Khong the cap audio focus");
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NarrationService] Loi BeginAudioDuckingAsync: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private partial void EndAudioDucking()
    {
        try
        {
            if (_audioManager is not null && _audioFocusListener is not null)
            {
                _audioManager.AbandonAudioFocus(_audioFocusListener);
                System.Diagnostics.Debug.WriteLine("[NarrationService] Da nha audio focus");
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NarrationService] Loi EndAudioDucking: {ex.Message}");
        }
    }

    private sealed class AudioFocusChangeListener : Java.Lang.Object, AudioManager.IOnAudioFocusChangeListener
    {
        public void OnAudioFocusChange(AudioFocus focusChange)
        {
            // Khong can xu ly them, chi can listener de dap ung API.
        }
    }
}
#endif
