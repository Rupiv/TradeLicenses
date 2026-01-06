using System.Data;
using Dapper;
using Gba.TradeLicense.Application.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

[ApiController]
[Route("api/payment-gateway-banks")]
public class PaymentGatewayBankController : ControllerBase
{
    private readonly string _connStr;

    public PaymentGatewayBankController(IConfiguration config)
    {
        _connStr = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string not found");
    }

    private IDbConnection CreateConnection()
        => new SqlConnection(_connStr);

    // ================= GET ALL =================
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        using var db = CreateConnection();

        var result = await db.QueryAsync(
            "usp_PaymentGatewayBank_CRUD",
            new { Action = "SELECT" },
            commandType: CommandType.StoredProcedure
        );

        return Ok(result);
    }

    // ================= GET BY ID =================
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        using var db = CreateConnection();

        var result = await db.QueryFirstOrDefaultAsync(
            "usp_PaymentGatewayBank_CRUD",
            new { Action = "SELECT_BY_ID", pg_ID = id },
            commandType: CommandType.StoredProcedure
        );

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    // ================= INSERT =================
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] PaymentGatewayBankDto dto,
        CancellationToken ct)
    {
        using var db = CreateConnection();

        var newId = await db.ExecuteScalarAsync<int>(
            "usp_PaymentGatewayBank_CRUD",
            new
            {
                Action = "INSERT",
                pg_Bank = dto.Pg_Bank,
                pg_TypeID = dto.Pg_TypeID
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new
        {
            pg_ID = newId,
            Message = "Inserted successfully"
        });
    }

    // ================= UPDATE =================
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] PaymentGatewayBankDto dto,
        CancellationToken ct)
    {
        using var db = CreateConnection();

        await db.ExecuteAsync(
            "usp_PaymentGatewayBank_CRUD",
            new
            {
                Action = "UPDATE",
                pg_ID = id,
                pg_Bank = dto.Pg_Bank,
                pg_TypeID = dto.Pg_TypeID
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { Message = "Updated successfully" });
    }

    // ================= DELETE =================
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        using var db = CreateConnection();

        await db.ExecuteAsync(
            "usp_PaymentGatewayBank_CRUD",
            new { Action = "DELETE", pg_ID = id },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { Message = "Deleted successfully" });
    }
}
