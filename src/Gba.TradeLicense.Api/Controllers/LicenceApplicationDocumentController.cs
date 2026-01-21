using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Dapper;
using Gba.TradeLicense.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Gba.TradeLicense.Api.Controllers
{
    [ApiController]
    [Route("api/licence-documents")]
    public class LicenceApplicationDocumentController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        private const long MAX_FILE_SIZE = 2 * 1024 * 1024; // 2 MB

        public LicenceApplicationDocumentController(
            IConfiguration config,
            IWebHostEnvironment env
        )
        {
            _config = config;
            _env = env;
        }

        private IDbConnection Db()
            => new SqlConnection(_config.GetConnectionString("Default"));

        /* =====================================================
           UPLOAD PATH: Uploads/LicenceDocuments/yyyy/MM/dd
        ===================================================== */
        private string GetUploadFolder()
        {
            var d = DateTime.Now;

            string path = Path.Combine(
                _env.ContentRootPath,     // ✅ FIX
                "Uploads", "LicenceDocuments",
                d.Year.ToString(),
                d.Month.ToString("00"),
                d.Day.ToString("00")
            );

            Directory.CreateDirectory(path);
            return path;
        }

        /* =====================================================
           INSERT / UPDATE DOCUMENT
        ===================================================== */
        [HttpPost("save-update")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SaveOrUpdate(
            [FromForm] LicenceDocumentUploadDto model
        )
        {
            if (model.File == null || model.File.Length == 0)
                return BadRequest("File is required");

            if (model.File.Length > MAX_FILE_SIZE)
                return BadRequest("File size must be less than 2 MB");

            string folder = GetUploadFolder();
            string storedName = Guid.NewGuid() + Path.GetExtension(model.File.FileName);
            string fullPath = Path.Combine(folder, storedName);

            await using (var fs = new FileStream(fullPath, FileMode.Create))
            {
                await model.File.CopyToAsync(fs);
            }

            using var db = Db();

            await db.ExecuteAsync(
                "usp_LicenceApplication_Document_CRUD",
                new
                {
                    Action = model.ApplicationDocumentID == null ? "INSERT" : "UPDATE",
                    ApplicationDocumentID = model.ApplicationDocumentID,
                    LicenceApplicationID = model.LicenceApplicationID,
                    DocumentID = model.DocumentID,
                    FileName = model.File.FileName,
                    FilePath = fullPath,
                    FileExtension = Path.GetExtension(model.File.FileName),
                    FileSizeKB = model.File.Length / 1024,
                    EntryLoginID = model.LoginID
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(new
            {
                success = true,
                message = "Document saved successfully"
            });
        }

        /* =====================================================
           GET ALL DOCUMENTS BY APPLICATION
        ===================================================== */
        [HttpGet("by-application/{licenceApplicationID:long}")]
        public async Task<IActionResult> GetByApplication(long licenceApplicationID)
        {
            using var db = Db();

            var documents = await db.QueryAsync(
                "usp_LicenceApplication_Document_CRUD",
                new
                {
                    Action = "GET",
                    LicenceApplicationID = licenceApplicationID
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(documents);
        }

        /* =====================================================
           DOWNLOAD DOCUMENT
        ===================================================== */
        [HttpGet("download/{applicationDocumentID:int}")]
        public async Task<IActionResult> Download(int applicationDocumentID)
        {
            using var db = Db();

            var doc = await db.QueryFirstOrDefaultAsync(
                @"SELECT FileName, FilePath
                  FROM Licence_Application_Document
                  WHERE ApplicationDocumentID = @ID
                    AND IsActive = 1",
                new { ID = applicationDocumentID }
            );

            if (doc == null)
                return NotFound("Document record not found");

            if (!System.IO.File.Exists(doc.FilePath))
                return NotFound($"File not found on server");

            var bytes = await System.IO.File.ReadAllBytesAsync(doc.FilePath);

            return File(
                bytes,
                "application/octet-stream",
                doc.FileName
            );
        }

        /* =====================================================
           SOFT DELETE DOCUMENT
        ===================================================== */
        [HttpDelete("{applicationDocumentID:int}")]
        public async Task<IActionResult> Delete(
            int applicationDocumentID,
            [FromQuery] int loginID
        )
        {
            using var db = Db();

            await db.ExecuteAsync(
                @"UPDATE Licence_Application_Document
                  SET IsActive = 0,
                      EntryLoginID = @LoginID,
                      EntryDate = GETDATE()
                  WHERE ApplicationDocumentID = @ID",
                new
                {
                    ID = applicationDocumentID,
                    LoginID = loginID
                }
            );

            return Ok(new
            {
                deleted = true,
                message = "Document deleted successfully"
            });
        }
    }
}
