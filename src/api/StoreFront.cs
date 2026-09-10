namespace DropCaptureList.Api;

public sealed class StoreFront
{
    private readonly FileDirectory _files;

    public StoreFront(FileDirectory files)
    {
        _files = files;
    }

    public WebSession SignIn(string email, string household, string? pin) =>
        _files.SignIn(email, household, pin);

    public bool IsAppAdmin(string email) => _files.IsAppAdmin(email);

    public string NewHouseholdPin => _files.NewHouseholdPin;

    public IReadOnlyList<HouseholdBrand> ListHouseholds() => _files.ListHouseholds();

    public IReadOnlyList<ListItem> ListItems(string email, string household, string? pin) =>
        _files.ListItems(email, household, pin);

    public void AddTextItem(string email, string household, string? pin, string text) =>
        _files.AddTextItem(email, household, pin, text);

    public void ToggleComplete(string email, string household, string? pin, Guid itemId) =>
        _files.CompleteItem(email, household, pin, itemId);

    public void RemoveItem(string email, string household, string? pin, Guid itemId) =>
        _files.RemoveItem(email, household, pin, itemId);

    public int ClearCompleted(string email, string household, string? pin) =>
        _files.ClearAll(email, household, pin);

    public int ClearAll(string email, string household, string? pin) =>
        _files.ClearAll(email, household, pin);

    public void UpsertItems(string email, string household, string? pin, IEnumerable<FileItem> items) =>
        _files.UpsertItems(email, household, pin, items);

    public IReadOnlyList<AdminUserDto> ListUsers(string actorEmail, string actorHousehold, string? pin)
    {
        EnsureActor(actorEmail, actorHousehold, pin);
        RequireAppAdmin(actorEmail);
        return _files.ListUsers();
    }

    public IReadOnlyList<HouseholdDirectoryDto> ListDirectory(string actorEmail, string actorHousehold, string? pin)
    {
        EnsureActor(actorEmail, actorHousehold, pin);
        RequireAppAdmin(actorEmail);
        return _files.ListHouseholds()
            .Select(h => new HouseholdDirectoryDto(
                h.Name,
                string.Join(", ", _files.ListMembers(h.Name)
                    .Select(m => string.IsNullOrWhiteSpace(m.Email) ? m.Nickname : m.Email))))
            .ToList();
    }

    public IReadOnlyList<HouseholdDto> HouseholdsForUser(Guid userId) => _files.HouseholdsForUser(userId);

    public IReadOnlyList<MemberDto> ListMembers(string actorEmail, string actorHousehold, string? pin, string household)
    {
        EnsureActor(actorEmail, actorHousehold, pin);
        RequireHouseholdAccess(actorEmail, household);
        return _files.ListMembers(household);
    }

    public void AddMember(string actorEmail, string actorHousehold, string? pin, string household, string email, string nickname)
    {
        EnsureActor(actorEmail, actorHousehold, pin);
        RequireHouseholdAccess(actorEmail, household);
        _files.AddUser(email, nickname, household, nickname, isAppAdmin: false);
    }

    public void RemoveMember(string actorEmail, string actorHousehold, string? pin, string household, Guid userId)
    {
        EnsureActor(actorEmail, actorHousehold, pin);
        RequireHouseholdAccess(actorEmail, household);
        _files.RemoveFromHousehold(userId, household);
    }

    public void CreateHousehold(string actorEmail, string actorHousehold, string? pin, string name, string? motto, string memberEmail, string memberNickname)
    {
        if (_files.HasUsers())
        {
            EnsureActor(actorEmail, actorHousehold, pin);
            RequireAppAdmin(actorEmail);
        }

        _files.CreateHouseholdWithMember(name, motto, memberEmail, memberNickname);
    }

    public void DeleteHousehold(string actorEmail, string actorHousehold, string? pin, string name)
    {
        EnsureActor(actorEmail, actorHousehold, pin);
        RequireAppAdmin(actorEmail);
        _files.DeleteHousehold(name);
    }

    public void SetMotto(string actorEmail, string actorHousehold, string? pin, string household, string motto)
    {
        EnsureActor(actorEmail, actorHousehold, pin);
        RequireHouseholdAccess(actorEmail, household);
        _files.SetMotto(household, motto);
    }

    public void SetPin(string actorEmail, string actorHousehold, string? pin, string household, string newPin)
    {
        EnsureActor(actorEmail, actorHousehold, pin);
        RequireAppAdmin(actorEmail);
        _files.SetPin(household, newPin);
    }

    public string AdminDefaultPin(string actorEmail, string actorHousehold, string? pin)
    {
        EnsureActor(actorEmail, actorHousehold, pin);
        RequireAppAdmin(actorEmail);
        return NewHouseholdPin;
    }

    private void EnsureActor(string email, string household, string? pin) =>
        _files.SignIn(email, household, pin);

    private void RequireAppAdmin(string email)
    {
        if (!_files.IsAppAdmin(email))
        {
            throw new InvalidOperationException("Only an app admin can do that.");
        }
    }

    private void RequireHouseholdAccess(string email, string household)
    {
        if (_files.IsAppAdmin(email) || _files.IsMember(email, household))
        {
            return;
        }

        throw new InvalidOperationException("You are not a member of this household.");
    }
}
