namespace DropCaptureList.Api;

public sealed record WebSession(
    Guid UserId,
    string Email,
    string Nickname,
    string Household,
    string Motto,
    string LogoLetter);

public sealed record ListItem(
    Guid Id,
    string Text,
    string Nickname,
    DateTimeOffset CreatedAt,
    bool IsCompleted,
    string? CompletedByNickname,
    DateTimeOffset? CompletedAt,
    int ExcelRow,
    int ExcelColumn,
    bool IsBold,
    string? FontColor,
    string? FillColor);

public sealed record HouseholdBrand(string Name, string Motto, string LogoLetter);

public static class HouseholdMark
{
    public static string Letter(string name)
    {
        name = name.Trim();
        return string.IsNullOrEmpty(name) ? "?" : char.ToUpperInvariant(name[0]).ToString();
    }
}
