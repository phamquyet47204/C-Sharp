using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Media;
using Microsoft.Maui.Networking;

namespace VinhKhanhFoodStreet.Services;

/// <summary>
/// Narration engine:
/// - Phat MP3 qua CommunityToolkit MediaElement.
/// - Doc TTS qua Microsoft.Maui.Media.TextToSpeech.
/// - Dam bao chi co 1 narration dang phat tai mot thoi diem.
/// - Co preemption: narration moi se dung narration cu de tranh de giọng.
/// </summary>
public partial class NarrationService : INarrationService
{
    private readonly IAppLanguageService _appLanguageService;
    private readonly IAudioQueueManager _audioQueueManager;
    private WeakReference<MediaElement>? _registeredMediaElement;

    public NarrationService(IAppLanguageService appLanguageService, IAudioQueueManager audioQueueManager)
    {
        _appLanguageService = appLanguageService;
        _audioQueueManager = audioQueueManager;
    }

    public void RegisterMediaElement(MediaElement? mediaElement)
    {
        _registeredMediaElement = mediaElement is null
            ? null
            : new WeakReference<MediaElement>(mediaElement);
    }

    public async Task SpeakAsync(string text, string lang)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        await RunExclusiveNarrationAsync(async ct =>
        {
            await StopMediaElementIfNeededAsync();

            // Uu tien ngon ngu user chon trong app; neu chua co moi dung ngon ngu truyen vao/he thong.
            var effectiveLang = _appLanguageService.GetEffectiveLanguage(lang);
            var locale = await ResolveBestLocaleAsync(effectiveLang);
            var sanitizedText = SanitizeTtsText(text);

            if (string.IsNullOrWhiteSpace(sanitizedText))
            {
                return;
            }

            // Offline-first: TTS van uu tien engine noi bo cua may.
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                Debug.WriteLine("[NarrationService] Dang o che do offline, su dung TTS noi bo cua thiet bi");
            }

            Debug.WriteLine($"[NarrationService] Bat dau TTS ({locale?.Language}:{locale?.Country})");

            await TextToSpeech.Default.SpeakAsync(sanitizedText, new SpeechOptions
            {
                Locale = locale,
                Pitch = 1.0f,
                Rate = 0.92f,
                Volume = 1.0f
            }, ct);

            Debug.WriteLine("[NarrationService] TTS hoan tat");
        });
    }

    public async Task PlayAudioAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        await RunExclusiveNarrationAsync(async ct =>
        {
            var mediaElement = await ResolveNarrationMediaElementAsync();
            if (mediaElement is null)
            {
                throw new InvalidOperationException("Khong tim thay MediaElement NarrationPlayer tren UI.");
            }

            var normalizedPath = NormalizeAudioPath(filePath);
            await EnsureAudioAssetExistsAsync(normalizedPath, ct);
            Debug.WriteLine($"[NarrationService] Bat dau phat MP3: {normalizedPath}");

            Task playbackTask = Task.CompletedTask;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                playbackTask = PlayWithMediaElementAsync(mediaElement, normalizedPath, ct);
            });

            await playbackTask;
            Debug.WriteLine("[NarrationService] MP3 hoan tat");
        });
    }

    public void StopAll()
    {
        try
        {
            _audioQueueManager.CancelCurrent();

            _ = MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var mediaElement = await ResolveNarrationMediaElementAsync();
                mediaElement?.Stop();
            });

            EndAudioDucking();
            Debug.WriteLine("[NarrationService] Da dung toan bo narration");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NarrationService] Loi StopAll: {ex.Message}");
        }
    }

    private async Task RunExclusiveNarrationAsync(Func<CancellationToken, Task> work)
    {
        await _audioQueueManager.RunExclusiveAsync(async ct =>
        {
            try
            {
                await BeginAudioDuckingAsync();
                await Task.Delay(120, ct);
                await work(ct);
            }
            finally
            {
                EndAudioDucking();
            }
        });
    }

    private async Task StopMediaElementIfNeededAsync()
    {
        try
        {
            var mediaElement = await ResolveNarrationMediaElementAsync();
            if (mediaElement is null)
            {
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() => mediaElement.Stop());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NarrationService] Loi StopMediaElementIfNeededAsync: {ex.Message}");
        }
    }

    private static string SanitizeTtsText(string input)
    {
        // Loai cac ky tu icon/symbol de giam kha nang engine TTS phat am bi re, vo am.
        var text = input
            .Replace("🔊", " ")
            .Replace("🧭", " ")
            .Replace("🔍", " ")
            .Replace("✕", " ")
            .Replace("★", " ")
            .Replace("↔", " ");

        text = Regex.Replace(text, "\\s+", " ").Trim();
        return text;
    }

    private static string NormalizeAudioPath(string filePath)
    {
        // Giu nguyen duong dan logic trong Resources/Raw (vi du: audio/vi/file.mp3).
        // Neu cat ve ten file thuong se mat folder va gay khong tim thay asset.
        var path = filePath.Replace("\\", "/").Trim();
        if (path.StartsWith("Resources/Raw/", StringComparison.OrdinalIgnoreCase))
        {
            path = path["Resources/Raw/".Length..];
        }

        return path.TrimStart('/');
    }

    private static async Task EnsureAudioAssetExistsAsync(string assetPath, CancellationToken ct)
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(assetPath);
            if (stream is null)
            {
                throw new FileNotFoundException($"Khong tim thay file am thanh trong app package: {assetPath}");
            }

            ct.ThrowIfCancellationRequested();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new FileNotFoundException($"Khong tim thay file am thanh: {assetPath}", ex);
        }
    }

    private async Task<Locale?> ResolveBestLocaleAsync(string languageCode)
    {
        try
        {
            var locales = await TextToSpeech.Default.GetLocalesAsync();

            foreach (var candidate in _appLanguageService.GetLanguageFallbackChain(languageCode))
            {
                var locale = ResolveLocaleCandidate(locales, candidate);
                if (locale is not null)
                {
                    return locale;
                }
            }

            return locales.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NarrationService] Loi ResolveBestLocaleAsync: {ex.Message}");
            return null;
        }
    }

    private static Locale? ResolveLocaleCandidate(System.Collections.Generic.IEnumerable<Locale> locales, string candidate)
    {
        var normalized = candidate.Trim();

        // Ho tro ma day du locale nhu vi-VN, ja-JP...
        var exact = locales.FirstOrDefault(l =>
            string.Equals($"{l.Language}-{l.Country}", normalized, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        // Ho tro ma 2 ky tu va alias jp -> ja.
        var shortCode = normalized.Split('-')[0].ToLowerInvariant();
        if (shortCode == "jp")
        {
            shortCode = "ja";
        }

        return locales.FirstOrDefault(l =>
            string.Equals(l.Language, shortCode, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<MediaElement?> ResolveNarrationMediaElementAsync()
    {
        if (_registeredMediaElement is not null &&
            _registeredMediaElement.TryGetTarget(out var registered) &&
            registered.Handler is not null)
        {
            return registered;
        }

        return await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page is null)
            {
                return null;
            }

            return page.FindByName<MediaElement>("NarrationPlayer");
        });
    }

    private static async Task PlayWithMediaElementAsync(MediaElement mediaElement, string assetPath, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        void CompletedHandler(object? sender, EventArgs args)
        {
            mediaElement.MediaEnded -= CompletedHandler;
            tcs.TrySetResult(null);
        }

        mediaElement.MediaEnded += CompletedHandler;
        mediaElement.Source = MediaSource.FromFile(assetPath);
        mediaElement.Play();

        using var registration = ct.Register(() =>
        {
            mediaElement.Stop();
            mediaElement.MediaEnded -= CompletedHandler;
            tcs.TrySetCanceled(ct);
        });

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(12), ct));
        if (completed != tcs.Task)
        {
            mediaElement.Stop();
            mediaElement.MediaEnded -= CompletedHandler;
            throw new TimeoutException($"Khong the phat xong file am thanh: {assetPath}");
        }

        await tcs.Task;
    }

    // Audio ducking: Android co implementation native; cac nen tang khac la no-op.
    private partial Task BeginAudioDuckingAsync();
    private partial void EndAudioDucking();
}
