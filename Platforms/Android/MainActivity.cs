using Android.App;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;

namespace VinhKhanhFoodStreet;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);

		// Dong bo mau status bar voi mau chu dao de khong con day den tren cung.
		Window?.SetStatusBarColor(Android.Graphics.Color.ParseColor("#FF7F50"));
	}
}
