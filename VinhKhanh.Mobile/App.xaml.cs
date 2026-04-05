namespace VinhKhanh.Mobile;

public partial class App : Application
{
    public App(Views.MapPage mainPage)
    {
        InitializeComponent();
        MainPage = new NavigationPage(mainPage);
    }
}