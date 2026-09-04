using System.Net.Http;
using System.Text;
using System.Text.Json;
using DropCaptureList.Windows.Models;

namespace DropCaptureList.Windows.Services;

public sealed class ApiBackend
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(2) };
    private readonly string _base;

    public ApiBackend(string apiBase)
    {
        _base = apiBase.TrimEnd('/');
    }

    public string? LastEmail { get; set; }

    public string? LastHousehold { get; set; }

    public UserSession SignIn(string email, string household)
    {
        var body = Post("/api/session", new { email, household });
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        LastEmail = root.GetProperty("email").GetString();
        LastHousehold = root.GetProperty("household").GetString() ?? household;
        var tenantId = Guid.Empty;
        var houses = HouseholdsForUser(root.TryGetProperty("userId", out var rawId) && rawId.ValueKind == JsonValueKind.String
            ? Guid.Parse(rawId.GetString()!)
            : Guid.Empty);
        var match = houses.FirstOrDefault(h => string.Equals(h.Name, LastHousehold, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            tenantId = match.Id;
        }

        return new UserSession
        {
            UserId = rawId.ValueKind == JsonValueKind.String ? Guid.Parse(rawId.GetString()!) : Guid.Empty,
            Email = LastEmail ?? email,
            Nickname = root.GetProperty("nickname").GetString() ?? "",
            TenantId = tenantId,
            TenantName = LastHousehold,
            IsAppAdmin = root.TryGetProperty("isAppAdmin", out var admin) && admin.GetBoolean()
        };
    }

    public IReadOnlyList<LocalTenant> HouseholdsForUser(Guid userId)
    {
        var json = Get($"/api/admin/households/{userId}");
        return JsonSerializer.Deserialize<List<ApiHousehold>>(json, Json)?.Select(h => new LocalTenant
        {
            Id = h.Id,
            Name = h.Name,
            Motto = h.Motto ?? ""
        }).ToList() ?? [];
    }

    public IReadOnlyList<string> KnownHouseholds()
    {
        var json = Get("/api/households");
        return JsonSerializer.Deserialize<List<ApiBrand>>(json, Json)?.Select(h => h.Name).ToList() ?? [];
    }

    public IReadOnlyList<AdminUserRow> ListUsers()
    {
        var json = Get("/api/admin/users");
        return JsonSerializer.Deserialize<List<ApiAdminUser>>(json, Json)?.Select(u => new AdminUserRow
        {
            UserId = u.UserId,
            LoginName = u.LoginName,
            Email = u.Email,
            IsAppAdmin = u.IsAppAdmin,
            Households = u.Households
        }).ToList() ?? [];
    }

    public void AddUser(string email, string loginName, string household, string nickname, bool isAppAdmin)
    {
        Post("/api/admin/users", new { email, loginName, household, nickname, isAppAdmin });
    }

    public void CreateHousehold(string name, string? motto)
    {
        Post("/api/admin/households", new { name, motto });
    }

    public void SetMotto(string household, string motto)
    {
        Post("/api/admin/motto", new { household, motto });
    }

    public void RemoveFromHousehold(Guid userId, string household)
    {
        Post("/api/admin/remove", new { userId, household });
    }

    public IReadOnlyList<CapturedItem> ListItems(string household)
    {
        var json = Get($"/api/households/{Uri.EscapeDataString(household)}/items");
        return JsonSerializer.Deserialize<List<ApiItem>>(json, Json)?.Select(i => new CapturedItem
        {
            Id = i.Id,
            Text = i.Text,
            Nickname = i.Nickname,
            TenantName = household,
            CreatedAt = i.CreatedAt,
            Source = CaptureSources.ExcelCell,
            ExcelRow = i.ExcelRow,
            ExcelColumn = i.ExcelColumn,
            IsBold = i.IsBold,
            FontColor = i.FontColor,
            FillColor = i.FillColor
        }).ToList() ?? [];
    }

    public void SaveItems(string email, string household, IReadOnlyList<CapturedItem> items)
    {
        Post($"/api/households/{Uri.EscapeDataString(household)}/items/bulk", new
        {
            email,
            household,
            items = items.Select(i => new
            {
                id = i.Id,
                text = i.Text,
                createdByUserId = i.UserId,
                createdAt = i.CreatedAt,
                source = i.Source,
                excelAddress = i.ExcelAddress,
                excelRow = i.ExcelRow,
                excelColumn = i.ExcelColumn,
                isBold = i.IsBold,
                fontColor = i.FontColor,
                fillColor = i.FillColor
            })
        });
    }

    public void DeleteItems(string email, string household, IEnumerable<Guid> ids)
    {
        foreach (var id in ids)
        {
            Post($"/api/households/{Uri.EscapeDataString(household)}/items/{id}/remove", new { email, household });
        }
    }

    public int ClearList(string email, string household)
    {
        var json = Post($"/api/households/{Uri.EscapeDataString(household)}/clear", new { email, household });
        try
        {
            return JsonSerializer.Deserialize<List<ApiItem>>(json, Json)?.Count ?? 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private string Get(string path)
    {
        using var response = _http.GetAsync(_base + path).GetAwaiter().GetResult();
        var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(StorageModeClient.Problem(body, "API request failed."));
        }

        return body;
    }

    private string Post(string path, object payload)
    {
        using var content = new StringContent(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json");
        using var response = _http.PostAsync(_base + path, content).GetAwaiter().GetResult();
        var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(StorageModeClient.Problem(body, "API request failed."));
        }

        return body;
    }

    private sealed class ApiHousehold
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string? Motto { get; set; }
    }

    private sealed class ApiBrand
    {
        public string Name { get; set; } = "";
    }

    private sealed class ApiAdminUser
    {
        public Guid UserId { get; set; }
        public string LoginName { get; set; } = "";
        public string Email { get; set; } = "";
        public bool IsAppAdmin { get; set; }
        public string Households { get; set; } = "";
    }

    private sealed class ApiItem
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = "";
        public string Nickname { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
        public int ExcelRow { get; set; }
        public int ExcelColumn { get; set; }
        public bool IsBold { get; set; }
        public string? FontColor { get; set; }
        public string? FillColor { get; set; }
    }
}

