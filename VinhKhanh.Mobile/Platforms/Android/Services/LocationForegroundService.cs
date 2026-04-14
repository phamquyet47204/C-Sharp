#if ANDROID
using System;
using Debug = System.Diagnostics.Debug;
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Microsoft.Maui.ApplicationModel;

namespace VinhKhanhFoodStreet.Services;

/// <summary>
/// Foreground Service cho Android.
///
/// Tai sao can class native nay:
/// Android se gioi han app chay nen, dac biet khi tat man hinh.
/// Vi vay can StartForeground + notification ongoing de he dieu hanh uu tien giu process song.
/// </summary>
[Service(
    Exported = false,
    ForegroundServiceType = Android.Content.PM.ForegroundService.TypeLocation)]
public class LocationForegroundService : Service
{
    public const string ActionStart = "vinhkhanh.location.action.START";
    public const string ActionStop = "vinhkhanh.location.action.STOP";

    private const string ChannelId = "vinhkhanh_location_tracking_channel";
    private const string ChannelName = "Theo doi vi tri";
    private const int NotificationId = 1088;

    public override IBinder? OnBind(Intent? intent)
    {
        return null;
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        try
        {
            if (intent?.Action == ActionStop)
            {
                StopForeground(StopForegroundFlags.Remove);
                StopSelf();
                return StartCommandResult.NotSticky;
            }

            CreateNotificationChannel();
            var notification = BuildTrackingNotification();
            StartForeground(NotificationId, notification);

            Debug.WriteLine("[AndroidForegroundService] Bat dau foreground service thanh cong");
            return StartCommandResult.Sticky;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AndroidForegroundService] Loi khi start service: {ex.Message}");
            return StartCommandResult.NotSticky;
        }
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
        {
            return;
        }

        var manager = (NotificationManager?)GetSystemService(NotificationService);
        if (manager is null)
        {
            return;
        }

        var channel = new NotificationChannel(ChannelId, ChannelName, NotificationImportance.Low)
        {
            Description = "Kenh thong bao theo doi vi tri nen cho module thuyet minh"
        };

        manager.CreateNotificationChannel(channel);
    }

    private Notification BuildTrackingNotification()
    {
        var openAppIntent = Platform.CurrentActivity?.PackageManager?.GetLaunchIntentForPackage(PackageName)
                            ?? new Intent(this, typeof(MainActivity));

        openAppIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

        var pendingIntent = PendingIntent.GetActivity(
            this,
            0,
            openAppIntent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        return new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("Pho am thuc Vinh Khanh")
            .SetContentText("Đang theo dõi vị trí để thuyết minh phố ẩm thực")
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetContentIntent(pendingIntent)
            .SetOngoing(true)
            .SetAutoCancel(false)
            .SetPriority((int)NotificationPriority.Low)
            .Build();
    }
}

/// <summary>
/// Lop dieu khien de bat/tat Foreground Service tu LocationService shared.
/// </summary>
public static class AndroidLocationForegroundController
{
    public static void Start()
    {
        try
        {
            Debug.WriteLine("[LocationService] Bat dau Service (Android Foreground)");

            var context = Platform.AppContext;
            var startIntent = new Intent(context, typeof(LocationForegroundService));
            startIntent.SetAction(LocationForegroundService.ActionStart);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                context.StartForegroundService(startIntent);
            }
            else
            {
                context.StartService(startIntent);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LocationService] Loi bat foreground service Android: {ex.Message}");
        }
    }

    public static void Stop()
    {
        try
        {
            var context = Platform.AppContext;
            var stopIntent = new Intent(context, typeof(LocationForegroundService));
            stopIntent.SetAction(LocationForegroundService.ActionStop);
            context.StartService(stopIntent);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LocationService] Loi dung foreground service Android: {ex.Message}");
        }
    }
}
#endif
