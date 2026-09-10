namespace DropCaptureList.Api;

public sealed class StoreFront
{
    private readonly FileDirectory _files;

    public StoreFront(FileDirectory files)
    {
        _files = files;
    }

    public WebSession SignIn(string email, string household) => _files.SignIn(email, household);

    public bool IsAppAdmin(string email) => _files.IsAppAdmin(email);

    public IReadOnlyList<HouseholdBrand> ListHouseholds() => _files.ListHouseholds();

    public IReadOnlyList<ListItem> ListItems(string household) => _files.ListItems(household);

    public void AddTextItem(string email, string household, string text) =>
        _files.AddTextItem(email, household, text);

    public void ToggleComplete(string email, string household, Guid itemId) =>
        _files.CompleteItem(email, household, itemId);

    public void RemoveItem(string email, string household, Guid itemId) =>
        _files.RemoveItem(email, household, itemId);

    public int ClearCompleted(string email, string household) => _files.ClearAll(email, household);

    public int ClearAll(string email, string household) => _files.ClearAll(email, household);

    public void UpsertItems(string email, string household, IEnumerable<FileItem> items) =>
        _files.UpsertItems(email, household, items);

    public IReadOnlyList<AdminUserDto> ListUsers(string actorEmail)
    {
        RequireAppAdmin(actorEmail);
        return _files.ListUsers();
    }

    public IReadOnlyList<HouseholdDto> HouseholdsForUser(Guid userId) => _files.HouseholdsForUser(userId);

    public IReadOnlyList<MemberDto> ListMembers(string actorEmail, string household)
    {
        RequireHouseholdAccess(actorEmail, household);
        return _files.ListMembers(household);
    }

    public void AddMember(string actorEmail, string household, string email, string nickname)
    {
        RequireHouseholdAccess(actorEmail, household);
        _files.AddUser(email, nickname, household, nickname, isAppAdmin: false);
    }

    public void RemoveMember(string actorEmail, string household, Guid userId)
    {
        RequireHouseholdAccess(actorEmail, household);
        _files.RemoveFromHousehold(userId, household);
    }

    public void CreateHousehold(string actorEmail, string name, string? motto, string memberEmail, string memberNickname)
    {
        RequireAppAdminOrEmpty(actorEmail);
        _files.CreateHouseholdWithMember(name, motto, memberEmail, memberNickname);
    }

    public void DeleteHousehold(string actorEmail, string name)
    {
        RequireAppAdmin(actorEmail);
        _files.DeleteHousehold(name);
    }

    public void SetMotto(string actorEmail, string household, string motto)
    {
        RequireHouseholdAccess(actorEmail, household);
        _files.SetMotto(household, motto);
    }

    private void RequireAppAdmin(string email)
    {
        if (!_files.IsAppAdmin(email))
        {
            throw new InvalidOperationException("Only an app admin can do that.");
        }
    }

    private void RequireAppAdminOrEmpty(string email)
    {
        if (_files.HasUsers() && !_files.IsAppAdmin(email))
        {
            throw new InvalidOperationException("Only an app admin can create a household.");
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
