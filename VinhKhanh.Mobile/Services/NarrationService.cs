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
/// NarrationService: Dịch vụ điều phối thuyết minh âm thanh.
/// 
/// Chức năng:
/// - Phát tệp âm thanh MP3 thông qua MediaElement (thư viện CommunityToolkit.Maui).
/// - Phát âm thanh dạng chuỗi văn bản thông qua Text-To-Speech (TTS) của hệ điều hành.
/// - Đảm bảo nguyên tắc "Độc quyền Narration": Tại một thời điểm chỉ có duy nhất một luồng thuyết minh được phát.
/// - Cơ chế Preemption (Chiếm quyền): Khi có yêu cầu thuyết minh mới, yêu cầu cũ sẽ bị dừng ngay lập tức.
/// - Audio Ducking: Giảm âm lượng các nguồn âm thanh khác trong ứng dụng khi thuyết minh đang phát.
/// </summary>
public partial class NarrationService : INarrationService
{
    private readonly IAppLanguageService _appLanguageService;
    private readonly IAudioQueueManager _audioQueueManager;
    
    // Sử dụng WeakReference để tránh rò rỉ bộ nhớ (memory leak) khi giữ tham chiếu tới Control UI (MediaElement).
    private WeakReference<MediaElement>? _registeredMediaElement;

    public NarrationService(IAppLanguageService appLanguageService, IAudioQueueManager audioQueueManager)
    {
        _appLanguageService = appLanguageService;
        _audioQueueManager = audioQueueManager;
    }

    /// <summary>
    /// Đăng ký MediaElement từ tầng giao diện (UI) để service có thể điều khiển việc phát MP3.
    /// </summary>
    public void RegisterMediaElement(MediaElement? mediaElement)
    {
        _registeredMediaElement = mediaElement is null
            ? null
            : new WeakReference<MediaElement>(mediaElement);
    }

