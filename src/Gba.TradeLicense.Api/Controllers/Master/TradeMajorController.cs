using System.Data;
using Gba.TradeLicense.Application.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;

[ApiController]
[Route("api/master/trade-major")]
public class TradeMajorController : ControllerBase
{
    private readonly IConfiguration _config;

    public TradeMajorController(IConfiguration config)
    {
        _config = config;
    }

    private IDbConnection Db()
        => new SqlConnection(_config.GetConnectionString("Default"));

    // GET ALL
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        using var db = Db();
        var data = await db.QueryAsync<TradeMajorDto>(
            "usp_TradeMajor_CRUD",
            new { Action = "GET" },
            commandType: CommandType.StoredProcedure);

        return Ok(data);
    }

    // INSERT
    [HttpPost]
    public async Task<IActionResult> Insert(TradeMajorDto dto)
    {
        using var db = Db();
        var id = await db.ExecuteScalarAsync<int>(
            "usp_TradeMajor_CRUD",
            new
            {
                Action = "INSERT",
                dto.TradeMajorCode,
                dto.TradeMajorName,
                dto.TradeMajorNativeName
            },
            commandType: CommandType.StoredProcedure);

        return Ok(new { Success = true, ID = id });
    }

    // UPDATE
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, TradeMajorDto dto)
    {
        using var db = Db();
        await db.ExecuteAsync(
            "usp_TradeMajor_CRUD",
            new
            {
                Action = "UPDATE",
                tradeMajorID = id,
                dto.TradeMajorCode,
                dto.TradeMajorName,
                dto.TradeMajorNativeName
            },
            commandType: CommandType.StoredProcedure);

        return Ok(new { Success = true });
    }

    // DELETE
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        using var db = Db();
        await db.ExecuteAsync(
            "usp_TradeMajor_CRUD",
            new { Action = "DELETE", tradeMajorID = id },
            commandType: CommandType.StoredProcedure);

        return Ok(new { Success = true });
    }
}
