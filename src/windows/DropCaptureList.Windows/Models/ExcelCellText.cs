using DropCaptureList.Windows.Helpers;

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

public sealed class ReplicaCell : ViewModelBase
{
    private CapturedItem? _item;
    private bool _isEditing;

    public int Row { get; init; }
    public int Column { get; init; }

    public CapturedItem? Item
    {
        get => _item;
        set
        {
            if (ReferenceEquals(_item, value))
            {
                return;
            }

            _item = value;
            RaisePropertyChanged();
            RaisePropertyChanged(nameof(Text));
            RaisePropertyChanged(nameof(CanEdit));
            RaisePropertyChanged(nameof(IsBold));
            RaisePropertyChanged(nameof(FontColor));
            RaisePropertyChanged(nameof(FillColor));
            RaisePropertyChanged(nameof(IsCompleted));
        }
    }

    public bool CanEdit => Item is not { IsCompleted: true };

    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    public string Text
    {
        get => Item?.Text ?? string.Empty;
        set
        {
            if (Item is null)
            {
                return;
            }

            if (Item.Text == value)
            {
                return;
            }

            Item.Text = value;
            RaisePropertyChanged();
        }
    }

    public bool IsBold => Item is { IsCompleted: false, IsBold: true };

    public string FontColor => Item is null
        ? "#0F172A"
        : Item.IsCompleted ? "#94A3B8" : Item.FontColor ?? "#0F172A";

    public string FillColor => Item is null
        ? "#FFFFFF"
        : Item.IsCompleted ? "#E2E8F0" : Item.FillColor is null or "#FFFFFF" ? "#FFFFFF" : Item.FillColor;

    public bool IsCompleted => Item?.IsCompleted == true;
}

public sealed class ReplicaRow
{
    public List<ReplicaCell> Cells { get; init; } = [];
}
