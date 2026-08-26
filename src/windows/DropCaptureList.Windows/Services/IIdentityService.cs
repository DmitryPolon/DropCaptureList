using DropCaptureList.Windows.Models;

namespace DropCaptureList.Windows.Services;

public interface IIdentityService
{
    UserSession SignIn(string emailOrLogin, string householdName);
    IReadOnlyList<LocalTenant> GetHouseholdsForUser(Guid userId);
    IReadOnlyList<string> KnownHouseholds();
    IReadOnlyList<AdminUserRow> ListUsers();
    void AddUser(string email, string loginName, string householdName, string nickname, bool isAppAdmin);
    void CreateHousehold(string name);
    void RemoveFromHousehold(Guid userId, string householdName);
}

public interface ICaptureService
{
    IReadOnlyList<CapturedItem> GetItems(Guid tenantId);
    IReadOnlyList<CapturedItem> AddExcelCells(UserSession session, IEnumerable<ExcelCellText> cells);
    int DeleteItems(Guid tenantId, IEnumerable<Guid> itemIds);
    int CompleteHousehold(Guid tenantId, Guid completedByUserId);
}
