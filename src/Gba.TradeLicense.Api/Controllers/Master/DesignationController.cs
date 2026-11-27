using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Gba.TradeLicense.Api.Controllers.Master
{
    [ApiController]
    [Gba.TradeLicense.Api.Services.AdminOnly(WritesOnly = true)]
    [Gba.TradeLicense.Api.Services.StaffOnly(WritesOnly = true)]
    [Route("api/designations")]
    public class DesignationController : ControllerBase
    {
        private readonly string _connStr;

        public DesignationController(IConfiguration config)
        {
            _connStr = config.GetConnectionString("Default")
                ?? throw new InvalidOperationException("Connection string not found");
        }

        private IDbConnection Db() => new SqlConnection(_connStr);

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            using var db = Db();
            var data = await db.QueryAsync(
                @"SELECT userDesignationID, userDesignationName, isActive
                  FROM dbo.Master_Designation
                  ORDER BY userDesignationName");
            return Ok(data);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            using var db = Db();
            var data = await db.QueryFirstOrDefaultAsync(
                @"SELECT userDesignationID, userDesignationName, isActive
                  FROM dbo.Master_Designation WHERE userDesignationID = @id",
                new { id });
            return data == null ? NotFound() : Ok(data);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] DesignationDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.UserDesignationName))
                return BadRequest(new { Message = "Designation name is required." });

            using var db = Db();
            var id = await db.ExecuteScalarAsync<int>(
                @"INSERT INTO dbo.Master_Designation (userDesignationName, isActive)
                  VALUES (@name, @active);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new { name = dto.UserDesignationName.Trim(), active = dto.IsActive ? "Y" : "N" });

            return Ok(new { Message = "Designation created successfully", UserDesignationID = id });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] DesignationDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.UserDesignationName))
                return BadRequest(new { Message = "Designation name is required." });

            using var db = Db();
            var rows = await db.ExecuteAsync(
                @"UPDATE dbo.Master_Designation
                     SET userDesignationName = @name, isActive = @active
                   WHERE userDesignationID = @id",
                new { id, name = dto.UserDesignationName.Trim(), active = dto.IsActive ? "Y" : "N" });

            return rows == 0 ? NotFound() : Ok(new { Updated = true });
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            using var db = Db();
            var rows = await db.ExecuteAsync(
                @"UPDATE dbo.Master_Designation SET isActive = 'N' WHERE userDesignationID = @id",
                new { id });
            return rows == 0 ? NotFound() : Ok(new { Deleted = true });
        }
    }

    public sealed record DesignationDto(string UserDesignationName, bool IsActive);
}