public sealed class ModeAwareIdentity : IIdentityService
{
    private readonly StorageModeClient _mode;
    private readonly IIdentityService _sql;
    private readonly ApiBackend? _api;

    public ModeAwareIdentity(StorageModeClient mode, IIdentityService sql, ApiBackend? api)
    {
        _mode = mode;
        _sql = sql;
        _api = api;
    }

    public UserSession SignIn(string emailOrLogin, string householdName)
    {
        EnsureMode();
        if (!_mode.IsFile || _api is null)
        {
            return _sql.SignIn(emailOrLogin, householdName);
        }

        return _api.SignIn(emailOrLogin, householdName);
    }

    public IReadOnlyList<LocalTenant> GetHouseholdsForUser(Guid userId)
    {
        EnsureMode();
        return _mode.IsFile && _api is not null ? _api.HouseholdsForUser(userId) : _sql.GetHouseholdsForUser(userId);
    }

    public IReadOnlyList<string> KnownHouseholds()
    {
        EnsureMode();
        return _mode.IsFile && _api is not null ? _api.KnownHouseholds() : _sql.KnownHouseholds();
    }

    public IReadOnlyList<AdminUserRow> ListUsers()
    {
        EnsureMode();
        return _mode.IsFile && _api is not null ? _api.ListUsers() : _sql.ListUsers();
    }

    public void AddUser(string email, string loginName, string householdName, string nickname, bool isAppAdmin)
    {
        EnsureMode();
        if (_mode.IsFile && _api is not null)
        {
            _api.AddUser(email, loginName, householdName, nickname, isAppAdmin);
            return;
        }

        _sql.AddUser(email, loginName, householdName, nickname, isAppAdmin);
    }

