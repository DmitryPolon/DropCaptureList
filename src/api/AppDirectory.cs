using Microsoft.Data.SqlClient;

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

public sealed class AppDirectory
{
    private readonly AzureSql _sql;

    public AppDirectory(AzureSql sql)
    {
        _sql = sql;
    }

    public WebSession SignIn(string email, string household)
    {
        email = email.Trim();
        household = household.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Enter your email.");
        }

        if (string.IsNullOrWhiteSpace(household))
        {
            throw new InvalidOperationException("Enter a household name.");
        }

        using var connection = _sql.Open();
        using var find = connection.CreateCommand();
        find.CommandText = """
            SELECT TOP (1) UserId
            FROM dbo.Users
            WHERE LOWER(ISNULL(Email, N'')) = LOWER(@login)
               OR LOWER(LoginName) = LOWER(@login);
            """;
        find.Parameters.AddWithValue("@login", email);
        var found = find.ExecuteScalar();
        if (found is not Guid userId)
        {
            throw new InvalidOperationException(
                "Unknown email. Sign in with the email stored for this user (check spelling). Household name is separate from nickname.");
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT u.UserId, ISNULL(u.Email, u.LoginName), m.Nickname, t.Name, ISNULL(t.Motto, N'')
            FROM dbo.Users u
            INNER JOIN dbo.Memberships m ON m.UserId = u.UserId
            INNER JOIN dbo.Tenants t ON t.TenantId = m.TenantId
            WHERE u.UserId = @userId
              AND LOWER(t.Name) = LOWER(@household);
            """;
        command.Parameters.AddWithValue("@userId", userId);
        command.Parameters.AddWithValue("@household", household);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException(
                "That email is registered, but not in this household. Use the household name (not the nickname).");
        }

        var name = reader.GetString(3);
        return new WebSession(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            name,
            reader.GetString(4),
            Letter(name));
    }

    public IReadOnlyList<ListItem> ListItems(string household)
    {
        household = household.Trim();
        using var connection = _sql.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT i.ItemId, i.Text, creator.Nickname, i.CreatedAt, i.CompletedAt, completer.Nickname,
                   i.ExcelRow, i.ExcelColumn, i.IsBold, i.FontColor, i.FillColor
            FROM dbo.Items i
            INNER JOIN dbo.Tenants t ON t.TenantId = i.TenantId
            INNER JOIN dbo.Memberships creator
                ON creator.UserId = i.CreatedByUserId AND creator.TenantId = i.TenantId
            LEFT JOIN dbo.Memberships completer
                ON completer.UserId = i.CompletedByUserId AND completer.TenantId = i.TenantId
            WHERE LOWER(t.Name) = LOWER(@household) AND i.IsDeleted = 0
            ORDER BY CASE WHEN i.CompletedAt IS NULL THEN 0 ELSE 1 END, i.CreatedAt DESC;
            """;
        command.Parameters.AddWithValue("@household", household);

        var list = new List<ListItem>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var completedAt = reader.IsDBNull(4) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(4);
            list.Add(new ListItem(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3),
                completedAt.HasValue,
                reader.IsDBNull(5) ? null : reader.GetString(5),
                completedAt,
                reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                !reader.IsDBNull(8) && reader.GetBoolean(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10)));
        }

        return list;
    }

    public void ToggleComplete(string email, string household, Guid itemId)
    {
        var session = SignIn(email, household);
        using var connection = _sql.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.Items
            SET
                CompletedAt = CASE WHEN CompletedAt IS NULL THEN SYSDATETIMEOFFSET() ELSE NULL END,
                CompletedByUserId = CASE WHEN CompletedAt IS NULL THEN @userId ELSE NULL END
            WHERE ItemId = @id
              AND TenantId = (SELECT TenantId FROM dbo.Tenants WHERE LOWER(Name) = LOWER(@household))
              AND IsDeleted = 0;
            """;
        command.Parameters.AddWithValue("@userId", session.UserId);
        command.Parameters.AddWithValue("@id", itemId);
        command.Parameters.AddWithValue("@household", household);
        if (command.ExecuteNonQuery() == 0)
        {
            throw new InvalidOperationException("That item is not on this household list.");
        }
    }

    public void SoftDelete(string email, string household, Guid itemId)
    {
        SignIn(email, household);
        using var connection = _sql.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.Items
            SET IsDeleted = 1
            WHERE ItemId = @id
              AND TenantId = (SELECT TenantId FROM dbo.Tenants WHERE LOWER(Name) = LOWER(@household))
              AND IsDeleted = 0;
            """;
        command.Parameters.AddWithValue("@id", itemId);
        command.Parameters.AddWithValue("@household", household);
        if (command.ExecuteNonQuery() == 0)
        {
            throw new InvalidOperationException("That item is not on this household list.");
        }
    }

    public int ClearCompleted(string email, string household)
    {
        SignIn(email, household);
        using var connection = _sql.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.Items
            SET IsDeleted = 1
            WHERE TenantId = (SELECT TenantId FROM dbo.Tenants WHERE LOWER(Name) = LOWER(@household))
              AND CompletedAt IS NOT NULL
              AND IsDeleted = 0;
            """;
        command.Parameters.AddWithValue("@household", household);
        return command.ExecuteNonQuery();
    }

    public void AddTextItem(string email, string household, string text)
    {
        text = text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Enter a task.");
        }

        if (text.Length > 500)
        {
            throw new InvalidOperationException("Keep the task under 500 characters.");
        }

        var session = SignIn(email, household);
        using var connection = _sql.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.Items (ItemId, TenantId, Text, Source, CreatedByUserId, CreatedAt)
            SELECT NEWID(), t.TenantId, @text, N'TextLine', @userId, SYSDATETIMEOFFSET()
            FROM dbo.Tenants t
            WHERE LOWER(t.Name) = LOWER(@household);
            """;
        command.Parameters.AddWithValue("@text", text);
        command.Parameters.AddWithValue("@userId", session.UserId);
        command.Parameters.AddWithValue("@household", household);
        if (command.ExecuteNonQuery() == 0)
        {
            throw new InvalidOperationException("Could not add the task.");
        }
    }

    internal static string Letter(string name)
    {
        name = name.Trim();
        return string.IsNullOrEmpty(name) ? "?" : char.ToUpperInvariant(name[0]).ToString();
    }
}
