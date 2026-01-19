using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Gba.TradeLicense.Api.Controllers
{
    [ApiController]
    [Route("api/office-types")]
    public class OfficeTypeController : ControllerBase
    {
        private readonly IConfiguration _config;

        public OfficeTypeController(IConfiguration config)
        {
            _config = config;
        }

        private IDbConnection Db()
            => new SqlConnection(_config.GetConnectionString("Default"));

        /* ======================================================
           GET ALL (PAGINATION)
        ====================================================== */
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            using var db = Db();

            using var multi = await db.QueryMultipleAsync(
                "usp_MasterOfficeType_CRUD",
                new
                {
                    Action = "GET_ALL",
                    PageNumber = pageNumber,
                    PageSize = pageSize
                },
                commandType: CommandType.StoredProcedure
            );

            var totalCount = await multi.ReadFirstAsync<int>();
            var data = (await multi.ReadAsync()).ToList();

            return Ok(new
            {
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Data = data
            });
        }

        /* ======================================================
           GET BY ID
        ====================================================== */
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            using var db = Db();

            var data = await db.QueryFirstOrDefaultAsync(
                "usp_MasterOfficeType_CRUD",
                new
                {
                    Action = "GET_BY_ID",
                    officeTypeID = id
                },
                commandType: CommandType.StoredProcedure
            );

            if (data == null)
                return NotFound();

            return Ok(data);
        }

        /* ======================================================
           SEARCH
        ====================================================== */
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string? text)
        {
            using var db = Db();

            var data = await db.QueryAsync(
                "usp_MasterOfficeType_CRUD",
                new
                {
                    Action = "SEARCH",
                    searchText = text
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(data);
        }

        /* ======================================================
           INSERT
        ====================================================== */
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] OfficeTypeDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.OfficeTypeName))
                return BadRequest("Office type name is required");

            using var db = Db();

            var id = await db.ExecuteScalarAsync<int>(
                "usp_MasterOfficeType_CRUD",
                new
                {
                    Action = "INSERT",
                    officeTypeName = dto.OfficeTypeName,
                    isActive = dto.IsActive
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(new
            {
                Message = "Office type created successfully",
                OfficeTypeID = id
            });
        }

        /* ======================================================
           UPDATE
        ====================================================== */
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] OfficeTypeDto dto)
        {
            using var db = Db();

            await db.ExecuteAsync(
                "usp_MasterOfficeType_CRUD",
                new
                {
                    Action = "UPDATE",
                    officeTypeID = id,
                    officeTypeName = dto.OfficeTypeName,
                    isActive = dto.IsActive
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(new { Updated = true });
        }

        /* ======================================================
           DELETE (SOFT DELETE)
        ====================================================== */
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            using var db = Db();

            await db.ExecuteAsync(
                "usp_MasterOfficeType_CRUD",
                new
                {
                    Action = "DELETE",
                    officeTypeID = id
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(new { Deleted = true });
        }
    }

    /* ======================================================
       DTO
    ====================================================== */
    public sealed record OfficeTypeDto
    (
        string OfficeTypeName,
        bool IsActive
    );
}
