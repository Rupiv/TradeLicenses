using System.Data;
using Dapper;
using Gba.TradeLicense.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

[ApiController]
[Route("api/master-moh")]
public class MasterMohController : ControllerBase
{
    private readonly IConfiguration _config;

    public MasterMohController(IConfiguration config)
    {
        _config = config;
    }

    private IDbConnection CreateConnection()
        => new SqlConnection(_config.GetConnectionString("Default"));

    // ================= GET ALL =================
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            using var db = CreateConnection();

            var list = await db.QueryAsync<MasterMohDto>(
                "usp_Master_MOH_CRUD",
                new { Action = "GET_ALL" },
                commandType: CommandType.StoredProcedure
            );

            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ================= GET BY ID =================
    [HttpGet("by-id/{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            using var db = CreateConnection();

            var moh = await db.QueryFirstOrDefaultAsync<MasterMohDto>(
                "usp_Master_MOH_CRUD",
                new { Action = "GET_BY_ID", mohID = id },
                commandType: CommandType.StoredProcedure
            );

            if (moh == null)
                return NotFound();

            return Ok(moh);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ================= INSERT =================
    [HttpPost]
    public async Task<IActionResult> Insert([FromBody] MasterMohDto dto)
    {
        try
        {
            using var db = CreateConnection();

            var id = await db.ExecuteScalarAsync<int>(
                "usp_Master_MOH_CRUD",
                new
                {
                    Action = "INSERT",
                    dto.MohCode,
                    dto.MohCodeOld,
                    dto.MohName,
                    dto.MohNativeName,
                    dto.MohShortName,
                    dto.ZoneID,
                    dto.ConstituencyID,
                    dto.HoId,
                    dto.JcId,
                    dto.DhoId,
                    dto.AdId,
                    dto.DdId,
                    dto.JdId
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(new { MohID = id });
        }
        catch (SqlException ex)
        {
            return StatusCode(500, new
            {
                message = "SQL Error",
                detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Server Error",
                detail = ex.Message
            });
        }
    }

    // ================= UPDATE =================
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] MasterMohDto dto)
    {
        try
        {
            using var db = CreateConnection();

            await db.ExecuteAsync(
                "usp_Master_MOH_CRUD",
                new
                {
                    Action = "UPDATE",
                    mohID = id,
                    dto.MohCode,
                    dto.MohCodeOld,
                    dto.MohName,
                    dto.MohNativeName,
                    dto.MohShortName,
                    dto.ZoneID,
                    dto.ConstituencyID,
                    dto.HoId,
                    dto.JcId,
                    dto.DhoId,
                    dto.AdId,
                    dto.DdId,
                    dto.JdId
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(new { Updated = true });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ================= DELETE =================
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            using var db = CreateConnection();

            await db.ExecuteAsync(
                "usp_Master_MOH_CRUD",
                new { Action = "DELETE", mohID = id },
                commandType: CommandType.StoredProcedure
            );

            return Ok(new { Deleted = true });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}