    /// <summary>
    /// Thuyết minh theo dạng đọc văn bản (TTS).
    /// </summary>
    public async Task SpeakAsync(string text, string lang)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        // Đảm bảo việc phát TTS là độc quyền thông qua QueueManager
        await RunExclusiveNarrationAsync(async ct =>
        {
            // Nếu MediaElement đang phát nhạc thì phải dừng trước khi nói
            await StopMediaElementIfNeededAsync();

            // Xác định ngôn ngữ hiệu dụng dựa trên cấu hình người dùng (Fallback chain)
            var effectiveLang = _appLanguageService.GetEffectiveLanguage(lang);
            var locale = await ResolveBestLocaleAsync(effectiveLang);
            
            // Xử lý làm sạch văn bản (loại bỏ icon, ký tự lạ) để tránh engine TTS phát âm lỗi
            var sanitizedText = SanitizeTtsText(text);

            if (string.IsNullOrWhiteSpace(sanitizedText))
            {
                return;
            }

            // Chế độ Offline-first: TTS vẫn hoạt động được nhờ bộ máy nội bộ của thiết bị
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                Debug.WriteLine("[NarrationService] Dang o che do offline, su dung TTS noi bo cua thiet bi");
            }

            Debug.WriteLine($"[NarrationService] Bat dau TTS ({locale?.Language}:{locale?.Country})");

            // Gọi API native của hệ điều hành thông qua MAUI Essentials
            await TextToSpeech.Default.SpeakAsync(sanitizedText, new SpeechOptions
            {
                Locale = locale,
                Pitch = 1.0f,
                Rate = 0.92f, // Giảm tốc độ đọc một chút để nghe tự nhiên và rõ ràng hơn
                Volume = 1.0f
            }, ct);

            Debug.WriteLine("[NarrationService] TTS hoan tat");
        });
    }

    /// <summary>
    /// Phát tệp âm thanh MP3 từ Resources nội bộ của ứng dụng.
    /// </summary>
    public async Task PlayAudioAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        await RunExclusiveNarrationAsync(async ct =>
        {
            // Kiểm tra xem UI đã sẵn sàng Control MediaElement chưa
            var mediaElement = await ResolveNarrationMediaElementAsync();
            if (mediaElement is null)
            {
                throw new InvalidOperationException("Khong tim thay MediaElement NarrationPlayer tren UI.");
            }

            // Chuẩn hóa đường dẫn tệp tin
            var normalizedPath = NormalizeAudioPath(filePath);
            
            // Kiểm tra sự tồn tại của tệp tin trong Package Bundle của App
            await EnsureAudioAssetExistsAsync(normalizedPath, ct);
            Debug.WriteLine($"[NarrationService] Bat dau phat MP3: {normalizedPath}");

            Task playbackTask = Task.CompletedTask;

            // Việc điều khiển MediaElement phải diễn ra trên MainThread (UI Thread)
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                playbackTask = PlayWithMediaElementAsync(mediaElement, normalizedPath, ct);
            });

            await playbackTask;
            Debug.WriteLine("[NarrationService] MP3 hoan tat");
        });
    }

    /// <summary>
    /// Dừng toàn bộ các luồng thuyết minh ngay lập tức.
    /// </summary>
    public void StopAll()
    {
        try
        {
            // Hủy tác vụ đang chạy trong hàng đợi độc quyền
            _audioQueueManager.CancelCurrent();

            _ = MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var mediaElement = await ResolveNarrationMediaElementAsync();
                mediaElement?.Stop();
            });

            // Kết thúc chế độ giảm âm nhạc nền
            EndAudioDucking();
            Debug.WriteLine("[NarrationService] Da dung toan bo narration");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NarrationService] Loi StopAll: {ex.Message}");
        }
    }

    /// <summary>
    /// Hàm bao (Wrapper) thực thi logic thuyết minh độc quyền.
    /// - Áp dụng Audio Ducking.
    /// - Nhường quyền nếu có yêu cầu mới.
    /// </summary>
    private async Task RunExclusiveNarrationAsync(Func<CancellationToken, Task> work)
    {
        await _audioQueueManager.RunExclusiveAsync(async ct =>
        {
            try
            {
                // Bắt đầu Ducking (giảm âm lượng các app khác/nhạc nền)
                await BeginAudioDuckingAsync();
                
                // Delay nhỏ để Ducking có hiệu lực mượt mà trước khi phát tiếng
                await Task.Delay(120, ct); 
                
                await work(ct);
            }
            finally
            {
                // Đảm bảo phục hồi âm lượng kể cả khi lỗi
                EndAudioDucking();
            }
        });
    }

    /// <summary>
    /// Dừng phát âm thanh trên MediaElement nếu nó đang bận.
    /// </summary>
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

    /// <summary>
    /// Làm sạch văn bản dành cho TTS.
    /// Engine TTS thường phát âm rất kỳ quặc hoặc bị lỗi "tịt" tiếng khi gặp các icon Emoji.
    /// </summary>
    private static string SanitizeTtsText(string input)
    {
        var text = input
            .Replace("🔊", " ")
            .Replace("🧭", " ")
            .Replace("🔍", " ")
            .Replace("✕", " ")
            .Replace("★", " ")
            .Replace("↔", " ");

        // Chuẩn hóa khoảng trắng thừa
        text = Regex.Replace(text, "\\s+", " ").Trim();
        return text;
    }

    /// <summary>
    /// Chuẩn hóa đường dẫn Asset (thay đổi dấu gạch chéo, loại bỏ tiền tố thừa).
    /// </summary>
    private static string NormalizeAudioPath(string filePath)
    {
        var path = filePath.Replace("\\", "/").Trim();
        if (path.StartsWith("Resources/Raw/", StringComparison.OrdinalIgnoreCase))
        {
            path = path["Resources/Raw/".Length..];
        }

        return path.TrimStart('/');
    }

    /// <summary>
    /// Xác thực tệp tin Asset có tồn tại trong Bundle ứng dụng hay không trước khi phát.
    /// </summary>
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

    /// <summary>
    /// Tìm kiếm Locale phù hợp nhất cho engine TTS dựa trên mã ngôn ngữ (vi, ja, en).
    /// </summary>
    private async Task<Locale?> ResolveBestLocaleAsync(string languageCode)
    {
        try
        {
            var locales = await TextToSpeech.Default.GetLocalesAsync();

            // Duyệt theo chuỗi ưu tiên (Fallback chain)
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

    /// <summary>
    /// Logic so khớp ngôn ngữ: Hỗ trợ cả mã vùng (vi-VN) và mã ngắn (vi).
    /// </summary>
    private static Locale? ResolveLocaleCandidate(System.Collections.Generic.IEnumerable<Locale> locales, string candidate)
    {
        var normalized = candidate.Trim();

        // Ưu tiên so khớp chính xác cả quốc gia (ví dụ: ja-JP)
        var exact = locales.FirstOrDefault(l =>
            string.Equals($"{l.Language}-{l.Country}", normalized, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        // Nếu không có, so khớp theo mã ngôn ngữ gốc (Language code)
        var shortCode = normalized.Split('-')[0].ToLowerInvariant();
        if (shortCode == "jp")
        {
            shortCode = "ja";
        }

        return locales.FirstOrDefault(l =>
            string.Equals(l.Language, shortCode, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Truy tìm MediaElement trên UI. Ưu tiên WeakReference đã đăng ký, 
    /// nếu không có thì dò tìm theo Name "NarrationPlayer" trong cây giao diện.
    /// </summary>
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

    /// <summary>
    /// Thực thi lệnh phát nhạc trên MediaElement và đợi cho đến khi hoàn tất hoặc bị hủy bỏ.
    /// </summary>
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

        // Đăng ký hành động hủy nếu CancellationToken được kích hoạt
        using var registration = ct.Register(() =>
        {
            mediaElement.Stop();
            mediaElement.MediaEnded -= CompletedHandler;
            tcs.TrySetCanceled(ct);
        });

        // Đảm bảo không bị treo vô hạn nếu sự kiện MediaEnded không nổ (Timeout 12s)
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(12), ct));
        if (completed != tcs.Task)
        {
            mediaElement.Stop();
            mediaElement.MediaEnded -= CompletedHandler;
            throw new TimeoutException($"Khong the phat xong file am thanh: {assetPath}");
        }

        await tcs.Task;
    }

    // Các hàm xử lý Audio Ducking đặc thù cho từng nền tảng (Platform-specific)
    private partial Task BeginAudioDuckingAsync();
    private partial void EndAudioDucking();
}
