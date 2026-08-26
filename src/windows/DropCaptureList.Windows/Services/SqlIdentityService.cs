using DropCaptureList.Windows.Models;
using Microsoft.Data.SqlClient;

namespace DropCaptureList.Windows.Services;

public sealed class SqlIdentityService : IIdentityService
{
    private readonly AzureSqlConnectionFactory _connections;

    public SqlIdentityService(AzureSqlConnectionFactory connections)
    {
        _connections = connections;
    }

    public UserSession SignIn(string emailOrLogin, string householdName)
    {
        emailOrLogin = emailOrLogin.Trim();
        householdName = householdName.Trim();
        if (string.IsNullOrWhiteSpace(emailOrLogin))
        {
            throw new InvalidOperationException("Enter your email.");
        }

        if (string.IsNullOrWhiteSpace(householdName))
        {
            throw new InvalidOperationException("Enter a household name.");
        }

        using var connection = _connections.Open();
        var userId = FindUserId(connection, emailOrLogin);
        if (userId is null)
        {
            throw new InvalidOperationException(
                "Unknown email. Sign in with the email stored for this user (check spelling). Household name is separate from nickname.");
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT u.UserId, u.LoginName, u.Email, u.IsAppAdmin, t.TenantId, t.Name, m.Nickname
            FROM dbo.Users u
            INNER JOIN dbo.Memberships m ON m.UserId = u.UserId
            INNER JOIN dbo.Tenants t ON t.TenantId = m.TenantId
            WHERE u.UserId = @userId
              AND LOWER(t.Name) = LOWER(@household);
            """;
        command.Parameters.AddWithValue("@userId", userId.Value);
        command.Parameters.AddWithValue("@household", householdName);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException(
                "That email is registered, but not in this household. Use the household name from the list (not the nickname).");
        }

        return new UserSession
        {
            UserId = reader.GetGuid(0),
            Email = reader.IsDBNull(2) ? reader.GetString(1) : reader.GetString(2),
            Nickname = reader.GetString(6),
            TenantId = reader.GetGuid(4),
            TenantName = reader.GetString(5),
            IsAppAdmin = reader.GetBoolean(3)
        };
    }

    private static Guid? FindUserId(SqlConnection connection, string emailOrLogin)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (1) UserId
            FROM dbo.Users
            WHERE LOWER(ISNULL(Email, N'')) = LOWER(@login)
               OR LOWER(LoginName) = LOWER(@login);
            """;
        command.Parameters.AddWithValue("@login", emailOrLogin);
        var found = command.ExecuteScalar();
        return found is Guid id ? id : found is null ? null : (Guid)found;
    }

    public IReadOnlyList<LocalTenant> GetHouseholdsForUser(Guid userId)
    {
        using var connection = _connections.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.TenantId, t.Name, t.Motto
            FROM dbo.Tenants t
            INNER JOIN dbo.Memberships m ON m.TenantId = t.TenantId
            WHERE m.UserId = @userId
            ORDER BY t.Name;
            """;
        command.Parameters.AddWithValue("@userId", userId);

        var list = new List<LocalTenant>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new LocalTenant
            {
                Id = reader.GetGuid(0),
                Name = reader.GetString(1),
                Motto = reader.IsDBNull(2) ? string.Empty : reader.GetString(2)
            });
        }

        return list;
    }

    public IReadOnlyList<string> KnownHouseholds()
    {
        using var connection = _connections.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Name FROM dbo.Tenants ORDER BY Name;";
        var list = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(reader.GetString(0));
        }

        return list;
    }

    public IReadOnlyList<AdminUserRow> ListUsers()
    {
        using var connection = _connections.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT u.UserId, u.LoginName, ISNULL(u.Email, N''), u.IsAppAdmin,
                   STRING_AGG(t.Name, N', ') WITHIN GROUP (ORDER BY t.Name)
            FROM dbo.Users u
            LEFT JOIN dbo.Memberships m ON m.UserId = u.UserId
            LEFT JOIN dbo.Tenants t ON t.TenantId = m.TenantId
            GROUP BY u.UserId, u.LoginName, u.Email, u.IsAppAdmin
            ORDER BY u.LoginName;
            """;

        var list = new List<AdminUserRow>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new AdminUserRow
            {
                UserId = reader.GetGuid(0),
                LoginName = reader.GetString(1),
                Email = reader.GetString(2),
                IsAppAdmin = reader.GetBoolean(3),
                Households = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
            });
        }

