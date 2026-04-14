using VinhKhanh.Mobile.ViewModels;

namespace VinhKhanh.Mobile.Views;

public partial class CheckInPage : ContentPage
{
	public CheckInPage(CheckInViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
