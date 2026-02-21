using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Gba.TradeLicense.Application.Models;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;

[ApiController]
[Route("api/licence-application")]
public class LicenceApplicationController : ControllerBase
{
    private readonly IConfiguration _config;

    public LicenceApplicationController(IConfiguration config)
    {
        _config = config;
    }

    private IDbConnection CreateConnection()
        => new SqlConnection(_config.GetConnectionString("Default"));

    // ================= INSERT DRAFT =================
    [HttpPost("draft")]
    public async Task<IActionResult> InsertDraft(
        [FromBody] LicenceApplicationUpsertDto dto,
        CancellationToken ct)
    {
        using var db = CreateConnection();

        var id = await db.ExecuteScalarAsync<long>(
            "usp_LicenceApplication_CRUD",
            new
            {
                Action = "INSERT",

                dto.FinanicalYearID,
                dto.TradeTypeID,

                BescomRRNumber = dto.BescomRRNumber ?? "",
                GSTNumber = dto.GSTNumber ?? "",
                PANNumber = dto.PANNumber ?? "",


                dto.LicenceFromDate,
                dto.LicenceToDate,

                dto.TradeLicenceID,
                dto.MohID,

                dto.LoginID,
                dto.EntryOriginLoginID,
                dto.InspectingOfficerID,

                LicenseType = dto.LicenseType ?? "",
                dto.ApplicantRepersenting,
                JathaStatus = dto.JathaStatus ?? "",
                dto.DocsSubmitted,
                ChallanNo = dto.ChallanNo ?? "",
                dto.NoOfYearsApplied
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { LicenceApplicationID = id });
    }

    // ================= UPDATE DRAFT =================
    [HttpPut("draft/{id:long}")]
    public async Task<IActionResult> UpdateDraft(
        long id,
        [FromBody] LicenceApplicationUpsertDto dto,
        CancellationToken ct)
    {
        using var db = CreateConnection();

        await db.ExecuteAsync(
            "usp_LicenceApplication_CRUD",
            new
            {
                Action = "UPDATE",
                LicenceApplicationID = id,

                dto.TradeTypeID,

                BescomRRNumber = dto.BescomRRNumber ?? "",
                GSTNumber = dto.GSTNumber ?? "",
                PANNumber = dto.PANNumber ?? "",

                dto.LicenceFromDate,
                dto.LicenceToDate,

                dto.InspectingOfficerID,
                LicenseType = dto.LicenseType ?? "",
                dto.ApplicantRepersenting,
                JathaStatus = dto.JathaStatus ?? "",
                dto.DocsSubmitted,
                ChallanNo = dto.ChallanNo ?? "",
                dto.NoOfYearsApplied
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { Updated = true });
    }

    // ================= GET BY ID =================
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        using var db = CreateConnection();

        var result = await db.QueryFirstOrDefaultAsync<dynamic>(
            "usp_LicenceApplication_CRUD",
            new
            {
                Action = "GET_BY_ID",
                LicenceApplicationID = id
            },
            commandType: CommandType.StoredProcedure
        );

        if (result == null)
            return NotFound();

        return Ok(result);
    }
    public class DbResponse
    {
        public bool Submitted { get; set; }
        public string Message { get; set; }
        public string ApplicationNumber { get; set; }
        public long? LicenceApplicationID { get; set; }
        public int? ErrorLine { get; set; }
        public string ErrorProcedure { get; set; }
    }

    // ================= SEARCH =================
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken ct)
    {
        using var db = CreateConnection();

        var list = await db.QueryAsync<dynamic>(
            "usp_LicenceApplication_CRUD",
            new
            {
                Action = "SEARCH",
                SearchText = q
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(list);
    }

    // ================= DELETE DRAFT =================
    [HttpDelete("draft/{id:long}")]
    public async Task<IActionResult> DeleteDraft(long id, CancellationToken ct)
    {
        using var db = CreateConnection();

        await db.ExecuteAsync(
            "usp_LicenceApplication_CRUD",
            new
            {
                Action = "DELETE_TEMP",
                LicenceApplicationID = id
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new { Deleted = true });
    }

    // ================= PAYMENT SUCCESS =================
    [HttpPost("payment-success/{id:long}")]
    public async Task<IActionResult> PaymentSuccess(long id, CancellationToken ct)
    {
        using var db = CreateConnection();

        var result = await db.QuerySingleAsync<dynamic>(
            "usp_LicenceApplication_CRUD",
            new
            {
                Action = "PAYMENT_SUCCESS",
                LicenceApplicationID = id
            },
            commandType: CommandType.StoredProcedure
        );

        return Ok(new
        {
            PaymentSuccess = true,
            result.ReceiptNumber,
            result.ReceiptSecurityCode
        });
    }

    // ================= FINAL SUBMIT =================
    [HttpPost("submit/{id:long}")]
    public async Task<IActionResult> FinalSubmit(long id, CancellationToken ct)
    {
        using var db = CreateConnection();

        var result = await db.QueryFirstOrDefaultAsync<DbResponse>(
            "usp_LicenceApplication_CRUD",
            new
            {
                Action = "FINAL_SUBMIT",
                licenceApplicationID = id
            },
            commandType: CommandType.StoredProcedure
        );

        // safety check (DB returned nothing)
        if (result == null)
        {
            return BadRequest(new DbResponse
            {
                Submitted = false,
                Message = "No response from database."
            });
        }

        // return exactly what SQL sends
        return Ok(result);
    }




    // ================= GET BY LOGIN (PAGINATED) =================
    // ================= GET BY LOGIN (PAGINATED) =================
    [HttpGet("by-login/{loginId:int}")]
    public async Task<IActionResult> GetByLogin(
     int loginId,
     [FromQuery] int pageNumber = 1,
     [FromQuery] int pageSize = 10,
     CancellationToken ct = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize <= 0) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        using var db = CreateConnection();

        var rows = (await db.QueryAsync(
            "usp_LicenceApplication_GetByLogin_Paged",
            new
            {
                UserID = loginId,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            commandType: CommandType.StoredProcedure
        )).ToList();

        if (!rows.Any())
        {
            return Ok(new
            {
                TotalRecords = 0,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Data = new List<object>()
            });
        }

        int totalRecords = (int)rows.First().TotalRecords;

        var groupedData = rows
            .GroupBy(r => (long)r.licenceApplicationID)
            .Select(g =>
            {
                var first = g.First();

                return new
                {
                    // ================= USER DETAILS =================
                    first.UserID,
                    first.FullName,
                    first.UserMobile,
                    first.UserEmail,
                    first.UserCreatedDate,

                    // ================= APPLICATION DETAILS =================
                    first.licenceApplicationID,
                    first.applicationNumber,
                    first.finanicalYearID,
                    first.tradeTypeID,
                    first.bescomRRNumber,
                    first.GSTNumber,
                    first.PANNumber,
                    first.applicationSubmitDate,
                    first.applicationEntryDate,
                    first.acknowledgementNumber,
                    first.acknowledgementDate,
                    first.receiptNumber,
                    first.receiptDate,
                    first.licenceFromDate,
                    first.licenceToDate,
                    first.NoOfYearsApplied,
                    first.docsSubmitted,
                    first.jathaStatus,
                    first.ChallanNo,
                    first.ApplicationIsActive,

                    // ================= STATUS =================
                    first.licenceApplicationStatusName,
                    first.CurrentStatusDescription,
                    first.licenceStatusName,

                    // ================= LICENCE MASTER =================
                    first.tradeLicenceID,
                    first.applicantName,
                    first.tradeName,
                    first.doorNumber,
                    first.address1,
                    first.address2,
                    first.address3,
                    first.pincode,
                    first.ApplicantMobile,
                    first.ApplicantEmail,
                    first.licenceNumber,
                    first.licenceCommencementDate,

                    // ================= MOH =================
                    first.mohcd,
                    first.mohname,
                    first.mohshortname,
                    first.MohAddress,

                    // ================= GEO =================
                    first.Latitude,
                    first.Longitude,
                    first.RoadID,
                    first.RoadWidthMtrs,
                    first.RoadCategoryCode,
                    first.RoadCategory,
                    first.GeoConfirmed,

                    // ================= DOCUMENTS =================
                    Documents = g
                        .Where(d => d.ApplicationDocumentID != null)
                        .GroupBy(d => d.ApplicationDocumentID)
                        .Select(d => new
                        {
                            ApplicationDocumentID = d.First().ApplicationDocumentID,
                            documentName = d.First().documentName,
                            FileName = d.First().FileName,
                            FilePath = d.First().FilePath,
                            FileExtension = d.First().FileExtension,
                            FileSizeKB = d.First().FileSizeKB,
                            DocumentIsActive = d.First().DocumentIsActive
                        })
                        .ToList()
                };
            })
            .ToList();

        return Ok(new
        {
            TotalRecords = totalRecords,
            PageNumber = pageNumber,
            PageSize = pageSize,
            Data = groupedData
        });
    }

    // ================= GET BY LOGIN (TEMP) =================
    [HttpGet("by-temp-login/{loginId:int}")]
    public async Task<IActionResult> GetByTempLogin(
        int loginId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        using var db = CreateConnection();

        using var multi = await db.QueryMultipleAsync(
            "usp_LicenceApplicationTemp_GetByLogin_Paged",
            new
            {
                LoginID = loginId,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            commandType: CommandType.StoredProcedure
        );

        var totalRecords = await multi.ReadFirstAsync<int>();
        var data = (await multi.ReadAsync()).ToList();

        return Ok(new
        {
            TotalRecords = totalRecords,
            PageNumber = pageNumber,
            PageSize = pageSize,
            Data = data
        });
    }

    // ================= GET ALL PAGED =================
    [HttpGet("paged")]
    public async Task<IActionResult> GetAllApplicationsPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        using var db = CreateConnection();

        using var multi = await db.QueryMultipleAsync(
            "usp_LicenceApplication_GetAll_Paged",
            new
            {
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            commandType: CommandType.StoredProcedure
        );

        var totalRecords = await multi.ReadFirstAsync<int>();
        var applications = await multi.ReadAsync();

        return Ok(new
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = totalRecords,
            TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
            Data = applications
        });
    }

    // ================= TRACK STATUS =================
    [HttpGet("current-status/{licenceApplicationID:long}")]
    public async Task<IActionResult> GetCurrentStatus(long licenceApplicationID)
    {
        using var db = CreateConnection();

        var status = await db.QueryFirstOrDefaultAsync(
            "usp_LicenceApplication_TrackStatus",
            new { LicenceApplicationID = licenceApplicationID },
            commandType: CommandType.StoredProcedure
        );

        if (status == null)
            return NotFound(new { message = "Application not found" });

        return Ok(status);
    }
}
