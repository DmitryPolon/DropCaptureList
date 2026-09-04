namespace DropCaptureList.Api;

public sealed class StoreFront
{
    private readonly StorageMode _mode;
    private readonly AppDirectory _sql;
    private readonly FileDirectory _files;
    private readonly Households _sqlHouseholds;
    private readonly AzureSql _azureSql;

    public StoreFront(StorageMode mode, AppDirectory sql, FileDirectory files, Households sqlHouseholds, AzureSql azureSql)
    {
        _mode = mode;
        _sql = sql;
        _files = files;
        _sqlHouseholds = sqlHouseholds;
        _azureSql = azureSql;
    }

    public StorageKind Kind => _mode.Kind;

    public bool IsFile => _mode.IsFile;

    public WebSession SignIn(string email, string household)
    {
        return _mode.IsFile ? _files.SignIn(email, household) : _sql.SignIn(email, household);
    }

    public bool IsAppAdmin(string email)
    {
        if (_mode.IsFile)
        {
            return _files.IsAppAdmin(email);
        }

        try
        {
            using var connection = _azureSql.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT TOP (1) IsAppAdmin
                FROM dbo.Users
                WHERE LOWER(ISNULL(Email, N'')) = LOWER(@login) OR LOWER(LoginName) = LOWER(@login);
                """;
            command.Parameters.AddWithValue("@login", email.Trim());
            return command.ExecuteScalar() is true;
        }
        catch
        {
            return false;
        }
    }

    public IReadOnlyList<HouseholdBrand> ListHouseholds()
    {
        return _mode.IsFile ? _files.ListHouseholds() : _sqlHouseholds.List();
    }

    public IReadOnlyList<ListItem> ListItems(string household)
    {
        return _mode.IsFile ? _files.ListItems(household) : _sql.ListItems(household);
    }

    public void AddTextItem(string email, string household, string text)
    {
        if (_mode.IsFile)
        {
            _files.AddTextItem(email, household, text);
        }
        else
        {
            _sql.AddTextItem(email, household, text);
        }
    }

    public void ToggleComplete(string email, string household, Guid itemId)
    {
        if (_mode.IsFile)
        {
            _files.CompleteItem(email, household, itemId);
        }
        else
        {
            _sql.ToggleComplete(email, household, itemId);
        }
    }

    public void RemoveItem(string email, string household, Guid itemId)
    {
        if (_mode.IsFile)
        {
            _files.RemoveItem(email, household, itemId);
        }
        else
        {
            _sql.SoftDelete(email, household, itemId);
        }
    }

    public int ClearCompleted(string email, string household)
    {
        return _mode.IsFile ? _files.ClearAll(email, household) : _sql.ClearCompleted(email, household);
    }

    public int ClearAll(string email, string household)
    {
        return _mode.IsFile ? _files.ClearAll(email, household) : _sql.ClearCompleted(email, household);
    }

    public void UpsertItems(string email, string household, IEnumerable<FileItem> items)
    {
        if (!_mode.IsFile)
        {
            throw new InvalidOperationException("Bulk file save is only used in File mode.");
        }

        _files.UpsertItems(email, household, items);
    }

    public void SetMode(StorageKind kind, string adminEmail)
    {
        if (!IsAppAdmin(adminEmail))
        {
            throw new InvalidOperationException("Only an app admin can switch Azure / File.");
        }

        if (kind == StorageKind.File && !_files.HasUsers())
        {
            _sql.ExportLiveTo(_files);
        }

        _mode.Set(kind);
    }

    public IReadOnlyList<AdminUserDto> ListUsers()
    {
        return _mode.IsFile ? _files.ListUsers() : [];
    }

    public IReadOnlyList<HouseholdDto> HouseholdsForUser(Guid userId)
    {
        return _mode.IsFile ? _files.HouseholdsForUser(userId) : [];
    }

    public void AddUser(string email, string login, string household, string nickname, bool isAppAdmin)
    {
        EnsureFile();
        _files.AddUser(email, login, household, nickname, isAppAdmin);
    }

    public void CreateHousehold(string name, string? motto)
    {
        EnsureFile();
        _files.CreateHousehold(name, motto);
    }

    public void SetMotto(string household, string motto)
    {
        EnsureFile();
        _files.SetMotto(household, motto);
    }

    public void RemoveFromHousehold(Guid userId, string household)
    {
        EnsureFile();
        _files.RemoveFromHousehold(userId, household);
    }

    private void EnsureFile()
    {
        if (!_mode.IsFile)
        {
            throw new InvalidOperationException("Switch to File mode to change users on disk.");
        }
    }
}
