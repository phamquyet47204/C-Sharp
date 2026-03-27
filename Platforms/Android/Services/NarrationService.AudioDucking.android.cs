#if ANDROID
using System.Diagnostics;
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

            // Yêu cầu audio focus với chế độ giảm âm (transient may duck)
            _audioFocusListener ??= new AudioFocusChangeListener();
            var result = _audioManager.RequestAudioFocus(_audioFocusListener, Android.Media.Stream.Music, AudioFocus.GainTransientMayDuck);
            
            if (result == AudioFocusRequest.Granted)
            {
                Debug.WriteLine("[NarrationService] Da cap audio focus cho narration");
            }
            else
            {
                Debug.WriteLine("[NarrationService] Khong the cap audio focus");
            }
        }
        catch (System.Exception ex)
        {
            Debug.WriteLine($"[NarrationService] Loi BeginAudioDuckingAsync: {ex.Message}");
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
                Debug.WriteLine("[NarrationService] Da nha audio focus");
            }
        }
        catch (System.Exception ex)
        {
            Debug.WriteLine($"[NarrationService] Loi EndAudioDucking: {ex.Message}");
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
