using Microsoft.Extensions.DependencyInjection;

namespace VinhKhanhFoodStreet;

public partial class App : Application
{
	private readonly IServiceProvider _serviceProvider;

	public App(IServiceProvider serviceProvider)
	{
		InitializeComponent();
		_serviceProvider = serviceProvider;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
#if WINDOWS
		Page rootPage = CreateWindowsLandingPage();
#else
		Page rootPage = _serviceProvider.GetRequiredService<MainPage>();
#endif

		var window = new Window(rootPage)
		{
			Title = "Vĩnh Khánh Food Street",
			Width = 1440,
			Height = 900
		};

		#if WINDOWS
		window.HandlerChanged += (_, _) =>
		{
			if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
			{
				nativeWindow.Activate();
			}
		};
		#endif

		return window;
	}

#if WINDOWS
	private static Page CreateWindowsLandingPage()
	{
		return new ContentPage
		{
			BackgroundColor = Colors.White,
			Content = new Grid
			{
				Padding = 32,
				RowDefinitions =
				{
					new RowDefinition(GridLength.Auto),
					new RowDefinition(GridLength.Auto),
					new RowDefinition(GridLength.Star)
				},
				Children =
				{
					new Label
					{
						Text = "Vĩnh Khánh Food Street",
						FontSize = 28,
						FontAttributes = FontAttributes.Bold,
						TextColor = Color.FromArgb("#FF7F50")
					},
					new Label
					{
						Text = "Bản Windows đang khởi động an toàn để tránh lỗi WinUI XAML.",
						FontSize = 16,
						TextColor = Colors.Black,
						Margin = new Thickness(0, 48, 0, 0)
					},
					new Label
					{
						Text = "Nếu bạn muốn, mình sẽ tiếp tục bóc tách phần MainPage để chạy bản desktop đầy đủ.",
						FontSize = 14,
						TextColor = Colors.DarkGray,
						Margin = new Thickness(0, 12, 0, 0)
					}
				}
			}
		};
	}
#endif
}