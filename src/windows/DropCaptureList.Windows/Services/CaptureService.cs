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
            .Where(i => i.TenantId == tenantId && i.CompletedAt is null)
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

    public CaptureSaveResult SaveItems(UserSession session, IReadOnlyList<CapturedItem> items)
    {
        var db = _store.Load();
        var live = db.Items
            .Where(i => i.TenantId == session.TenantId && i.CompletedAt is null)
            .ToList();
        var byId = live.ToDictionary(i => i.Id);
        var texts = live
            .Select(i => i.Text.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var inserted = 0;
        var updated = 0;
        var skipped = 0;
        var now = DateTimeOffset.Now;

        foreach (var item in items)
        {
            var text = item.Text.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (byId.TryGetValue(item.Id, out var existing))
            {
                if (string.Equals(existing.Text.Trim(), text, StringComparison.Ordinal))
                {
                    continue;
                }

                existing.Text = text;
                updated++;
                continue;
            }

            if (!texts.Add(text))
            {
                skipped++;
                continue;
            }

            db.Items.Add(new CapturedItem
            {
                Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id,
                Text = text,
                UserId = session.UserId,
                Nickname = session.Nickname,
                TenantId = session.TenantId,
                TenantName = session.TenantName,
                CreatedAt = item.CreatedAt == default ? now : item.CreatedAt,
                Source = string.IsNullOrWhiteSpace(item.Source) ? CaptureSources.ExcelCell : item.Source,
                ExcelAddress = item.ExcelAddress,
                ExcelRow = item.ExcelRow,
                ExcelColumn = item.ExcelColumn,
                IsBold = item.IsBold,
                FontColor = item.FontColor,
                FillColor = item.FillColor
            });
            inserted++;
        }

        if (inserted > 0 || updated > 0)
        {
            _store.Save(db);
        }

        return new CaptureSaveResult
        {
            Inserted = inserted,
            Updated = updated,
            DuplicatesSkipped = skipped
        };
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

    public int PurgeCompletedOlderThanOneMonth()
    {
        var cutoff = DateTimeOffset.Now.AddMonths(-1);
        var db = _store.Load();
        var removed = db.Items.RemoveAll(i => i.CompletedAt is { } done && done < cutoff);
        if (removed > 0)
        {
            _store.Save(db);
        }

        return removed;
    }
}
