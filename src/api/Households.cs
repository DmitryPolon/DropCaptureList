using Microsoft.Data.SqlClient;

namespace DropCaptureList.Api;

public sealed record HouseholdBrand(string Name, string Motto, string LogoLetter);

public sealed class Households
{
    private readonly AzureSql _sql;

    public Households(AzureSql sql)
    {
        _sql = sql;
    }

    public IReadOnlyList<HouseholdBrand> List()
    {
        using var connection = _sql.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT [Name], ISNULL([Motto], N'')
            FROM dbo.Tenants
            ORDER BY [Name];
            """;

        try
        {
            var list = new List<HouseholdBrand>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var name = reader.GetString(0);
                var motto = reader.GetString(1);
                list.Add(new HouseholdBrand(name, motto, Letter(name)));
            }

            return list;
        }
        catch (SqlException ex) when (ex.Number == 207)
        {
            throw new InvalidOperationException(
                "Household motto column is missing. Run database/06_AddTenantMotto.sql against the database.");
        }
    }

    private static string Letter(string name)
    {
        name = name.Trim();
        return string.IsNullOrEmpty(name) ? "?" : char.ToUpperInvariant(name[0]).ToString();
    }
}
