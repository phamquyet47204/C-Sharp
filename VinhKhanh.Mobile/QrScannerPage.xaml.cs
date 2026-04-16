using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Maui.ApplicationModel;
using ZXing.Net.Maui;
using VinhKhanhFoodStreet.Configuration;
using VinhKhanhFoodStreet.Services;

namespace VinhKhanhFoodStreet;

public partial class QrScannerPage : ContentPage
{
    private readonly HttpClient _httpClient;
    private readonly INarrationService _narrationService;
    private readonly IAppLanguageService _appLanguageService;
    private bool _isProcessingScan;

    public QrScannerPage(INarrationService narrationService, IAppLanguageService appLanguageService)
    {
        InitializeComponent();

        _narrationService = narrationService;
        _appLanguageService = appLanguageService;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(AppConfig.BaseApiUrl),
            Timeout = TimeSpan.FromSeconds(12)
        };

        CameraView.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.TwoDimensional,
            AutoRotate = true,
            Multiple = false
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            await EnsureCameraPermissionAsync();
            CameraView.IsDetecting = true;
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Không thể bật camera quét QR: {ex.Message}";
        }
    }

    protected override void OnDisappearing()
    {
        CameraView.IsDetecting = false;
        base.OnDisappearing();
    }

    private async void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        if (_isProcessingScan)
        {
            return;
        }

        var rawValue = e.Results?.FirstOrDefault()?.Value?.Trim();
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return;
        }

        _isProcessingScan = true;
        CameraView.IsDetecting = false;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            StatusLabel.Text = "Đã nhận mã QR, đang tải nội dung...";
        });

        try
        {
            var token = ExtractQrToken(rawValue);
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("QR không đúng định dạng /api/qr/{token}.");
            }

            var payload = await _httpClient.GetFromJsonAsync<QrResolveResponse>($"api/qr/{WebUtility.UrlEncode(token)}");
            if (payload is null)
            {
                throw new InvalidOperationException("Máy chủ không trả về dữ liệu QR.");
            }

            var preferredLanguage = _appLanguageService.GetEffectiveLanguage();
            var localization = SelectLocalization(payload.Localizations, preferredLanguage)
                ?? payload.Localizations.FirstOrDefault();

            if (localization is null)
            {
                throw new InvalidOperationException("POI này chưa có nội dung thuyết minh.");
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusLabel.Text = "Quét QR thành công.";
                ResultNameLabel.Text = localization.Name;
                ResultDescriptionLabel.Text = localization.Description;
                ResultCard.IsVisible = true;
            });

            var speakLanguage = _appLanguageService.GetEffectiveLanguage(localization.LanguageCode);
            await _narrationService.SpeakAsync(localization.Description, speakLanguage);
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusLabel.Text = $"Không thể xử lý QR: {ex.Message}";
                ResultCard.IsVisible = false;
            });
        }
        finally
        {
            _isProcessingScan = false;
        }
    }

    private async Task EnsureCameraPermissionAsync()
    {
        var cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (cameraStatus != PermissionStatus.Granted)
        {
            cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
        }

        if (cameraStatus != PermissionStatus.Granted)
        {
            throw new PermissionException("Bạn chưa cấp quyền camera cho ứng dụng.");
        }
    }

    private static string? ExtractQrToken(string rawValue)
    {
        if (Uri.TryCreate(rawValue, UriKind.Absolute, out var absoluteUri))
        {
            var segments = absoluteUri.AbsolutePath
                .Trim('/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length >= 3 &&
                string.Equals(segments[0], "api", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(segments[1], "qr", StringComparison.OrdinalIgnoreCase))
            {
                return segments[2];
            }

            return segments.LastOrDefault();
        }

        return rawValue.Trim();
    }

    private static QrLocalization? SelectLocalization(IReadOnlyList<QrLocalization> localizations, string preferredLanguage)
    {
        foreach (var candidate in BuildLanguageFallbackChain(preferredLanguage))
        {
            var match = localizations.FirstOrDefault(l =>
                string.Equals(NormalizeLanguage(l.LanguageCode), candidate, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static IEnumerable<string> BuildLanguageFallbackChain(string preferredLanguage)
    {
        var normalized = NormalizeLanguage(preferredLanguage);
        yield return normalized;

        if (!string.Equals(normalized, "en", StringComparison.OrdinalIgnoreCase))
        {
            yield return "en";
        }

        if (!string.Equals(normalized, "vi", StringComparison.OrdinalIgnoreCase))
        {
            yield return "vi";
        }
    }

    private static string NormalizeLanguage(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return "vi";
        }

        var shortCode = languageCode.Trim().Replace('_', '-').ToLowerInvariant().Split('-')[0];
        return shortCode == "jp" ? "ja" : shortCode;
    }

    private void OnScanAgainClicked(object? sender, EventArgs e)
    {
        ResultCard.IsVisible = false;
        StatusLabel.Text = "Đang chờ quét QR...";
        CameraView.IsDetecting = true;
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private sealed class QrResolveResponse
    {
        [JsonPropertyName("poiId")]
        public int PoiId { get; set; }

        [JsonPropertyName("qrToken")]
        public string QrToken { get; set; } = string.Empty;

        [JsonPropertyName("localizations")]
        public List<QrLocalization> Localizations { get; set; } = new();
    }

    private sealed class QrLocalization
    {
        [JsonPropertyName("languageCode")]
        public string LanguageCode { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }
}
