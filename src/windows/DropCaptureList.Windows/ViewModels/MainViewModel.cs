using System.Collections.ObjectModel;
using DropCaptureList.Windows.Helpers;
using DropCaptureList.Windows.Models;
using DropCaptureList.Windows.Services;

namespace DropCaptureList.Windows.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly ICaptureService _captures;
    private readonly ExcelSelectionCapture _excel;
    private readonly ProtectedSessionStore _sessions;
    private readonly IIdentityService _identity;
    private UserSession _session;
    private LocalTenant? _selectedHousehold;
    private string _statusMessage = "Open Excel, highlight cells, then capture. Each cell becomes one record.";
    private bool _showHouseholdSwitcher;

    private double _cellWidth = 100;

    public MainViewModel(
        UserSession session,
        ICaptureService captures,
        ExcelSelectionCapture excel,
        ProtectedSessionStore sessions,
        IIdentityService identity)
    {
        _session = session;
        _captures = captures;
        _excel = excel;
        _sessions = sessions;
        _identity = identity;

        CaptureCommand = new RelayCommand(CaptureFromExcel);
        ClearHouseholdCommand = new RelayCommand(ClearHousehold);
        SignOutCommand = new RelayCommand(SignOut);
        OpenAdminCommand = new RelayCommand(OpenAdmin, () => session.IsAppAdmin);

        Households = new ObservableCollection<LocalTenant>();
        Items = new ObservableCollection<CapturedItem>();
        ReplicaRows = new ObservableCollection<ReplicaRow>();
    }

    public IIdentityService Identity => _identity;

    public ObservableCollection<LocalTenant> Households { get; }
    public ObservableCollection<CapturedItem> Items { get; }
    public ObservableCollection<ReplicaRow> ReplicaRows { get; }

    public string Nickname => _session.Nickname;

    public string RoleLabel => _session.IsAppAdmin ? "App admin" : "Member";

    public bool IsAppAdmin => _session.IsAppAdmin;

    public string HouseholdLabel => _session.TenantName;

    public bool ShowHouseholdSwitcher
    {
        get => _showHouseholdSwitcher;
        private set => SetProperty(ref _showHouseholdSwitcher, value);
    }

    public LocalTenant? SelectedHousehold
    {
        get => _selectedHousehold;
        set
        {
            if (value is null || value.Id == _session.TenantId)
            {
                SetProperty(ref _selectedHousehold, value);
                return;
            }

            SetProperty(ref _selectedHousehold, value);
            _session = new UserSession
            {
                UserId = _session.UserId,
                Email = _session.Email,
                Nickname = _session.Nickname,
                TenantId = value.Id,
                TenantName = value.Name,
                IsAppAdmin = _session.IsAppAdmin
            };
            _sessions.Save(_session);
            RaisePropertyChanged(nameof(HouseholdLabel));
            ReloadItems();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public double CellWidth
    {
        get => _cellWidth;
        set
        {
            SetProperty(ref _cellWidth, value);
            RaisePropertyChanged(nameof(CellHeight));
        }
    }

    public double CellHeight => Math.Max(24, Math.Round(_cellWidth * 0.32));

    public RelayCommand CaptureCommand { get; }
    public RelayCommand ClearHouseholdCommand { get; }
    public RelayCommand SignOutCommand { get; }
    public RelayCommand OpenAdminCommand { get; }

    public event EventHandler? SignedOut;
    public event EventHandler? AdminRequested;

    private void OpenAdmin() => AdminRequested?.Invoke(this, EventArgs.Empty);

    private void CaptureFromExcel()
    {
        try
        {
            var cells = _excel.ReadHighlightedCells();
            var toStore = cells.Where(c => !string.IsNullOrWhiteSpace(c.Text)).ToList();
            if (toStore.Count == 0)
            {
                StatusMessage = "No non-empty cells in the Excel selection.";
                return;
            }

            var added = _captures.AddExcelCells(_session, toStore);
            ShowReplica(cells);
            ReloadItems();
            StatusMessage = added.Count == 1
                ? "Captured 1 cell."
                : $"Captured {added.Count} cells.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    public void DeleteItems(IEnumerable<CapturedItem> selected)
    {
        try
        {
            var ids = selected.Select(i => i.Id).ToList();
            if (ids.Count == 0)
            {
                StatusMessage = "Select one or more rows to delete.";
                return;
            }

            var deleted = _captures.DeleteItems(_session.TenantId, ids);
            ReloadItems();
            StatusMessage = deleted == 1 ? "Deleted 1 cell." : $"Deleted {deleted} cells.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void ClearHousehold()
    {
        try
        {
            var completed = _captures.CompleteHousehold(_session.TenantId, _session.UserId);
            ReloadItems();
            StatusMessage = completed == 0
                ? "Nothing left to complete."
                : $"Marked {completed} items completed.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    public void LoadFromStore()
    {
        try
        {
            ReloadHouseholds();
            ReloadItems();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void SignOut()
    {
        _sessions.Clear();
        SignedOut?.Invoke(this, EventArgs.Empty);
    }

    private void ReloadHouseholds()
    {
        Households.Clear();
        foreach (var household in _identity.GetHouseholdsForUser(_session.UserId))
        {
            Households.Add(household);
        }

        ShowHouseholdSwitcher = Households.Count > 1;
        _selectedHousehold = Households.FirstOrDefault(h => h.Id == _session.TenantId);
        RaisePropertyChanged(nameof(SelectedHousehold));
    }

    private void ReloadItems()
    {
        Items.Clear();
        foreach (var item in _captures.GetItems(_session.TenantId))
        {
            Items.Add(item);
        }

        var latest = Items.Where(i => i.ExcelRow > 0).ToList();
        if (latest.Count == 0)
        {
            ReplicaRows.Clear();
            return;
        }

        var stamp = latest.Max(i => i.CreatedAt);
        ShowReplicaFromItems(latest.Where(i => i.CreatedAt == stamp));
    }

    private void ShowReplica(IEnumerable<ExcelCellText> cells)
    {
        ReplicaRows.Clear();
        foreach (var row in ReplicaGrid.FromCells(cells))
        {
            ReplicaRows.Add(row);
        }
    }

    private void ShowReplicaFromItems(IEnumerable<CapturedItem> items)
    {
        ReplicaRows.Clear();
        foreach (var row in ReplicaGrid.FromItems(items))
        {
            ReplicaRows.Add(row);
        }
    }
}
