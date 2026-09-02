using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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
            var adminVm = new AdminViewModel(viewModel.Identity, viewModel.Captures);
            var admin = new AdminWindow(adminVm);
            admin.Owner = this;
            admin.ShowDialog();
            if (adminVm.SqlWasUsed)
            {
                ViewModel.LoadHouseholdsAfterAdmin();
            }
        };
    }

    public MainViewModel ViewModel { get; }

    private void Household_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.SelectedItem is LocalTenant household)
        {
            ViewModel.SelectedHousehold = household;
        }
    }

    private void ReplicaCell_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2 || sender is not Border border || border.DataContext is not ReplicaCell cell)
        {
            return;
        }

        if (!ViewModel.BeginEdit(cell))
        {
            return;
        }

        e.Handled = true;
        Dispatcher.BeginInvoke(() =>
        {
            if (FindTextBox(border) is { } box)
            {
                box.Focus();
                box.SelectAll();
            }

            ViewModel.AllowLostFocus();
        }, DispatcherPriority.Loaded);
    }

    private void ReplicaEditor_OnLostFocus(object sender, RoutedEventArgs e) => ViewModel.EndEditFromFocus();

    private void ReplicaEditor_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ViewModel.EndEdit();
            Keyboard.ClearFocus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ViewModel.EndEdit(cancel: true);
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private static TextBox? FindTextBox(DependencyObject parent)
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is TextBox box)
            {
                return box;
            }

            if (FindTextBox(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private void DeleteSelected_OnClick(object sender, RoutedEventArgs e)
    {
        var selected = ItemsGrid.SelectedItems.OfType<CapturedItem>().ToList();
        ViewModel.DeleteItems(selected);
    }

    private void ClearHousehold_OnClick(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Mark all items on this list as completed?\nThey stay in the database and leave this window on Refresh.",
            "Clear list",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            ViewModel.ClearHouseholdCommand.Execute(null);
        }
    }
}
