using System.Data;
using Dapper;
using Gba.TradeLicense.Application.Abstractions;
using Gba.TradeLicense.Application.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

public sealed class DashboardService : IDashboardService
{
    private readonly string _connStr;

    public DashboardService(IConfiguration config)
    {
        _connStr = config.GetConnectionString("Default")!;
    }

    public async Task<IEnumerable<ApplicationStatusCount>> GetStatusCountAsync(
        int loginID,
        CancellationToken ct)
    {
        using var db = new SqlConnection(_connStr);

        return await db.QueryAsync<ApplicationStatusCount>(
            "usp_Dashboard_ApplicationStatusCount",
            new { loginID },
            commandType: CommandType.StoredProcedure
        );
    }
}
