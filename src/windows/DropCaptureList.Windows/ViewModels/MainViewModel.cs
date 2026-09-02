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
    private readonly HashSet<Guid> _persistedIds = [];
    private UserSession _session;
    private LocalTenant? _selectedHousehold;
    private string _statusMessage = "Capture adds cells locally. Save writes the database. Refresh loads the live list (completed items stay in SQL and drop off here). Double-click a cell to edit.";
    private bool _showHouseholdSwitcher;
    private ReplicaCell? _editingCell;
    private string _textBeforeEdit = string.Empty;
    private bool _suppressHouseholdBinding;
    private bool _ignoreLostFocus;
    private int _lastPurgeCount;

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
        SaveCommand = new RelayCommand(SaveToDatabase);
        RefreshCommand = new RelayCommand(RefreshFromDatabase);
        ClearHouseholdCommand = new RelayCommand(ClearHousehold);
        SignOutCommand = new RelayCommand(SignOut);
        OpenAdminCommand = new RelayCommand(OpenAdmin, () => session.IsAppAdmin);

        Households = new ObservableCollection<LocalTenant>();
        Items = new ObservableCollection<CapturedItem>();
        ReplicaRows = new ObservableCollection<ReplicaRow>();
    }

    public IIdentityService Identity => _identity;

    public ICaptureService Captures => _captures;

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
            if (_suppressHouseholdBinding || value is null || value.Id == _selectedHousehold?.Id)
            {
                return;
            }

            var switched = value.Id != _session.TenantId;
            _selectedHousehold = value;
            RaisePropertyChanged();
            if (!switched)
            {
                return;
            }

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
            RefreshFromDatabase();
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
    public RelayCommand SaveCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand ClearHouseholdCommand { get; }
    public RelayCommand SignOutCommand { get; }
    public RelayCommand OpenAdminCommand { get; }

    public event EventHandler? SignedOut;
    public event EventHandler? AdminRequested;

    private void OpenAdmin() => AdminRequested?.Invoke(this, EventArgs.Empty);

    public bool BeginEdit(ReplicaCell cell)
    {
        if (cell.Item is { IsCompleted: true })
        {
            return false;
        }

        if (_editingCell == cell && cell.IsEditing)
        {
            return true;
        }

        EndEdit();
        if (cell.Item is null)
        {
            var item = new CapturedItem
            {
                Id = Guid.NewGuid(),
                Text = string.Empty,
                UserId = _session.UserId,
                Nickname = _session.Nickname,
                TenantId = _session.TenantId,
                TenantName = _session.TenantName,
                CreatedAt = DateTimeOffset.Now,
                Source = CaptureSources.ExcelCell,
                ExcelRow = cell.Row,
                ExcelColumn = cell.Column
            };
            cell.Item = item;
            Items.Add(item);
        }

        _ignoreLostFocus = true;
        _editingCell = cell;
        _textBeforeEdit = cell.Text;
        cell.IsEditing = true;
        return true;
    }

    public void EndEdit(bool cancel = false)
    {
        if (_editingCell is null)
        {
            return;
        }

        if (cancel)
        {
            _editingCell.Text = _textBeforeEdit;
        }

        _editingCell.IsEditing = false;
        _editingCell = null;
        _ignoreLostFocus = false;
    }

    public void EndEditFromFocus()
    {
        if (_ignoreLostFocus)
        {
            return;
        }

        EndEdit();
    }

    public void AllowLostFocus() => _ignoreLostFocus = false;

    private void CaptureFromExcel()
    {
        try
        {
            EndEdit();
            var cells = _excel.ReadHighlightedCells();
            var toStore = cells.Where(c => !string.IsNullOrWhiteSpace(c.Text)).ToList();
            if (toStore.Count == 0)
            {
                StatusMessage = "No non-empty cells in the Excel selection.";
                return;
            }

            var texts = Items
                .Select(i => i.Text.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var occupied = Items
                .Select(i => (i.ExcelRow, i.ExcelColumn))
                .ToHashSet();

            var added = 0;
            var skipped = 0;
            var now = DateTimeOffset.Now;
            foreach (var cell in toStore)
            {
                var text = cell.Text.Trim();
                if (!texts.Add(text) || occupied.Contains((cell.Row, cell.Column)))
                {
                    skipped++;
                    continue;
                }

                occupied.Add((cell.Row, cell.Column));
                Items.Add(new CapturedItem
                {
                    Id = Guid.NewGuid(),
                    Text = text,
                    UserId = _session.UserId,
                    Nickname = _session.Nickname,
                    TenantId = _session.TenantId,
                    TenantName = _session.TenantName,
                    CreatedAt = now,
                    Source = CaptureSources.ExcelCell,
                    ExcelAddress = cell.Address,
                    ExcelRow = cell.Row,
                    ExcelColumn = cell.Column,
                    IsBold = cell.IsBold,
                    FontColor = cell.FontColor,
                    FillColor = cell.FillColor
                });
                added++;
            }

            RebuildReplica();
            StatusMessage = skipped == 0
                ? (added == 1 ? "Added 1 cell. Save to write the database." : $"Added {added} cells. Save to write the database.")
                : $"Added {added} cells, skipped {skipped} duplicate(s). Save to write the database.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void SaveToDatabase()
    {
        try
        {
            EndEdit();
            var result = _captures.SaveItems(_session, Items.ToList());
            RefreshFromDatabase();
            var extra = _lastPurgeCount > 0 ? $" Removed {_lastPurgeCount} completed item(s) older than a month." : "";
            StatusMessage = result.DuplicatesSkipped == 0
                ? $"Saved {result.Inserted} new, {result.Updated} edited.{extra}"
                : $"Saved {result.Inserted} new, {result.Updated} edited, skipped {result.DuplicatesSkipped} duplicate(s).{extra}";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void RefreshFromDatabase()
    {
        try
        {
            EndEdit();
            var purged = MaybePurgeOldCompleted();
            _lastPurgeCount = purged;
            Items.Clear();
            _persistedIds.Clear();
            foreach (var item in _captures.GetItems(_session.TenantId))
            {
                Items.Add(item);
                _persistedIds.Add(item.Id);
            }

            RebuildReplica();
            var loaded = Items.Count == 0
                ? "Live list is empty. Completed items are not shown."
                : $"Loaded {Items.Count} live items. Completed rows from the database are not shown.";
            StatusMessage = purged > 0
                ? $"Removed {purged} completed item(s) older than a month. {loaded}"
                : loaded;
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
            EndEdit();
            var selectedItems = selected.ToList();
            if (selectedItems.Count == 0)
            {
                StatusMessage = "Select one or more rows to delete.";
                return;
            }

            var persisted = selectedItems.Where(i => _persistedIds.Contains(i.Id)).Select(i => i.Id).ToList();
            if (persisted.Count > 0)
            {
                _captures.DeleteItems(_session.TenantId, persisted);
            }

            var remove = selectedItems.Select(i => i.Id).ToHashSet();
            for (var i = Items.Count - 1; i >= 0; i--)
            {
                if (remove.Contains(Items[i].Id))
                {
                    _persistedIds.Remove(Items[i].Id);
                    Items.RemoveAt(i);
                }
            }

            RebuildReplica();
            StatusMessage = selectedItems.Count == 1 ? "Removed 1 cell." : $"Removed {selectedItems.Count} cells.";
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
            EndEdit();
            var completed = _captures.CompleteHousehold(_session.TenantId, _session.UserId);
            RefreshFromDatabase();
            StatusMessage = completed == 0
                ? "Nothing left to complete."
                : $"Marked {completed} items completed. Refresh dropped them from this list.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private int MaybePurgeOldCompleted()
    {
        var day = DateTime.Now.Day;
        if (day is not (1 or 15))
        {
            return 0;
        }

        return _captures.PurgeCompletedOlderThanOneMonth();
    }

    public void LoadFromStore()
    {
        try
        {
            ReloadHouseholds();
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
        _suppressHouseholdBinding = true;
        try
        {
            Households.Clear();
            foreach (var household in _identity.GetHouseholdsForUser(_session.UserId))
            {
                Households.Add(household);
            }

            ShowHouseholdSwitcher = Households.Count > 1;
            _selectedHousehold = Households.FirstOrDefault(h => h.Id == _session.TenantId);
            RaisePropertyChanged(nameof(SelectedHousehold));
            RaisePropertyChanged(nameof(HouseholdLabel));
        }
        finally
        {
            _suppressHouseholdBinding = false;
        }
    }

    private void RebuildReplica()
    {
        ReplicaRows.Clear();
        foreach (var row in ReplicaGrid.FromItems(Items))
        {
            ReplicaRows.Add(row);
        }
    }
}
