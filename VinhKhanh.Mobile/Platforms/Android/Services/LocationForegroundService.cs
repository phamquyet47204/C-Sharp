#if ANDROID
using System;
using Debug = System.Diagnostics.Debug;
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

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

    public override void OnTaskRemoved(Intent? rootIntent)
    {
        base.OnTaskRemoved(rootIntent);
        
        Debug.WriteLine("[AndroidForegroundService] OnTaskRemoved: App bi quet bo. Dang gui tin hieu offline va kill process...");
        
        // Luu lai service de dung trong background thread vi context co the bi huy
        var services = IPlatformApplication.Current?.Services;
        
        // Chay mot Task ngam de gui tin hieu offline sau do tu sat (Kill process)
        Task.Run(async () => 
        {
            try 
            {
                var locationService = services?.GetService<ILocationService>();
                var analyticsService = services?.GetService<AnalyticsService>();

                // 1. Dung loop de dam bao khong con location_update
                if (locationService != null)
                {
                    _ = locationService.StopListeningAsync();
                }

                // 2. Gui tin hieu offline
                if (analyticsService != null)
                {
                    await analyticsService.TrackAppLifecycleAsync("offline");
                }
                
                // 3. Cho mot chut de dam bao package mang duoc gui di
                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LocationForegroundService] Loi trong luc thoat app: {ex.Message}");
            }
            finally
            {
                // 4. TU SAT: Giet chet hoan toan tien trinh de dung moi thread ngam (GPS, TTS, v.v.)
                Debug.WriteLine("[AndroidForegroundService] TU SAT: Dong hoan toan tien trinh ung dung.");
                Android.OS.Process.KillProcess(Android.OS.Process.MyPid());
            }
        });

        // Dung foreground service va xoa thong bao ngay lap tuc cho UI muot
        StopForeground(StopForegroundFlags.Remove);
        StopSelf();
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
