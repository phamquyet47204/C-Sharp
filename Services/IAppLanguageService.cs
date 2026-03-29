using System.Collections.Generic;

namespace VinhKhanhFoodStreet.Services;

public interface IAppLanguageService
{
    string GetPreferredLanguageOrEmpty();
    string GetEffectiveLanguage(string? requestedLanguageCode = null);
    IReadOnlyList<string> GetLanguageFallbackChain(string? requestedLanguageCode = null);
    void SetPreferredLanguage(string languageCode);
}
