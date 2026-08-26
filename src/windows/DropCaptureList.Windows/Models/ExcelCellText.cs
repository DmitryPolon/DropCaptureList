namespace DropCaptureList.Windows.Models;

public sealed class ExcelCellText
{
    public required string Text { get; init; }
    public required string Address { get; init; }
    public int Row { get; init; }
    public int Column { get; init; }
    public bool IsBold { get; init; }
    public string? FontColor { get; init; }
    public string? FillColor { get; init; }
}

public sealed class ReplicaCell
{
    public string Text { get; init; } = string.Empty;
    public bool IsBold { get; init; }
    public string FontColor { get; init; } = "#0F172A";
    public string FillColor { get; init; } = "#FFFFFF";
    public bool IsCompleted { get; init; }
}

public sealed class ReplicaRow
{
    public List<ReplicaCell> Cells { get; init; } = [];
}
