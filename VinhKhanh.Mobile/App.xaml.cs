<<<<<<< HEAD
=======
using VinhKhanh.Mobile.Services;

>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
namespace VinhKhanh.Mobile;

public partial class App : Application
{
<<<<<<< HEAD
    public App(Views.MapPage mainPage)
    {
        InitializeComponent();
        MainPage = new NavigationPage(mainPage);
    }
=======
    private readonly AnalyticsService _analytics;
    private readonly AccessControlService _accessControl;

    public App(Views.MapPage mainPage, AnalyticsService analytics, AccessControlService accessControl)
    {
        InitializeComponent();
        _analytics = analytics;
        _accessControl = accessControl;
        MainPage = new NavigationPage(mainPage);
    }

    protected override void OnStart()
    {
        base.OnStart();
        _ = _analytics.TrackAppOpenAsync();
        // Đăng ký device lên server để bắt đầu tính 7 ngày trial
        _ = _accessControl.RegisterDeviceAsync();
    }
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
}