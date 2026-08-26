using System.Windows;
using DropCaptureList.Windows.ViewModels;

namespace DropCaptureList.Windows.Views;

public partial class AdminWindow : Window
{
    public AdminWindow(AdminViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
