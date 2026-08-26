using System.Runtime.InteropServices;
using DropCaptureList.Windows.Models;

namespace DropCaptureList.Windows.Services;

public sealed class ExcelSelectionCapture
{
    private const int MaxCells = 5_000;

    public IReadOnlyList<ExcelCellText> ReadHighlightedCells()
    {
        dynamic excel = RunningComObject.Get("Excel.Application");
        dynamic? selection = excel.Selection
            ?? throw new InvalidOperationException("Nothing is selected in Excel.");

        if (!TryGetWorksheet(selection, out dynamic? worksheet) || worksheet is null)
        {
            throw new InvalidOperationException("Select worksheet cells in Excel (not a chart or shape), then capture.");
        }

        dynamic used = worksheet!.UsedRange;
        dynamic? target = excel.Intersect(selection, used);
        if (target is null)
        {
            return [];
        }

        var results = new List<ExcelCellText>();
        dynamic areas = target.Areas;
        var areaCount = (int)areas.Count;
        for (var areaIndex = 1; areaIndex <= areaCount; areaIndex++)
        {
            dynamic area = areas.Item(areaIndex);
            dynamic cells = area.Cells;
            var cellCount = (int)cells.Count;
            if (cellCount > MaxCells)
            {
                throw new InvalidOperationException("The selection is too large. Highlight a smaller range.");
            }

            for (var i = 1; i <= cellCount; i++)
            {
                dynamic cell = cells.Item(i);
                if (IsSecondaryMergedCell(cell))
                {
                    continue;
                }

                results.Add(ReadCell(cell));
            }
        }

        return results;
    }

    private static ExcelCellText ReadCell(dynamic cell)
    {
        var text = Convert.ToString(cell.Text) ?? string.Empty;
        var address = Convert.ToString(cell.Address) ?? string.Empty;
        var row = (int)cell.Row;
        var column = (int)cell.Column;
        var bold = false;
        try
        {
            bold = cell.Font.Bold is bool b && b;
        }
        catch (COMException)
        {
        }

        return new ExcelCellText
        {
            Text = text,
            Address = address,
            Row = row,
            Column = column,
            IsBold = bold,
            FontColor = ToHex(TryColor(() => cell.Font.Color)),
            FillColor = ToHex(TryColor(() => cell.Interior.Color))
        };
    }

    private static int? TryColor(Func<dynamic> read)
    {
        try
        {
            return Convert.ToInt32(read());
        }
        catch
        {
            return null;
        }
    }

    private static string? ToHex(int? bgr)
    {
        if (bgr is null)
        {
            return null;
        }

        var n = bgr.Value;
        var r = n & 0xFF;
        var g = (n >> 8) & 0xFF;
        var b = (n >> 16) & 0xFF;
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static bool TryGetWorksheet(dynamic selection, out dynamic? worksheet)
    {
        try
        {
            worksheet = selection.Worksheet;
            return worksheet is not null;
        }
        catch (COMException)
        {
            worksheet = null;
            return false;
        }
        catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            worksheet = null;
            return false;
        }
    }

    private static bool IsSecondaryMergedCell(dynamic cell)
    {
        try
        {
            if (!(bool)cell.MergeCells)
            {
                return false;
            }

            dynamic merge = cell.MergeArea;
            return (int)cell.Row != (int)merge.Row || (int)cell.Column != (int)merge.Column;
        }
        catch (COMException)
        {
            return false;
        }
    }
}