    public void CreateHousehold(string name, string? motto = null)
    {
        EnsureMode();
        if (_mode.IsFile && _api is not null)
        {
            _api.CreateHousehold(name, motto);
            return;
        }

        _sql.CreateHousehold(name, motto);
    }

    public void SetHouseholdMotto(string householdName, string motto)
    {
        EnsureMode();
        if (_mode.IsFile && _api is not null)
        {
            _api.SetMotto(householdName, motto);
            return;
        }

        _sql.SetHouseholdMotto(householdName, motto);
    }

    public void RemoveFromHousehold(Guid userId, string householdName)
    {
        EnsureMode();
        if (_mode.IsFile && _api is not null)
        {
            _api.RemoveFromHousehold(userId, householdName);
            return;
        }

        _sql.RemoveFromHousehold(userId, householdName);
    }

    private void EnsureMode()
    {
        if (_mode.HasApi)
        {
            try
            {
                _mode.Refresh();
            }
            catch
            {
            }
        }
    }
}

public sealed class ModeAwareCapture : ICaptureService
{
    private readonly StorageModeClient _mode;
    private readonly ICaptureService _sql;
    private readonly ApiBackend? _api;

    public ModeAwareCapture(StorageModeClient mode, ICaptureService sql, ApiBackend? api)
    {
        _mode = mode;
        _sql = sql;
        _api = api;
    }

    public IReadOnlyList<CapturedItem> GetItems(Guid tenantId)
    {
        try
        {
            _mode.Refresh();
        }
        catch
        {
        }

        if (_mode.IsFile && _api is not null)
        {
            var household = _api.LastHousehold;
            if (string.IsNullOrWhiteSpace(household))
            {
                throw new InvalidOperationException("Sign in again so File mode can load the list from the API.");
            }

            return _api.ListItems(household);
        }

        return _sql.GetItems(tenantId);
    }

    public IReadOnlyList<CapturedItem> AddExcelCells(UserSession session, IEnumerable<ExcelCellText> cells)
    {
        return _sql.AddExcelCells(session, cells);
    }

    public CaptureSaveResult SaveItems(UserSession session, IReadOnlyList<CapturedItem> items)
    {
        if (UseFile(out var api, out var household))
        {
            api.SaveItems(session.Email, household, items);
            return new CaptureSaveResult { Inserted = items.Count };
        }

        return _sql.SaveItems(session, items);
    }

    public int DeleteItems(Guid tenantId, IEnumerable<Guid> itemIds)
    {
        if (UseFile(out var api, out var household) && api.LastEmail is { } email)
        {
            var ids = itemIds.ToList();
            api.DeleteItems(email, household, ids);
            return ids.Count;
        }

        if (UseFile(out api, out household))
        {
            throw new InvalidOperationException("Sign in again so File mode can delete through the API.");
        }

        return _sql.DeleteItems(tenantId, itemIds);
    }

    public int CompleteHousehold(Guid tenantId, Guid completedByUserId)
    {
        if (UseFile(out var api, out var household) && api.LastEmail is { } email)
        {
            api.ClearList(email, household);
            return 1;
        }

        return _sql.CompleteHousehold(tenantId, completedByUserId);
    }

    public int PurgeCompletedOlderThanOneMonth()
    {
        return _mode.IsFile ? 0 : _sql.PurgeCompletedOlderThanOneMonth();
    }

    public AdminSnapshot GetVCoreSnapshot() => _sql.GetVCoreSnapshot();

    public AdminSnapshot GetSqlUsageSnapshot() => _sql.GetSqlUsageSnapshot();

    private bool UseFile(out ApiBackend api, out string household)
    {
        api = _api!;
        household = _api?.LastHousehold ?? "";
        return _mode.IsFile && _api is not null && household.Length > 0;
    }
}
