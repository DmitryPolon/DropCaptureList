using DropCaptureList.Windows.Helpers;
using DropCaptureList.Windows.Models;
using DropCaptureList.Windows.Services;
using System.Collections.ObjectModel;
using System.Globalization;

namespace DropCaptureList.Windows.ViewModels;

public sealed class AdminViewModel : ViewModelBase
{
    private readonly IIdentityService _identity;
    private readonly ICaptureService _captures;
    private string _newEmail = string.Empty;
    private string _newNickname = string.Empty;
    private string _newHousehold = string.Empty;
    private bool _newIsAppAdmin;
    private string _newHouseholdName = string.Empty;
    private string _newHouseholdMotto = string.Empty;
    private string _mottoHouseholdName = string.Empty;
    private string _mottoText = string.Empty;
    private string _removeHouseholdName = string.Empty;
    private AdminUserRow? _selectedUser;
    private string _statusMessage = "Full admin (reports, users) will live on the web app. This window is a temporary helper.";
    private string _dataUsedLabel = "Data used: …";
    private string _vCoreLabel = "Free compute: …";
    private string _lastClearedLabel = "Last cleared: …";

    public AdminViewModel(IIdentityService identity, ICaptureService captures)
    {
        _identity = identity;
        _captures = captures;
        Users = new ObservableCollection<AdminUserRow>();
        AddUserCommand = new RelayCommand(AddUser);
        CreateHouseholdCommand = new RelayCommand(CreateHousehold);
        SaveMottoCommand = new RelayCommand(SaveMotto);
        RemoveFromHouseholdCommand = new RelayCommand(RemoveFromHousehold);
        Reload();
    }

    public ObservableCollection<AdminUserRow> Users { get; }

    public string WebAppUrl => AdminSnapshot.WebAppUrl;

    public string DataUsedLabel
    {
        get => _dataUsedLabel;
        private set => SetProperty(ref _dataUsedLabel, value);
    }

    public string VCoreLabel
    {
        get => _vCoreLabel;
        private set => SetProperty(ref _vCoreLabel, value);
    }

    public string LastClearedLabel
    {
        get => _lastClearedLabel;
        private set => SetProperty(ref _lastClearedLabel, value);
    }

    public string NewEmail
    {
        get => _newEmail;
        set => SetProperty(ref _newEmail, value);
    }

    public string NewNickname
    {
        get => _newNickname;
        set => SetProperty(ref _newNickname, value);
    }

    public string NewHousehold
    {
        get => _newHousehold;
        set => SetProperty(ref _newHousehold, value);
    }

    public bool NewIsAppAdmin
    {
        get => _newIsAppAdmin;
        set => SetProperty(ref _newIsAppAdmin, value);
    }

    public string NewHouseholdName
    {
        get => _newHouseholdName;
        set => SetProperty(ref _newHouseholdName, value);
    }

    public string NewHouseholdMotto
    {
        get => _newHouseholdMotto;
        set => SetProperty(ref _newHouseholdMotto, value);
    }

    public string MottoHouseholdName
    {
        get => _mottoHouseholdName;
        set => SetProperty(ref _mottoHouseholdName, value);
    }

    public string MottoText
    {
        get => _mottoText;
        set => SetProperty(ref _mottoText, value);
    }

    public AdminUserRow? SelectedUser
    {
        get => _selectedUser;
        set => SetProperty(ref _selectedUser, value);
    }

    public string RemoveHouseholdName
    {
        get => _removeHouseholdName;
        set => SetProperty(ref _removeHouseholdName, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public RelayCommand AddUserCommand { get; }
    public RelayCommand CreateHouseholdCommand { get; }
    public RelayCommand SaveMottoCommand { get; }
    public RelayCommand RemoveFromHouseholdCommand { get; }

    private void Reload()
    {
        Users.Clear();
        foreach (var user in _identity.ListUsers())
        {
            Users.Add(user);
        }

        try
        {
            var snap = _captures.GetAdminSnapshot();
            var mb = snap.DataUsedBytes / (1024.0 * 1024.0);
            DataUsedLabel =
                $"Data used: {snap.DataUsedPercent.ToString("0.###", CultureInfo.CurrentCulture)}% of 32 GB free ({mb.ToString("0.0", CultureInfo.CurrentCulture)} MB)";
            if (snap.VCoreRemaining is { } left)
            {
                var usedPct = Math.Max(0, (AdminSnapshot.FreeVCoreSeconds - left) * 100.0 / AdminSnapshot.FreeVCoreSeconds);
                VCoreLabel =
                    $"Free compute: {usedPct.ToString("0.###", CultureInfo.CurrentCulture)}% of 100,000 vCore-seconds used ({left.ToString("N0", CultureInfo.CurrentCulture)} left this month)";
            }
            else
            {
                VCoreLabel = snap.VCoreError is { Length: > 0 } ? $"Free compute: {snap.VCoreError}" : "Free compute: not available.";
            }
            if (snap.LastClearedAt is { } at)
            {
                var who = string.IsNullOrWhiteSpace(snap.LastClearedHousehold) ? "a household" : snap.LastClearedHousehold;
                var note = snap.LastClearedIsApproximate ? " (last completed item; run database/08 then Clear list for an exact stamp)" : "";
                LastClearedLabel = $"Last cleared: {who} · {at.ToLocalTime():g}{note}";
            }
            else
            {
                LastClearedLabel = "Last cleared: not recorded yet. Use Clear list after running database/08.";
            }
        }
        catch (Exception ex)
        {
            DataUsedLabel = "Data used: could not read.";
            VCoreLabel = "Free compute: could not read.";
            LastClearedLabel = ex.Message;
        }
    }

    private void AddUser()
    {
        try
        {
            _identity.AddUser(NewEmail, NewNickname, NewHousehold, NewNickname, NewIsAppAdmin);
            StatusMessage = "User added.";
            NewEmail = string.Empty;
            NewNickname = string.Empty;
            NewIsAppAdmin = false;
            Reload();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void CreateHousehold()
    {
        try
        {
            _identity.CreateHousehold(NewHouseholdName, NewHouseholdMotto);
            StatusMessage = "Household created.";
            NewHouseholdName = string.Empty;
            NewHouseholdMotto = string.Empty;
            Reload();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void SaveMotto()
    {
        try
        {
            _identity.SetHouseholdMotto(MottoHouseholdName, MottoText);
            StatusMessage = string.IsNullOrWhiteSpace(MottoText) ? "Motto cleared." : "Motto saved.";
            Reload();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void RemoveFromHousehold()
    {
        try
        {
            if (SelectedUser is null)
            {
                throw new InvalidOperationException("Select a user in the list.");
            }

            _identity.RemoveFromHousehold(SelectedUser.UserId, RemoveHouseholdName);
            StatusMessage = "Removed from household.";
            Reload();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }
}
