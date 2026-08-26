using System.Collections.ObjectModel;
using DropCaptureList.Windows.Helpers;
using DropCaptureList.Windows.Models;
using DropCaptureList.Windows.Services;

namespace DropCaptureList.Windows.ViewModels;

public sealed class LoginViewModel : ViewModelBase
{
    private readonly IIdentityService _identity;
    private string _email = string.Empty;
    private string _householdName = string.Empty;
    private string _statusMessage = "Enter your email and household, then Continue.";
    private bool _isBusy;

    public LoginViewModel(IIdentityService identity, UserSession? remembered)
    {
        _identity = identity;
        KnownHouseholds = new ObservableCollection<string>();
        ContinueCommand = new RelayCommand(Continue, () =>
            !_isBusy && !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(HouseholdName));

        if (remembered is not null)
        {
            Email = remembered.Email ?? string.Empty;
            HouseholdName = remembered.TenantName ?? string.Empty;
        }
    }

    public ObservableCollection<string> KnownHouseholds { get; }

    public string Email
    {
        get => _email;
        set
        {
            SetProperty(ref _email, value);
            ContinueCommand.RaiseCanExecuteChanged();
        }
    }

    public string HouseholdName
    {
        get => _householdName;
        set
        {
            SetProperty(ref _householdName, value);
            ContinueCommand.RaiseCanExecuteChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public RelayCommand ContinueCommand { get; }

    public UserSession? SignedInSession { get; private set; }

    public event EventHandler? SignedIn;

    public async void LoadHouseholdHints()
    {
        try
        {
            var names = await Task.Run(() => _identity.KnownHouseholds().ToList());
            KnownHouseholds.Clear();
            foreach (var name in names)
            {
                KnownHouseholds.Add(name);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async void Continue()
    {
        _isBusy = true;
        ContinueCommand.RaiseCanExecuteChanged();
        StatusMessage = "Signing in…";
        try
        {
            var email = Email;
            var household = HouseholdName;
            SignedInSession = await Task.Run(() => _identity.SignIn(email, household));
            StatusMessage = string.Empty;
            SignedIn?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            ContinueCommand.RaiseCanExecuteChanged();
        }
    }
}
