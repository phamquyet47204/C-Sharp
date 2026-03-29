using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Maui.Storage;

namespace VinhKhanhFoodStreet.Services;

public class AppLanguageService : IAppLanguageService
{
    private const string PreferredLanguageKey = "app_preferred_language";

    public string GetPreferredLanguageOrEmpty()
    {
        return Preferences.Default.Get(PreferredLanguageKey, string.Empty);
    }

    public string GetEffectiveLanguage(string? requestedLanguageCode = null)
    {
        var preferred = NormalizeLanguageCode(GetPreferredLanguageOrEmpty());
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            return preferred;
        }

        var normalizedRequested = NormalizeLanguageCode(requestedLanguageCode);
        if (!string.IsNullOrWhiteSpace(normalizedRequested))
        {
            return normalizedRequested;
        }

        return NormalizeLanguageCode(CultureInfo.CurrentUICulture.Name) ?? "vi";
    }

    public IReadOnlyList<string> GetLanguageFallbackChain(string? requestedLanguageCode = null)
    {
        var primary = GetEffectiveLanguage(requestedLanguageCode);
        var chain = new List<string> { primary };

        if (!chain.Contains("en", StringComparer.OrdinalIgnoreCase))
        {
            chain.Add("en");
        }

        if (!chain.Contains("vi", StringComparer.OrdinalIgnoreCase))
        {
            chain.Add("vi");
        }

        return chain;
    }

    public void SetPreferredLanguage(string languageCode)
    {
        var normalized = NormalizeLanguageCode(languageCode) ?? "vi";
        Preferences.Default.Set(PreferredLanguageKey, normalized);
    }

    private static string? NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return null;
        }

        var raw = languageCode.Trim().Replace('_', '-').ToLowerInvariant();
        var shortCode = raw.Split('-')[0];

        return shortCode switch
        {
            "vi" => "vi",
            "en" => "en",
            "ja" => "ja",
            "jp" => "ja",
            _ => "vi"
        };
    }
}
