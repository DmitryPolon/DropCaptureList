using System.Windows;
using DropCaptureList.Windows.ViewModels;

namespace DropCaptureList.Windows.Views;

public partial class LoginWindow : Window
{
    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }

    public LoginViewModel ViewModel { get; }
}
