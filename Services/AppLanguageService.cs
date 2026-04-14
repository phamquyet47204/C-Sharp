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

    public string T(string key, string lang)
    {
        return (lang, key) switch
        {
            // App Core
            ("en", "AppTitle") => "Vinh Khanh Food Street",
            ("ja", "AppTitle") => "ビンカイン フードストリート",
            (_, "AppTitle") => "Vĩnh Khánh Food Street",

            ("en", "MapTab") => "Map",
            ("ja", "MapTab") => "地図",
            (_, "MapTab") => "Bản đồ",

            ("en", "ListTab") => "List",
            ("ja", "ListTab") => "一覧",
            (_, "ListTab") => "Danh sách",

            // Categories
            ("en", "CategoryAll") => "All",
            ("ja", "CategoryAll") => "すべて",
            (_, "CategoryAll") => "Tất cả",

            ("en", "CategorySnail") => "Snails & Seafood",
            ("ja", "CategorySnail") => "巻貝・海鮮",
            (_, "CategorySnail") => "Ốc & Hải sản",

            ("en", "CategoryBbq") => "BBQ & Hotpot",
            ("ja", "CategoryBbq") => "焼肉/鍋",
            (_, "CategoryBbq") => "Đồ nướng & Lẩu",

            ("en", "CategoryStreet") => "Street Food",
            ("ja", "CategoryStreet") => "ストリートフード",
            (_, "CategoryStreet") => "Ăn vặt",

            ("en", "CategoryPhotoSpot") => "Photo Spots",
            ("ja", "CategoryPhotoSpot") => "写真スポット",
            (_, "CategoryPhotoSpot") => "Check-in & Sống ảo",

            ("en", "CategoryDrink") => "Drinks",
            ("ja", "CategoryDrink") => "ドリンク",
            (_, "CategoryDrink") => "Đồ uống",

            ("en", "CategoryUtility") => "Utilities",
            ("ja", "CategoryUtility") => "ユーティリティ",
            (_, "CategoryUtility") => "Tiện ích",

            // Search
            ("en", "Search") => "Find",
            ("ja", "Search") => "検索",
            (_, "Search") => "Tìm",

            ("en", "SearchPlaceholder") => "Search restaurants...",
            ("ja", "SearchPlaceholder") => "店名を検索...",
            (_, "SearchPlaceholder") => "Tìm quán trên bản đồ...",

            // Settings Page
            ("en", "SettingsTitle") => "Settings",
            ("ja", "SettingsTitle") => "設定",
            (_, "SettingsTitle") => "Cài đặt",

            ("en", "LanguageSection") => "Language",
            ("ja", "LanguageSection") => "言語 / Language",
            (_, "LanguageSection") => "Ngôn ngữ / Language",

            ("en", "AccessPassSection") => "Access Pass",
            ("ja", "AccessPassSection") => "サービスプラン / Access Pass",
            (_, "AccessPassSection") => "Gói dịch vụ / Access Pass",

            ("en", "StatusActive") => "Active",
            ("ja", "StatusActive") => "有効",
            (_, "StatusActive") => "Đã kích hoạt",

            ("en", "StatusTrialActive") => "Trial Active",
            ("ja", "StatusTrialActive") => "試用版が有効",
            (_, "StatusTrialActive") => "Gói dùng thử",

            ("en", "DaysRemaining") => "days remaining",
            ("ja", "DaysRemaining") => "日残り",
            (_, "DaysRemaining") => "ngày còn lại",

            ("en", "StatusInactive") => "Inactive",
            ("ja", "StatusInactive") => "未アクティブ",
            (_, "StatusInactive") => "Chưa kích hoạt",

            ("en", "TrialExpired") => "7-day trial expired or not used.",
            ("ja", "TrialExpired") => "7日間のトライアルが期限切れか未使用です。",
            (_, "TrialExpired") => "Gói dùng thử 7 ngày đã hết hạn hoặc chưa dùng.",

            ("en", "StatusExpiryDate") => "Access Pass expires on: ",
            ("ja", "StatusExpiryDate") => "有効期限: ",
            (_, "StatusExpiryDate") => "Gói Access Pass còn hiệu lực đến: ",

            ("en", "UnlockAllMessage") => "Unlock full narration and detailed maps for just $1/7 days.",
            ("ja", "UnlockAllMessage") => "わずか1ドル/7日間で、すべてのナレーションと詳細マップを利用できます。",
            (_, "UnlockAllMessage") => "Mở khóa toàn bộ tính năng thuyết minh và bản đồ chi tiết chỉ với 1$/7 ngày.",

            ("en", "BuyPassButton") => "Buy Access Pass ($1/7 days)",
            ("ja", "BuyPassButton") => "Access Passを購入 ($1/7日間)",
            (_, "BuyPassButton") => "Mua Access Pass ($1/7 ngày)",

            ("en", "RenewPassButton") => "Renew for 7 days ($1)",
            ("ja", "RenewPassButton") => "7日間延長 ($1)",
            (_, "RenewPassButton") => "Gia hạn thêm 7 ngày ($1)",

            // Common
            ("en", "Ok") => "OK",
            ("ja", "Ok") => "OK",
            (_, "Ok") => "OK",

            ("en", "Cancel") => "Cancel",
            ("ja", "Cancel") => "キャンセル",
            (_, "Cancel") => "Hủy",

            ("en", "Success") => "Success",
            ("ja", "Success") => "成功",
            (_, "Success") => "Thành công",

            ("en", "Confirm") => "Confirm",
            ("ja", "Confirm") => "確認",
            (_, "Confirm") => "Xác nhận",

            ("en", "BuyPrompt") => "Do you want to buy 7 days Access Pass for $1? (Simulation)",
            ("ja", "BuyPrompt") => "1ドルで7日間のAccess Passを購入しますか？（決済シミュレーション）",
            (_, "BuyPrompt") => "Bạn có muốn mua gói Access Pass 7 ngày với giá $1 không? (Giả lập thanh toán)",

            ("en", "BuySuccess") => "Thank you! Your Access Pass is now active.",
            ("ja", "BuySuccess") => "ありがとうございます！Access Passが有効になりました。",
            (_, "BuySuccess") => "Cảm ơn bạn! Gói Access Pass của bạn đã được kích hoạt.",

            // GPS & Maps
            ("en", "GpsTracking") => "Tracking location...",
            ("ja", "GpsTracking") => "位置追跡中...",
            (_, "GpsTracking") => "Đang theo dõi vị trí...",

            ("en", "GpsStartFailed") => "Cannot start GPS",
            ("ja", "GpsStartFailed") => "GPSを開始できません",
            (_, "GpsStartFailed") => "Không thể khởi động GPS",

            ("en", "CenteredToYourLocation") => "Centered to your location",
            ("ja", "CenteredToYourLocation") => "現在地に移動しました",
            (_, "CenteredToYourLocation") => "Đã căn giữa vị trí của bạn",

            ("en", "NarrationFallback") => "This is",
            ("ja", "NarrationFallback") => "こちらは",
            (_, "NarrationFallback") => "Đây là",

            _ => key
        };
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
