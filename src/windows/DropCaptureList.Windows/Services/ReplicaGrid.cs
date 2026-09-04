using DropCaptureList.Windows.Models;

namespace DropCaptureList.Windows.Services;

public static class ReplicaGrid
{
    public static IReadOnlyList<ReplicaRow> FromItems(IEnumerable<CapturedItem> items)
    {
        var all = items.ToList();
        var excel = all.Where(i => i.ExcelRow > 0 && i.ExcelColumn > 0).ToList();
        var leftover = all.Where(i => i.ExcelRow <= 0 || i.ExcelColumn <= 0)
            .OrderByDescending(i => i.CreatedAt)
            .ToList();

        var rows = new List<ReplicaRow>();
        if (excel.Count > 0)
        {
            rows.AddRange(BuildSheet(excel));
        }

        foreach (var item in leftover)
        {
            rows.Add(new ReplicaRow
            {
                Cells =
                [
                    new ReplicaCell
                    {
                        Item = item,
                        Row = item.ExcelRow,
                        Column = item.ExcelColumn
                    }
                ]
            });
        }

        return rows;
    }

    private static IEnumerable<ReplicaRow> BuildSheet(List<CapturedItem> list)
    {
        var minRow = list.Min(c => c.ExcelRow);
        var maxRow = list.Max(c => c.ExcelRow);
        var minCol = list.Min(c => c.ExcelColumn);
        var maxCol = list.Max(c => c.ExcelColumn);
        var lookup = new Dictionary<(int Row, int Column), CapturedItem>();
        foreach (var item in list)
        {
            lookup[(item.ExcelRow, item.ExcelColumn)] = item;
        }

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

            yield return replicaRow;
        }
    }
}
