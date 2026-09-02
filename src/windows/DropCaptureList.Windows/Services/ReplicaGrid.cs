using DropCaptureList.Windows.Models;

namespace DropCaptureList.Windows.Services;

public static class ReplicaGrid
{
    public static IReadOnlyList<ReplicaRow> FromItems(IEnumerable<CapturedItem> items)
    {
        var list = items.Where(i => i.ExcelRow > 0 || i.ExcelColumn > 0).ToList();
        if (list.Count == 0)
        {
            list = items.ToList();
        }

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
                lookup.TryGetValue((row, col), out var item);
                replicaRow.Cells.Add(new ReplicaCell
                {
                    Item = item,
                    Row = row,
                    Column = col
                });
            }

            rows.Add(replicaRow);
        }

        return rows;
    }
}