        return list;
    }

    public void AddUser(string email, string loginName, string householdName, string nickname, bool isAppAdmin)
    {
        email = email.Trim();
        loginName = string.IsNullOrWhiteSpace(loginName) ? nickname.Trim() : loginName.Trim();
        nickname = nickname.Trim();
        householdName = householdName.Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(nickname) || string.IsNullOrWhiteSpace(householdName))
        {
            throw new InvalidOperationException("Email, nickname, and household are required.");
        }

        using var connection = _connections.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            Guid tenantId;
            using (var findTenant = connection.CreateCommand())
            {
                findTenant.Transaction = tx;
                findTenant.CommandText = "SELECT TenantId FROM dbo.Tenants WHERE Name = @name;";
                findTenant.Parameters.AddWithValue("@name", householdName);
                var found = findTenant.ExecuteScalar();
                if (found is null)
                {
                    throw new InvalidOperationException("Unknown household. Create it first.");
                }

                tenantId = (Guid)found;
            }

            var userId = Guid.NewGuid();
            using (var insertUser = connection.CreateCommand())
            {
                insertUser.Transaction = tx;
                insertUser.CommandText = """
                    INSERT INTO dbo.Users (UserId, LoginName, Email, IsAppAdmin)
                    VALUES (@id, @login, @email, @admin);
                    """;
                insertUser.Parameters.AddWithValue("@id", userId);
                insertUser.Parameters.AddWithValue("@login", loginName);
                insertUser.Parameters.AddWithValue("@email", email);
                insertUser.Parameters.AddWithValue("@admin", isAppAdmin);
                insertUser.ExecuteNonQuery();
            }

            using (var insertMember = connection.CreateCommand())
            {
                insertMember.Transaction = tx;
                insertMember.CommandText = """
                    INSERT INTO dbo.Memberships (UserId, TenantId, Nickname, Role)
                    VALUES (@userId, @tenantId, @nickname, N'Member');
                    """;
                insertMember.Parameters.AddWithValue("@userId", userId);
                insertMember.Parameters.AddWithValue("@tenantId", tenantId);
                insertMember.Parameters.AddWithValue("@nickname", nickname);
                insertMember.ExecuteNonQuery();
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public void CreateHousehold(string name, string? motto = null)
    {
        name = name.Trim();
        motto = NormalizeMotto(motto);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Enter a household name.");
        }

        using var connection = _connections.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO dbo.Tenants (TenantId, Name, Motto) VALUES (NEWID(), @name, @motto);";
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@motto", string.IsNullOrEmpty(motto) ? DBNull.Value : motto);
        command.ExecuteNonQuery();
    }

    public void SetHouseholdMotto(string householdName, string motto)
    {
        householdName = householdName.Trim();
        motto = NormalizeMotto(motto);
        if (string.IsNullOrWhiteSpace(householdName))
        {
            throw new InvalidOperationException("Enter the household name.");
        }

        using var connection = _connections.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.Tenants
            SET Motto = @motto
            WHERE LOWER(Name) = LOWER(@name);
            """;
        command.Parameters.AddWithValue("@name", householdName);
        command.Parameters.AddWithValue("@motto", string.IsNullOrEmpty(motto) ? DBNull.Value : motto);
        if (command.ExecuteNonQuery() == 0)
        {
            throw new InvalidOperationException("Unknown household.");
        }
    }

    private static string NormalizeMotto(string? motto)
    {
        motto = motto?.Trim() ?? string.Empty;
        return motto.Length <= 120 ? motto : motto[..120];
    }

    public void RemoveFromHousehold(Guid userId, string householdName)
    {
        householdName = householdName.Trim();
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(householdName))
        {
            throw new InvalidOperationException("Select a user and enter the household to remove them from.");
        }

        using var connection = _connections.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE m
            FROM dbo.Memberships m
            INNER JOIN dbo.Tenants t ON t.TenantId = m.TenantId
            WHERE m.UserId = @userId AND LOWER(t.Name) = LOWER(@household);
            """;
        command.Parameters.AddWithValue("@userId", userId);
        command.Parameters.AddWithValue("@household", householdName);
        if (command.ExecuteNonQuery() == 0)
        {
            throw new InvalidOperationException("That user is not in this household.");
        }
    }
}
