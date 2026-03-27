using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Media;

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
    private readonly SemaphoreSlim _playbackLock = new(1, 1);
    private CancellationTokenSource? _currentNarrationCts;
    private WeakReference<MediaElement>? _registeredMediaElement;

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
            var locale = await ResolveLocaleAsync(lang);
            Debug.WriteLine($"[NarrationService] Bat dau TTS ({locale?.Language}:{locale?.Country})");

            await TextToSpeech.Default.SpeakAsync(text, new SpeechOptions
            {
                Locale = locale
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
            _currentNarrationCts?.Cancel();

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
        // Preempt narration cu de uu tien narration moi khi vao vung moi.
        _currentNarrationCts?.Cancel();

        var localCts = new CancellationTokenSource();
        _currentNarrationCts = localCts;

        await _playbackLock.WaitAsync();
        try
        {
            localCts.Token.ThrowIfCancellationRequested();

            await BeginAudioDuckingAsync();
            await work(localCts.Token);
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("[NarrationService] Narration bi huy do co yeu cau moi");
        }
        finally
        {
            EndAudioDucking();

            if (ReferenceEquals(_currentNarrationCts, localCts))
            {
                _currentNarrationCts = null;
            }

            localCts.Dispose();
            _playbackLock.Release();
        }
    }

    private static string NormalizeAudioPath(string filePath)
    {
        // MauiAsset trong Resources/Raw duoc truy cap bang logical name,
        // nen uu tien ten file de de phat tren nhieu nen tang.
        var path = filePath.Replace("\\", "/").Trim();
        if (path.StartsWith("Resources/Raw/", StringComparison.OrdinalIgnoreCase))
        {
            path = path["Resources/Raw/".Length..];
        }

        return path.Contains('/') ? path[(path.LastIndexOf('/') + 1)..] : path;
    }

    private static async Task<Locale?> ResolveLocaleAsync(string lang)
    {
        if (string.IsNullOrWhiteSpace(lang))
        {
            return null;
        }

        try
        {
            var locales = await TextToSpeech.Default.GetLocalesAsync();
            var normalized = lang.Trim();

            // Ho tro nhap day du locale nhu ja-JP, vi-VN...
            var exact = locales.FirstOrDefault(l =>
                string.Equals($"{l.Language}-{l.Country}", normalized, StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
            {
                return exact;
            }

            // Fallback theo ma ngon ngu 2 ky tu: vi, en, ja...
            var shortCode = normalized.Split('-')[0];
            return locales.FirstOrDefault(l =>
                string.Equals(l.Language, shortCode, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NarrationService] Loi ResolveLocaleAsync: {ex.Message}");
            return null;
        }
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

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(30), ct));
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
