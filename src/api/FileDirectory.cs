using System.Text.Json;
using System.Text.RegularExpressions;

namespace DropCaptureList.Api;

public sealed class FileUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string LoginName { get; set; } = string.Empty;
    public bool IsAppAdmin { get; set; }
}

public sealed class FileMember
{
    public Guid UserId { get; set; }
    public string Nickname { get; set; } = string.Empty;
}

public sealed class FileHousehold
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Motto { get; set; } = string.Empty;
    public List<FileMember> Members { get; set; } = [];
    public List<FileItem> Items { get; set; } = [];
}

public sealed class FileItem
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string Source { get; set; } = "TextLine";
    public string? ExcelAddress { get; set; }
    public int ExcelRow { get; set; }
    public int ExcelColumn { get; set; }
    public bool IsBold { get; set; }
    public string? FontColor { get; set; }
    public string? FillColor { get; set; }
}

public sealed class FileDirectory
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _root;
    private readonly object _gate = new();

    public FileDirectory(string dataDirectory)
    {
        _root = Path.Combine(dataDirectory, "households");
        Directory.CreateDirectory(dataDirectory);
        Directory.CreateDirectory(_root);
    }

    public bool HasUsers()
    {
        lock (_gate)
        {
            return LoadUsers().Count > 0;
        }
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

        lock (_gate)
        {
            var user = FindUser(email) ?? throw new InvalidOperationException(
                "Unknown email. Sign in with the email stored for this user (check spelling). Household name is separate from nickname.");
            var house = FindHousehold(household) ?? throw new InvalidOperationException(
                "That email is registered, but not in this household. Use the household name from the list (not the nickname).");
            var member = house.Members.FirstOrDefault(m => m.UserId == user.Id)
                ?? throw new InvalidOperationException(
                    "That email is registered, but not in this household. Use the household name from the list (not the nickname).");
            return new WebSession(user.Id, user.Email, member.Nickname, house.Name, house.Motto, AppDirectory.Letter(house.Name));
        }
    }

    public bool IsAppAdmin(string email)
    {
        lock (_gate)
        {
            return FindUser(email)?.IsAppAdmin == true;
        }
    }

    public IReadOnlyList<HouseholdBrand> ListHouseholds()
    {
        lock (_gate)
        {
            return LoadHouseholds()
                .OrderBy(h => h.Name, StringComparer.OrdinalIgnoreCase)
                .Select(h => new HouseholdBrand(h.Name, h.Motto, AppDirectory.Letter(h.Name)))
                .ToList();
        }
    }

    public IReadOnlyList<ListItem> ListItems(string household)
    {
        lock (_gate)
        {
            var house = FindHousehold(household);
            if (house is null)
            {
                return [];
            }

            var users = LoadUsers().ToDictionary(u => u.Id);
            return house.Items
                .OrderByDescending(i => i.CreatedAt)
                .Select(i =>
                {
                    var nick = house.Members.FirstOrDefault(m => m.UserId == i.CreatedByUserId)?.Nickname
                        ?? users.GetValueOrDefault(i.CreatedByUserId)?.Email
                        ?? "?";
                    return new ListItem(
                        i.Id, i.Text, nick, i.CreatedAt, false, null, null,
                        i.ExcelRow, i.ExcelColumn, i.IsBold, i.FontColor, i.FillColor);
                })
                .ToList();
        }
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
        lock (_gate)
        {
            var house = RequireHousehold(household);
            if (house.Items.Any(i => string.Equals(i.Text.Trim(), text, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            house.Items.Add(new FileItem
            {
                Id = Guid.NewGuid(),
                Text = text,
                CreatedByUserId = session.UserId,
                CreatedAt = DateTimeOffset.Now,
                Source = "TextLine"
            });
            SaveHousehold(house);
        }
    }

    public void UpsertItems(string email, string household, IEnumerable<FileItem> incoming)
    {
        var session = SignIn(email, household);
        lock (_gate)
        {
            var house = RequireHousehold(household);
            foreach (var item in incoming)
            {
                var text = item.Text.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var existing = house.Items.FirstOrDefault(i => i.Id == item.Id);
                if (existing is not null)
                {
                    existing.Text = text;
                    existing.IsBold = item.IsBold;
                    existing.FontColor = item.FontColor;
                    existing.FillColor = item.FillColor;
                    existing.ExcelRow = item.ExcelRow;
                    existing.ExcelColumn = item.ExcelColumn;
                    existing.ExcelAddress = item.ExcelAddress;
                    continue;
                }

                if (house.Items.Any(i => string.Equals(i.Text.Trim(), text, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                house.Items.Add(new FileItem
                {
                    Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id,
                    Text = text,
                    CreatedByUserId = session.UserId,
                    CreatedAt = item.CreatedAt == default ? DateTimeOffset.Now : item.CreatedAt,
                    Source = string.IsNullOrWhiteSpace(item.Source) ? "ExcelCell" : item.Source,
                    ExcelAddress = item.ExcelAddress,
                    ExcelRow = item.ExcelRow,
                    ExcelColumn = item.ExcelColumn,
                    IsBold = item.IsBold,
                    FontColor = item.FontColor,
                    FillColor = item.FillColor
                });
            }

            SaveHousehold(house);
        }
    }

    public void CompleteItem(string email, string household, Guid itemId)
    {
        SignIn(email, household);
        lock (_gate)
        {
            var house = RequireHousehold(household);
            var removed = house.Items.RemoveAll(i => i.Id == itemId);
            if (removed == 0)
            {
                throw new InvalidOperationException("That item is not on this household list.");
            }

            SaveHousehold(house);
        }
    }

    public void RemoveItem(string email, string household, Guid itemId)
    {
        CompleteItem(email, household, itemId);
    }

    public int ClearAll(string email, string household)
    {
        SignIn(email, household);
        lock (_gate)
        {
            var house = RequireHousehold(household);
            var count = house.Items.Count;
            house.Items.Clear();
            SaveHousehold(house);
            return count;
        }
    }

    public void CreateHousehold(string name, string? motto)
    {
        name = name.Trim();
        motto = (motto ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Enter a household name.");
        }

        lock (_gate)
        {
            var houses = LoadHouseholds();
            if (houses.Count >= 2 && houses.All(h => !string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("File mode allows at most two households.");
            }

            if (houses.Any(h => string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("That household already exists.");
            }

            SaveHousehold(new FileHousehold { Id = Guid.NewGuid(), Name = name, Motto = motto.Length <= 120 ? motto : motto[..120] });
        }
    }

    public void SetMotto(string household, string motto)
    {
        motto = (motto ?? string.Empty).Trim();
        if (motto.Length > 120)
        {
            motto = motto[..120];
        }

        lock (_gate)
        {
            var house = RequireHousehold(household);
            house.Motto = motto;
            SaveHousehold(house);
        }
    }

    public void AddUser(string email, string loginName, string household, string nickname, bool isAppAdmin)
    {
        email = email.Trim();
        nickname = nickname.Trim();
        household = household.Trim();
        loginName = string.IsNullOrWhiteSpace(loginName) ? nickname : loginName.Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(nickname) || string.IsNullOrWhiteSpace(household))
        {
            throw new InvalidOperationException("Email, nickname, and household are required.");
        }

        lock (_gate)
        {
            var house = RequireHousehold(household);
            var users = LoadUsers();
            var user = users.FirstOrDefault(u =>
                string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)
                || string.Equals(u.LoginName, email, StringComparison.OrdinalIgnoreCase));
            if (user is null)
            {
                user = new FileUser { Id = Guid.NewGuid(), Email = email, LoginName = loginName, IsAppAdmin = isAppAdmin };
                users.Add(user);
                SaveUsers(users);
            }
            else
            {
                user.IsAppAdmin = user.IsAppAdmin || isAppAdmin;
                SaveUsers(users);
            }

            if (house.Members.All(m => m.UserId != user.Id))
            {
                house.Members.Add(new FileMember { UserId = user.Id, Nickname = nickname });
                SaveHousehold(house);
            }
        }
    }

    public void RemoveFromHousehold(Guid userId, string household)
    {
        lock (_gate)
        {
            var house = RequireHousehold(household);
            var removed = house.Members.RemoveAll(m => m.UserId == userId);
            if (removed == 0)
            {
                throw new InvalidOperationException("That user is not in this household.");
            }

            SaveHousehold(house);
        }
    }

    public IReadOnlyList<AdminUserDto> ListUsers()
    {
        lock (_gate)
        {
            var users = LoadUsers();
            var houses = LoadHouseholds();
            return users
                .OrderBy(u => u.LoginName, StringComparer.OrdinalIgnoreCase)
                .Select(u => new AdminUserDto(
                    u.Id,
                    u.LoginName,
                    u.Email,
                    u.IsAppAdmin,
                    string.Join(", ", houses.Where(h => h.Members.Any(m => m.UserId == u.Id)).Select(h => h.Name))))
                .ToList();
        }
    }

    public IReadOnlyList<HouseholdDto> HouseholdsForUser(Guid userId)
    {
        lock (_gate)
        {
            return LoadHouseholds()
                .Where(h => h.Members.Any(m => m.UserId == userId))
                .OrderBy(h => h.Name, StringComparer.OrdinalIgnoreCase)
                .Select(h => new HouseholdDto(h.Id, h.Name, h.Motto))
                .ToList();
        }
    }

    public void Import(IReadOnlyList<FileUser> users, IReadOnlyList<FileHousehold> households)
    {
        lock (_gate)
        {
            SaveUsers(users.ToList());
            foreach (var old in Directory.Exists(_root) ? Directory.GetDirectories(_root) : [])
            {
                Directory.Delete(old, recursive: true);
            }

            foreach (var house in households.Take(2))
            {
                SaveHousehold(house);
            }
        }
    }

    private FileUser? FindUser(string email)
    {
        return LoadUsers().FirstOrDefault(u =>
            string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)
            || string.Equals(u.LoginName, email, StringComparison.OrdinalIgnoreCase));
    }

    private FileHousehold? FindHousehold(string name)
    {
        return LoadHouseholds().FirstOrDefault(h => string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private FileHousehold RequireHousehold(string name)
    {
        return FindHousehold(name) ?? throw new InvalidOperationException("Unknown household. Create it first.");
    }

    private List<FileUser> LoadUsers()
    {
        var path = Path.Combine(Path.GetDirectoryName(_root)!, "users.json");
        if (!File.Exists(path))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<FileUser>>(File.ReadAllText(path), Json) ?? [];
    }

    private void SaveUsers(List<FileUser> users)
    {
        var path = Path.Combine(Path.GetDirectoryName(_root)!, "users.json");
        AtomicWrite(path, JsonSerializer.Serialize(users, Json));
    }

    private List<FileHousehold> LoadHouseholds()
    {
        if (!Directory.Exists(_root))
        {
            return [];
        }

        var list = new List<FileHousehold>();
        foreach (var dir in Directory.GetDirectories(_root))
        {
            var path = Path.Combine(dir, "household.json");
            if (!File.Exists(path))
            {
                continue;
            }

            var house = JsonSerializer.Deserialize<FileHousehold>(File.ReadAllText(path), Json);
            if (house is not null)
            {
                list.Add(house);
            }
        }

        return list;
    }

    private void SaveHousehold(FileHousehold house)
    {
        var dir = Path.Combine(_root, FolderName(house.Name));
        Directory.CreateDirectory(dir);
        AtomicWrite(Path.Combine(dir, "household.json"), JsonSerializer.Serialize(house, Json));
    }

    private static string FolderName(string name)
    {
        var safe = Regex.Replace(name.Trim(), @"[^\w\- ]+", "", RegexOptions.CultureInvariant);
        return string.IsNullOrWhiteSpace(safe) ? "household" : safe;
    }

    private static void AtomicWrite(string path, string json)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, path, overwrite: true);
    }
}

public sealed record AdminUserDto(Guid UserId, string LoginName, string Email, bool IsAppAdmin, string Households);

public sealed record HouseholdDto(Guid Id, string Name, string Motto);
