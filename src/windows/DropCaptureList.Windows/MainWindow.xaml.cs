using System.Linq;
using System.Windows;
using DropCaptureList.Windows.Models;
using DropCaptureList.Windows.ViewModels;
using DropCaptureList.Windows.Views;

namespace DropCaptureList.Windows;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        Loaded += (_, _) => ViewModel.LoadFromStore();
        viewModel.AdminRequested += (_, _) =>
        {
            var admin = new AdminWindow(new AdminViewModel(viewModel.Identity));
            admin.Owner = this;
            admin.ShowDialog();
        };
    }

    public MainViewModel ViewModel { get; }

    private void DeleteSelected_OnClick(object sender, RoutedEventArgs e)
    {
        var selected = ItemsGrid.SelectedItems.OfType<CapturedItem>().ToList();
        ViewModel.DeleteItems(selected);
    }

    private void ClearHousehold_OnClick(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Mark all items on this list as completed?\nThey stay in the database and show gray. Delete selected is for mistakes.",
            "Clear list",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            ViewModel.ClearHouseholdCommand.Execute(null);
        }
    }
}
