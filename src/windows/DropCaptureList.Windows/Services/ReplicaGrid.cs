using DropCaptureList.Windows.Models;

namespace DropCaptureList.Windows.Services;

public static class ReplicaGrid
{
    public static IReadOnlyList<ReplicaRow> FromCells(IEnumerable<ExcelCellText> cells)
    {
        var list = cells.ToList();
        if (list.Count == 0)
        {
            return [];
        }

        var minRow = list.Min(c => c.Row);
        var maxRow = list.Max(c => c.Row);
        var minCol = list.Min(c => c.Column);
        var maxCol = list.Max(c => c.Column);
        var lookup = new Dictionary<(int Row, int Column), ExcelCellText>();
        foreach (var cell in list)
        {
            lookup[(cell.Row, cell.Column)] = cell;
        }

        var rows = new List<ReplicaRow>();
        for (var row = minRow; row <= maxRow; row++)
        {
            var replicaRow = new ReplicaRow();
            for (var col = minCol; col <= maxCol; col++)
            {
                if (!lookup.TryGetValue((row, col), out var cell))
                {
                    replicaRow.Cells.Add(new ReplicaCell());
                    continue;
                }

                replicaRow.Cells.Add(new ReplicaCell
                {
                    Text = cell.Text,
                    IsBold = cell.IsBold,
                    FontColor = cell.FontColor ?? "#0F172A",
                    FillColor = cell.FillColor is null or "#FFFFFF" ? "#FFFFFF" : cell.FillColor
                });
            }

            rows.Add(replicaRow);
        }

        return rows;
    }

    public static IReadOnlyList<ReplicaRow> FromItems(IEnumerable<CapturedItem> items)
    {
        var list = items.ToList();
        if (list.Count == 0)
        {
            return [];
        }

        var minRow = list.Min(c => c.ExcelRow);
        var maxRow = list.Max(c => c.ExcelRow);
        var minCol = list.Min(c => c.ExcelColumn);
        var maxCol = list.Max(c => c.ExcelColumn);
        var lookup = new Dictionary<(int Row, int Column), CapturedItem>();
        foreach (var item in list)
        {
            lookup[(item.ExcelRow, item.ExcelColumn)] = item;
        }

        var rows = new List<ReplicaRow>();
        for (var row = minRow; row <= maxRow; row++)
        {
            var replicaRow = new ReplicaRow();
            for (var col = minCol; col <= maxCol; col++)
            {
                if (!lookup.TryGetValue((row, col), out var item))
                {
                    replicaRow.Cells.Add(new ReplicaCell());
                    continue;
                }

                replicaRow.Cells.Add(new ReplicaCell
                {
                    Text = item.Text,
                    IsBold = item.IsCompleted ? false : item.IsBold,
                    FontColor = item.IsCompleted ? "#94A3B8" : item.FontColor ?? "#0F172A",
                    FillColor = item.IsCompleted ? "#E2E8F0" : item.FillColor is null or "#FFFFFF" ? "#FFFFFF" : item.FillColor,
                    IsCompleted = item.IsCompleted
                });
            }

            rows.Add(replicaRow);
        }

        return rows;
    }
}
