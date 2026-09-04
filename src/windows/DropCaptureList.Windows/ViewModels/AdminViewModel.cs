using DropCaptureList.Windows.Helpers;
using DropCaptureList.Windows.Models;
using DropCaptureList.Windows.Services;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace DropCaptureList.Windows.ViewModels;

public sealed class AdminViewModel : ViewModelBase
{
    private readonly IIdentityService _identity;
    private readonly ICaptureService _captures;
    private readonly StorageModeClient _mode;
    private readonly UserSession _session;
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
    private string _dataUsedLabel = "Data used: not loaded.";
    private string _vCoreLabel = "Free compute: …";
    private string _lastClearedLabel = "Last cleared: not loaded.";
    private bool _sqlBusy;

    public AdminViewModel(IIdentityService identity, ICaptureService captures, StorageModeClient mode, UserSession session)
    {
        _identity = identity;
        _captures = captures;
        _mode = mode;
        _session = session;
        Users = new ObservableCollection<AdminUserRow>();
        AddUserCommand = new RelayCommand(AddUser);
        CreateHouseholdCommand = new RelayCommand(CreateHousehold);
        SaveMottoCommand = new RelayCommand(SaveMotto);
        RemoveFromHouseholdCommand = new RelayCommand(RemoveFromHousehold);
        LoadSqlDetailsCommand = new RelayCommand(LoadSqlDetails, () => !_sqlBusy && AzureSelected);
        UseAzureCommand = new RelayCommand(() => SetMode("Azure"));
        UseFileCommand = new RelayCommand(() => SetMode("File"));
        try
        {
            _mode.Refresh();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }

        if (AzureSelected)
        {
            LoadVCore();
        }
        else
        {
            VCoreLabel = "Free compute: not used in File mode.";
            DataUsedLabel = "Data used: JSON files on the API (no SQL).";
            LastClearedLabel = "Last cleared: completed items are deleted in File mode.";
            LoadFileUsers();
        }
    }

    public ObservableCollection<AdminUserRow> Users { get; }

    public bool SqlWasUsed { get; private set; }

    public bool AzureSelected => !_mode.IsFile;

    public bool FileSelected => _mode.IsFile;

    public RelayCommand UseAzureCommand { get; }

    public RelayCommand UseFileCommand { get; }

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
    public RelayCommand LoadSqlDetailsCommand { get; }

