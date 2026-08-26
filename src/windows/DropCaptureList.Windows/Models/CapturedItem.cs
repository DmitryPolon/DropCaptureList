namespace DropCaptureList.Windows.Models;

public sealed class CapturedItem
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public bool IsCompleted => CompletedAt.HasValue;
    public string Source { get; set; } = CaptureSources.ExcelCell;
    public string? ExcelAddress { get; set; }
    public int ExcelRow { get; set; }
    public int ExcelColumn { get; set; }
    public bool IsBold { get; set; }
    public string? FontColor { get; set; }
    public string? FillColor { get; set; }
}

public static class CaptureSources
{
    public const string ExcelCell = "ExcelCell";
    public const string TextLine = "TextLine";
}
