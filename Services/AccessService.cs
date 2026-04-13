using System;
using Microsoft.Maui.Storage;

namespace VinhKhanhFoodStreet.Services;

public class AccessService
{
    private const string AccessPassExpiryKey = "access_pass_expiry";

    public bool HasActivePass()
    {
        var expiryStr = Preferences.Get(AccessPassExpiryKey, string.Empty);
        if (DateTime.TryParse(expiryStr, out var expiryDate))
        {
            return expiryDate > DateTime.UtcNow;
        }

        return false;
    }

    public DateTime? GetExpiryDate()
    {
        var expiryStr = Preferences.Get(AccessPassExpiryKey, string.Empty);
        if (DateTime.TryParse(expiryStr, out var expiryDate))
        {
            return expiryDate;
        }
        return null;
    }

    public void PurchaseSuccess(int days = 7)
    {
        var newExpiry = DateTime.UtcNow.AddDays(days);
        Preferences.Set(AccessPassExpiryKey, newExpiry.ToString("O"));
    }
}
