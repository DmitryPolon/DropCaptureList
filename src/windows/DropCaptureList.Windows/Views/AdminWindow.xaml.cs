using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using DropCaptureList.Windows.ViewModels;

namespace DropCaptureList.Windows.Views;

public partial class AdminWindow : Window
{
    public AdminWindow(AdminViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void WebAppLink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
