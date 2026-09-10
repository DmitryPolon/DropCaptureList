using DropCaptureList.Windows.Models;

namespace DropCaptureList.Windows.Services;

public interface IIdentityService
{
    UserSession SignIn(string emailOrLogin, string householdName);
    IReadOnlyList<LocalTenant> GetHouseholdsForUser(Guid userId);
    IReadOnlyList<string> KnownHouseholds();
    IReadOnlyList<LocalTenant> ListAllHouseholds();
    IReadOnlyList<AdminUserRow> ListUsers();
    IReadOnlyList<MemberRow> ListMembers(string household);
    void AddMember(string household, string email, string nickname);
    void CreateHousehold(string name, string? motto, string memberEmail, string memberNickname);
    void DeleteHousehold(string name);
    void SetHouseholdMotto(string householdName, string motto);
    void RemoveFromHousehold(Guid userId, string householdName);
}

public interface ICaptureService
{
    IReadOnlyList<CapturedItem> GetItems(Guid tenantId);
    IReadOnlyList<CapturedItem> AddExcelCells(UserSession session, IEnumerable<ExcelCellText> cells);
    CaptureSaveResult SaveItems(UserSession session, IReadOnlyList<CapturedItem> items);
    int DeleteItems(Guid tenantId, IEnumerable<Guid> itemIds);
    int CompleteHousehold(Guid tenantId, Guid completedByUserId);
    int PurgeCompletedOlderThanOneMonth();
    AdminSnapshot GetVCoreSnapshot();
    AdminSnapshot GetSqlUsageSnapshot();
}
