using VinhKhanh.Mobile.ViewModels;

namespace VinhKhanh.Mobile.Views;

public partial class VisitorRegisterPage : ContentPage
{
    public VisitorRegisterPage(VisitorRegisterViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