    private void SetMode(string mode)
    {
        try
        {
            StatusMessage = mode == "File"
                ? "Switching to File (copies live SQL rows once if the folder is empty)…"
                : "Switching to Azure SQL…";
            _mode.Set(_session.Email, mode);
            RaisePropertyChanged(nameof(AzureSelected));
            RaisePropertyChanged(nameof(FileSelected));
            LoadSqlDetailsCommand.RaiseCanExecuteChanged();
            if (_mode.IsFile)
            {
                SqlWasUsed = true;
                VCoreLabel = "Free compute: not used in File mode.";
                DataUsedLabel = "Data used: JSON files on the API (no SQL).";
                LastClearedLabel = "Last cleared: completed items are deleted in File mode.";
                LoadFileUsers();
                StatusMessage = "File mode on. SignalR is live. SQL buttons are off.";
            }
            else
            {
                LoadVCore();
                StatusMessage = "Azure SQL mode on.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async void LoadFileUsers()
    {
        try
        {
            var users = await Task.Run(() => _identity.ListUsers().ToList());
            Users.Clear();
            foreach (var user in users)
            {
                Users.Add(user);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async void LoadVCore()
    {
        VCoreLabel = "Free compute: …";
        try
        {
            var snap = await Task.Run(() => _captures.GetVCoreSnapshot());
            ApplyVCore(snap);
        }
        catch (Exception ex)
        {
            VCoreLabel = $"Free compute: {ex.Message}";
        }
    }

    private async void LoadSqlDetails()
    {
        _sqlBusy = true;
        LoadSqlDetailsCommand.RaiseCanExecuteChanged();
        SqlWasUsed = true;
        DataUsedLabel = "Data used: connecting to Azure SQL…";
        LastClearedLabel = "Last cleared: connecting to Azure SQL…";
        StatusMessage = "Connecting to Azure SQL (paused databases take up to a minute)…";
        try
        {
            var users = await Task.Run(() => _identity.ListUsers().ToList());
            var snap = await Task.Run(() => _captures.GetSqlUsageSnapshot());
            Users.Clear();
            foreach (var user in users)
            {
                Users.Add(user);
            }

            ApplySqlUsage(snap);
            StatusMessage = "Loaded storage, last cleared, and users from SQL.";
        }
        catch (Exception ex)
        {
            DataUsedLabel = "Data used: could not read.";
            LastClearedLabel = ex.Message;
            StatusMessage = ex.Message;
        }
        finally
        {
            _sqlBusy = false;
            LoadSqlDetailsCommand.RaiseCanExecuteChanged();
        }
    }

    private void ApplyVCore(AdminSnapshot snap)
    {
        if (snap.VCoreRemaining is { } left)
        {
            var usedPct = Math.Max(0, (AdminSnapshot.FreeVCoreSeconds - left) * 100.0 / AdminSnapshot.FreeVCoreSeconds);
            var sample = snap.VCoreSampledAt is { } at
                ? $" · Azure sample {at.ToLocalTime():g}"
                : "";
            VCoreLabel =
                $"Free compute: {usedPct.ToString("0.#", CultureInfo.CurrentCulture)}% of 100,000 vCore-seconds used ({left.ToString("N0", CultureInfo.CurrentCulture)} left this month){sample}";
        }
        else
        {
            VCoreLabel = snap.VCoreError is { Length: > 0 } ? $"Free compute: {snap.VCoreError}" : "Free compute: not available.";
        }
    }

    private void ApplySqlUsage(AdminSnapshot snap)
    {
        var mb = snap.DataUsedBytes / (1024.0 * 1024.0);
        DataUsedLabel =
            $"Data used: {snap.DataUsedPercent.ToString("0.###", CultureInfo.CurrentCulture)}% of 32 GB free ({mb.ToString("0.0", CultureInfo.CurrentCulture)} MB)";
        if (snap.LastClearedAt is { } cleared)
        {
            var who = string.IsNullOrWhiteSpace(snap.LastClearedHousehold) ? "a household" : snap.LastClearedHousehold;
            var note = snap.LastClearedIsApproximate ? " (last completed item; run database/08 then Clear list for an exact stamp)" : "";
            LastClearedLabel = $"Last cleared: {who} · {cleared.ToLocalTime():g}{note}";
        }
        else
        {
            LastClearedLabel = "Last cleared: not recorded yet. Use Clear list after running database/08.";
        }
    }

    private void AddUser()
    {
        try
        {
            SqlWasUsed = true;
            _identity.AddUser(NewEmail, NewNickname, NewHousehold, NewNickname, NewIsAppAdmin);
            StatusMessage = "User added.";
            NewEmail = string.Empty;
            NewNickname = string.Empty;
            NewIsAppAdmin = false;
            LoadSqlDetails();
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
            SqlWasUsed = true;
            _identity.CreateHousehold(NewHouseholdName, NewHouseholdMotto);
            StatusMessage = "Household created.";
            NewHouseholdName = string.Empty;
            NewHouseholdMotto = string.Empty;
            LoadSqlDetails();
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
            SqlWasUsed = true;
            _identity.SetHouseholdMotto(MottoHouseholdName, MottoText);
            StatusMessage = string.IsNullOrWhiteSpace(MottoText) ? "Motto cleared." : "Motto saved.";
            LoadSqlDetails();
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

            SqlWasUsed = true;
            _identity.RemoveFromHousehold(SelectedUser.UserId, RemoveHouseholdName);
            StatusMessage = "Removed from household.";
            LoadSqlDetails();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }
}
