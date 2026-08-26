using DropCaptureList.Windows.Models;

namespace DropCaptureList.Windows.Services;

public sealed class CaptureService : ICaptureService
{
    private readonly ICaptureStore _store;

    public CaptureService(ICaptureStore store)
    {
        _store = store;
    }

    public IReadOnlyList<CapturedItem> GetItems(Guid tenantId)
    {
        return _store.Load().Items
            .Where(i => i.TenantId == tenantId)
            .OrderByDescending(i => i.CreatedAt)
            .ToList();
    }

    public IReadOnlyList<CapturedItem> AddExcelCells(UserSession session, IEnumerable<ExcelCellText> cells)
    {
        var db = _store.Load();
        var added = new List<CapturedItem>();
        var now = DateTimeOffset.Now;

        foreach (var cell in cells)
        {
            if (string.IsNullOrWhiteSpace(cell.Text))
            {
                continue;
            }

            var item = new CapturedItem
            {
                Id = Guid.NewGuid(),
                Text = cell.Text,
                UserId = session.UserId,
                Nickname = session.Nickname,
                TenantId = session.TenantId,
                TenantName = session.TenantName,
                CreatedAt = now,
                Source = CaptureSources.ExcelCell,
                ExcelAddress = cell.Address,
                ExcelRow = cell.Row,
                ExcelColumn = cell.Column,
                IsBold = cell.IsBold,
                FontColor = cell.FontColor,
                FillColor = cell.FillColor
            };
            db.Items.Add(item);
            added.Add(item);
        }

        if (added.Count > 0)
        {
            _store.Save(db);
        }

        return added;
    }

    public int DeleteItems(Guid tenantId, IEnumerable<Guid> itemIds)
    {
        var ids = itemIds.ToHashSet();
        var db = _store.Load();
        var removed = db.Items.RemoveAll(i => i.TenantId == tenantId && ids.Contains(i.Id));
        if (removed > 0)
        {
            _store.Save(db);
        }

        return removed;
    }

    public int CompleteHousehold(Guid tenantId, Guid completedByUserId)
    {
        var db = _store.Load();
        var now = DateTimeOffset.Now;
        var n = 0;
        foreach (var item in db.Items.Where(i => i.TenantId == tenantId && i.CompletedAt is null))
        {
            item.CompletedAt = now;
            n++;
        }

        if (n > 0)
        {
            _store.Save(db);
        }

        return n;
    }
}
