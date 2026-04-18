using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Maui.Storage;

namespace VinhKhanhFoodStreet.Services;

/// <summary>
/// AppLanguageService: Dịch vụ quản lý đa ngôn ngữ và từ điển nội bộ của ứng dụng.
/// 
/// Chức năng chính:
/// - Quản lý ngôn ngữ ưu tiên của người dùng (Preferred Language).
/// - Xây dựng chuỗi Fallback ngôn ngữ (Fallback Chain) để đảm bảo luôn có nội dung hiển thị.
/// - Lưu trữ từ điển tĩnh cho các chuỗi văn bản UI (Title, Tabs, Categories, v.v.).
/// - Chuẩn hóa mã ngôn ngữ (Normalize Language Code) tương thích với tiêu chuẩn ISO.
/// </summary>
public class AppLanguageService : IAppLanguageService
{
    private const string PreferredLanguageKey = "app_preferred_language";

    /// <summary>
    /// Lấy ngôn ngữ người dùng đã chọn thủ công trong phần cài đặt.
    /// Trả về chuỗi rỗng nếu chưa bao giờ chọn.
    /// </summary>
    public string GetPreferredLanguageOrEmpty()
    {
        return Preferences.Default.Get(PreferredLanguageKey, string.Empty);
    }

    /// <summary>
    /// Quyết định ngôn ngữ hiệu dụng (Effective Language) để thực hiện thuyết minh hoặc hiển thị UI.
    /// Thứ tự ưu tiên:
    /// 1. Ngôn ngữ đã chọn trong Cài đặt app.
    /// 2. Ngôn ngữ yêu cầu cụ thể từ tham số truyền vào.
    /// 3. Ngôn ngữ của Hệ điều hành (UI Culture).
    /// 4. Mặc định là Tiếng Việt (vi).
    /// </summary>
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

    /// <summary>
    /// Xây dựng danh sách ưu tiên tìm kiếm nội bộ (Fallback Chain).
    /// Ví dụ: Nếu xem tiếng Nhật (ja), chuỗi sẽ là [ja, en, vi]. 
    /// Nếu một POI không có bản dịch tiếng Nhật, hệ thống sẽ tự tìm bản dịch tiếng Anh, sau đó là tiếng Việt.
    /// </summary>
    public IReadOnlyList<string> GetLanguageFallbackChain(string? requestedLanguageCode = null)
    {
        var primary = GetEffectiveLanguage(requestedLanguageCode);
        var chain = new List<string> { primary };

        // Tiếng Anh luôn là lựa chọn thứ 2 nếu không phải tiếng chính
        if (!chain.Contains("en", StringComparer.OrdinalIgnoreCase))
        {
            chain.Add("en");
        }

        // Tiếng Việt là lựa chọn cuối cùng (Base language)
        if (!chain.Contains("vi", StringComparer.OrdinalIgnoreCase))
        {
            chain.Add("vi");
        }

        return chain;
    }

    /// <summary>
    /// Lưu lựa chọn ngôn ngữ của người dùng vào bộ nhớ bền vững.
    /// </summary>
    public void SetPreferredLanguage(string languageCode)
    {
        var normalized = NormalizeLanguageCode(languageCode) ?? "vi";
        Preferences.Default.Set(PreferredLanguageKey, normalized);
    }

    /// <summary>
    /// Hàm dịch thuật (Translation function) thủ công.
    /// Truy xuất chuỗi ký tự dựa trên Mã định danh (Key) và Ngôn ngữ (Lang).
    /// </summary>
    public string T(string key, string lang)
    {
        return (lang, key) switch
        {
            // --- Core App UI ---
            ("en", "AppTitle") => "Vinh Khanh Food Street",
            ("ja", "AppTitle") => "ビンカイン フードストリート",
            (_, "AppTitle") => "Vĩnh Khánh Food Street",

            ("en", "MapTab") => "Map",
            ("ja", "MapTab") => "地図",
            (_, "MapTab") => "Bản đồ",

            ("en", "ListTab") => "List",
            ("ja", "ListTab") => "一覧",
            (_, "ListTab") => "Danh sách",

            // --- Danh mục Quán ---
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

            // --- Tìm kiếm ---
            ("en", "Search") => "Find",
            ("ja", "Search") => "検索",
            (_, "Search") => "Tìm",

            ("en", "SearchPlaceholder") => "Search restaurants...",
            ("ja", "SearchPlaceholder") => "店名を検索...",
            (_, "SearchPlaceholder") => "Tìm quán trên bản đồ...",

            // --- Trang Cài đặt & Bản quyền ---
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

            // --- Nút bấm và phản hồi chung ---
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

            // --- GPS & Trạng thái ---
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

    /// <summary>
    /// Chuẩn hóa mã ngôn ngữ để sử dụng nội bộ.
    /// Xử lý các trường hợp đặc biệt như jp (alias của ja) hoặc định dạng vi_VN.
    /// </summary>
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
            "jp" => "ja", // Đồng nhất jp thành ja (tiêu chuẩn ISO)
            _ => "vi"
        };
    }
}
