using DropCaptureList.Windows.Models;

namespace DropCaptureList.Windows.Services;

public sealed class LocalIdentityService : IIdentityService
{
    private readonly ICaptureStore _store;

    public LocalIdentityService(ICaptureStore store)
    {
        _store = store;
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

        var db = _store.Load();
        var user = db.Users.FirstOrDefault(u =>
            string.Equals(u.Email, emailOrLogin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(u.Nickname, emailOrLogin, StringComparison.OrdinalIgnoreCase));

        if (user is null)
        {
            throw new InvalidOperationException("Unknown user. An app admin must add this email first.");
        }

        var tenant = db.Tenants.FirstOrDefault(t =>
            string.Equals(t.Name, householdName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Unknown household.");

        var member = db.Memberships.FirstOrDefault(m => m.UserId == user.Id && m.TenantId == tenant.Id)
            ?? throw new InvalidOperationException("This user is not a member of that household.");

        return new UserSession
        {
            UserId = user.Id,
            Email = user.Email ?? emailOrLogin,
            Nickname = user.Nickname,
            TenantId = tenant.Id,
            TenantName = tenant.Name,
            IsAppAdmin = user.IsAppAdmin
        };
    }

    public IReadOnlyList<LocalTenant> GetHouseholdsForUser(Guid userId)
    {
        var db = _store.Load();
        var tenantIds = db.Memberships
            .Where(m => m.UserId == userId)
            .Select(m => m.TenantId)
            .ToHashSet();

        return db.Tenants.Where(t => tenantIds.Contains(t.Id)).OrderBy(t => t.Name).ToList();
    }

    public IReadOnlyList<string> KnownHouseholds()
    {
        return _store.Load().Tenants.Select(t => t.Name).OrderBy(n => n).ToList();
    }

    public IReadOnlyList<AdminUserRow> ListUsers()
    {
        var db = _store.Load();
        return db.Users.Select(u => new AdminUserRow
        {
            UserId = u.Id,
            LoginName = u.Nickname,
            Email = u.Email ?? string.Empty,
            IsAppAdmin = u.IsAppAdmin,
            Households = string.Join(", ",
                db.Memberships.Where(m => m.UserId == u.Id)
                    .Join(db.Tenants, m => m.TenantId, t => t.Id, (_, t) => t.Name)
                    .OrderBy(n => n))
        }).OrderBy(u => u.Email).ToList();
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

        var db = _store.Load();
        if (db.Users.Any(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("That email is already a user.");
        }

        var tenant = db.Tenants.FirstOrDefault(t =>
            string.Equals(t.Name, householdName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Unknown household. Create it first.");

        var user = new LocalUser
        {
            Id = Guid.NewGuid(),
            Nickname = nickname,
            Email = email,
            IsAppAdmin = isAppAdmin
        };
        db.Users.Add(user);
        db.Memberships.Add(new LocalMembership { UserId = user.Id, TenantId = tenant.Id });
        _store.Save(db);
    }

    public void CreateHousehold(string name, string? motto = null)
    {
        name = name.Trim();
        motto = (motto ?? string.Empty).Trim();
        if (motto.Length > 120)
        {
            motto = motto[..120];
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Enter a household name.");
        }

        var db = _store.Load();
        if (db.Tenants.Any(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("That household already exists.");
        }

        db.Tenants.Add(new LocalTenant { Id = Guid.NewGuid(), Name = name, Motto = motto });
        _store.Save(db);
    }

    public void SetHouseholdMotto(string householdName, string motto)
    {
        householdName = householdName.Trim();
        motto = (motto ?? string.Empty).Trim();
        if (motto.Length > 120)
        {
            motto = motto[..120];
        }

        var db = _store.Load();
        var tenant = db.Tenants.FirstOrDefault(t =>
            string.Equals(t.Name, householdName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Unknown household.");
        tenant.Motto = motto;
        _store.Save(db);
    }

    public void RemoveFromHousehold(Guid userId, string householdName)
    {
        householdName = householdName.Trim();
        var db = _store.Load();
        var tenant = db.Tenants.FirstOrDefault(t =>
            string.Equals(t.Name, householdName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Unknown household.");
        var removed = db.Memberships.RemoveAll(m => m.UserId == userId && m.TenantId == tenant.Id);
        if (removed == 0)
        {
            throw new InvalidOperationException("That user is not in this household.");
        }

        _store.Save(db);
    }
}
