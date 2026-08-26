using DropCaptureList.Windows.Models;
using Microsoft.Data.SqlClient;

namespace DropCaptureList.Windows.Services;

public sealed class SqlCaptureService : ICaptureService
{
    private readonly AzureSqlConnectionFactory _connections;

    public SqlCaptureService(AzureSqlConnectionFactory connections)
    {
        _connections = connections;
    }

    public IReadOnlyList<CapturedItem> GetItems(Guid tenantId)
    {
        using var connection = _connections.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT i.ItemId, i.Text, i.CreatedByUserId, m.Nickname, i.TenantId, t.Name, i.CreatedAt, i.Source, i.ExcelAddress,
                   i.ExcelRow, i.ExcelColumn, i.IsBold, i.FontColor, i.FillColor, i.CompletedAt
            FROM dbo.Items i
            INNER JOIN dbo.Tenants t ON t.TenantId = i.TenantId
            INNER JOIN dbo.Memberships m ON m.UserId = i.CreatedByUserId AND m.TenantId = i.TenantId
            WHERE i.TenantId = @tenantId AND i.IsDeleted = 0
            ORDER BY i.CreatedAt DESC;
            """;
        command.Parameters.AddWithValue("@tenantId", tenantId);

        var list = new List<CapturedItem>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new CapturedItem
            {
                Id = reader.GetGuid(0),
                Text = reader.GetString(1),
                UserId = reader.GetGuid(2),
                Nickname = reader.GetString(3),
                TenantId = reader.GetGuid(4),
                TenantName = reader.GetString(5),
                CreatedAt = reader.GetFieldValue<DateTimeOffset>(6),
                Source = reader.GetString(7),
                ExcelAddress = reader.IsDBNull(8) ? null : reader.GetString(8),
                ExcelRow = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                ExcelColumn = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
                IsBold = !reader.IsDBNull(11) && reader.GetBoolean(11),
                FontColor = reader.IsDBNull(12) ? null : reader.GetString(12),
                FillColor = reader.IsDBNull(13) ? null : reader.GetString(13),
                CompletedAt = reader.IsDBNull(14) ? null : reader.GetFieldValue<DateTimeOffset>(14)
            });
        }

        return list;
    }

    public IReadOnlyList<CapturedItem> AddExcelCells(UserSession session, IEnumerable<ExcelCellText> cells)
    {
        var added = new List<CapturedItem>();
        var now = DateTimeOffset.Now;
        using var connection = _connections.Open();
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

            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO dbo.Items (ItemId, TenantId, Text, Source, ExcelAddress, ExcelRow, ExcelColumn, IsBold, FontColor, FillColor, CreatedByUserId, CreatedAt)
                VALUES (@id, @tenantId, @text, @source, @address, @row, @col, @bold, @font, @fill, @userId, @createdAt);
                """;
            command.Parameters.AddWithValue("@id", item.Id);
            command.Parameters.AddWithValue("@tenantId", item.TenantId);
            command.Parameters.AddWithValue("@text", item.Text);
            command.Parameters.AddWithValue("@source", item.Source);
            command.Parameters.AddWithValue("@address", (object?)item.ExcelAddress ?? DBNull.Value);
            command.Parameters.AddWithValue("@row", item.ExcelRow);
            command.Parameters.AddWithValue("@col", item.ExcelColumn);
            command.Parameters.AddWithValue("@bold", item.IsBold);
            command.Parameters.AddWithValue("@font", (object?)item.FontColor ?? DBNull.Value);
            command.Parameters.AddWithValue("@fill", (object?)item.FillColor ?? DBNull.Value);
            command.Parameters.AddWithValue("@userId", item.UserId);
            command.Parameters.AddWithValue("@createdAt", item.CreatedAt);
            command.ExecuteNonQuery();
            added.Add(item);
        }

        return added;
    }

    public int DeleteItems(Guid tenantId, IEnumerable<Guid> itemIds)
    {
        var ids = itemIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return 0;
        }

        using var connection = _connections.Open();
        var deleted = 0;
        foreach (var id in ids)
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                DELETE FROM dbo.Items
                WHERE TenantId = @tenantId AND ItemId = @id;
                """;
            command.Parameters.AddWithValue("@tenantId", tenantId);
            command.Parameters.AddWithValue("@id", id);
            deleted += command.ExecuteNonQuery();
        }

        return deleted;
    }

    public int CompleteHousehold(Guid tenantId, Guid completedByUserId)
    {
        using var connection = _connections.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.Items
            SET CompletedAt = SYSDATETIMEOFFSET(), CompletedByUserId = @userId
            WHERE TenantId = @tenantId AND CompletedAt IS NULL AND IsDeleted = 0;
            """;
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@userId", completedByUserId);
        return command.ExecuteNonQuery();
    }
}
