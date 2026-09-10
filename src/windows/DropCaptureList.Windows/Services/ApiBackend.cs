using System.Net.Http;
using System.Text;
using System.Text.Json;
using DropCaptureList.Windows.Models;

namespace DropCaptureList.Windows.Services;

public sealed class ApiBackend : IIdentityService, ICaptureService
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

    public IReadOnlyList<LocalTenant> ListAllHouseholds()
    {
        var email = Uri.EscapeDataString(LastEmail ?? "");
        var json = Get($"/api/admin/directory?email={email}");
        return JsonSerializer.Deserialize<List<ApiDirectory>>(json, Json)?.Select(h => new LocalTenant
        {
            Name = h.Name,
            Emails = h.Emails ?? ""
        }).ToList() ?? [];
    }

    public IReadOnlyList<string> KnownHouseholds()
    {
        var json = Get("/api/households");
        return JsonSerializer.Deserialize<List<ApiBrand>>(json, Json)?.Select(h => h.Name).ToList() ?? [];
    }

    public IReadOnlyList<LocalTenant> GetHouseholdsForUser(Guid userId) => HouseholdsForUser(userId);

    public IReadOnlyList<AdminUserRow> ListUsers()
    {
        var email = Uri.EscapeDataString(LastEmail ?? "");
        var json = Get($"/api/admin/users?email={email}");
        return JsonSerializer.Deserialize<List<ApiAdminUser>>(json, Json)?.Select(u => new AdminUserRow
        {
            UserId = u.UserId,
            LoginName = u.LoginName,
            Email = u.Email,
            IsAppAdmin = u.IsAppAdmin,
            Households = u.Households
        }).ToList() ?? [];
    }

    public IReadOnlyList<MemberRow> ListMembers(string household)
    {
        var email = Uri.EscapeDataString(LastEmail ?? "");
        var house = Uri.EscapeDataString(household);
        var json = Get($"/api/members?email={email}&household={house}");
        return JsonSerializer.Deserialize<List<ApiMember>>(json, Json)?.Select(m => new MemberRow
        {
            UserId = m.UserId,
            Email = m.Email,
            Nickname = m.Nickname,
            IsAppAdmin = m.IsAppAdmin
        }).ToList() ?? [];
    }

    public void AddMember(string household, string email, string nickname)
    {
        Post("/api/members", new { actorEmail = LastEmail, household, email, nickname });
    }

    public void CreateHousehold(string name, string? motto, string memberEmail, string memberNickname)
    {
        Post("/api/admin/households", new
        {
            actorEmail = LastEmail,
            name,
            motto,
            memberEmail,
            memberNickname
        });
    }

    public void DeleteHousehold(string name)
    {
        Post("/api/admin/households/delete", new { actorEmail = LastEmail, name });
    }

    public void SetHouseholdMotto(string household, string motto)
    {
        Post("/api/admin/motto", new { actorEmail = LastEmail, household, motto });
    }

    public void RemoveFromHousehold(Guid userId, string household)
    {
        Post("/api/members/remove", new { actorEmail = LastEmail, household, userId });
    }

    public IReadOnlyList<CapturedItem> GetItems(Guid tenantId)
    {
        var household = LastHousehold;
        if (string.IsNullOrWhiteSpace(household))
        {
            throw new InvalidOperationException("Sign in again so the list can load from the API.");
        }

        return ListItems(household);
    }

    public IReadOnlyList<CapturedItem> AddExcelCells(UserSession session, IEnumerable<ExcelCellText> cells)
    {
        throw new InvalidOperationException("Excel capture stays on this PC until you tap Save.");
    }

    public CaptureSaveResult SaveItems(UserSession session, IReadOnlyList<CapturedItem> items)
    {
        SaveItems(session.Email, session.TenantName, items);
        return new CaptureSaveResult { Inserted = items.Count };
    }

    public int DeleteItems(Guid tenantId, IEnumerable<Guid> itemIds)
    {
        if (LastEmail is not { Length: > 0 } email || string.IsNullOrWhiteSpace(LastHousehold))
        {
            throw new InvalidOperationException("Sign in again so deletes can go through the API.");
        }

        var ids = itemIds.ToList();
        DeleteItems(email, LastHousehold, ids);
        return ids.Count;
    }

    public int CompleteHousehold(Guid tenantId, Guid completedByUserId)
    {
        if (LastEmail is not { Length: > 0 } email || string.IsNullOrWhiteSpace(LastHousehold))
        {
            throw new InvalidOperationException("Sign in again so the list can be cleared through the API.");
        }

        ClearList(email, LastHousehold);
        return 1;
    }

    public int PurgeCompletedOlderThanOneMonth() => 0;

    public AdminSnapshot GetVCoreSnapshot() => new();

    public AdminSnapshot GetSqlUsageSnapshot() => new();

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
            throw new InvalidOperationException(StorageModeClient.Problem(body, response.StatusCode, path));
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
            throw new InvalidOperationException(StorageModeClient.Problem(body, response.StatusCode, path));
        }

        return body;
    }

    private sealed class ApiHousehold
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string? Motto { get; set; }
    }

    private sealed class ApiDirectory
    {
        public string Name { get; set; } = "";
        public string? Emails { get; set; }
    }

    private sealed class ApiBrand
    {
        public string Name { get; set; } = "";
        public string? Motto { get; set; }
    }

    private sealed class ApiMember
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = "";
        public string Nickname { get; set; } = "";
        public bool IsAppAdmin { get; set; }
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
