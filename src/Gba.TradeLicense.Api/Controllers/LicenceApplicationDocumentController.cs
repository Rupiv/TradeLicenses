using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Dapper;
using Gba.TradeLicense.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Gba.TradeLicense.Api.Controllers
{
    [Authorize]
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
        public async Task<IActionResult> SaveOrUpdate([FromForm] LicenceDocumentUploadDto model)
        {
            if (model.File == null || model.File.Length == 0)
                return BadRequest("File is required");

            if (model.File.Length > 2 * 1024 * 1024) // 2MB
                return BadRequest("File size must be less than 2 MB");

            // ✅ 1. Validate Extension
            var allowedExtensions = new[] { ".pdf" };
            var extension = Path.GetExtension(model.File.FileName).ToLower();

            if (!allowedExtensions.Contains(extension))
                return BadRequest("Only PDF files are allowed");

            // ❌ Block dangerous extensions explicitly
            var blockedExtensions = new[] { ".aspx", ".php", ".exe", ".js", ".bat" };
            if (blockedExtensions.Contains(extension))
                return BadRequest("Invalid file type");

            // ✅ 2. Validate MIME Type
            if (model.File.ContentType != "application/pdf")
                return BadRequest("Invalid file type (MIME mismatch)");

            // ✅ 3. Validate File Signature (MAGIC NUMBER)
            using (var stream = model.File.OpenReadStream())
            {
                byte[] buffer = new byte[4];
                await stream.ReadAsync(buffer, 0, 4);
                var header = System.Text.Encoding.ASCII.GetString(buffer);

                if (header != "%PDF")
                    return BadRequest("Invalid PDF file content");
            }

            // ✅ 4. Secure File Name (NO original name)
            string storedName = Guid.NewGuid().ToString() + ".pdf";

            // ✅ 5. Secure Folder (Make sure outside wwwroot)
            string folder = GetUploadFolder();
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            string fullPath = Path.Combine(folder, storedName);

            // ✅ 6. Save File
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

                    // ⚠️ Store original name separately (safe)
                    FileName = Path.GetFileName(model.File.FileName),

                    FilePath = fullPath,

                    // Always force .pdf
                    FileExtension = ".pdf",

                    FileSizeKB = model.File.Length / 1024,
                    EntryLoginID = model.LoginID
                },
                commandType: CommandType.StoredProcedure
            );

            return Ok(new
            {
                success = true,
                message = "Document uploaded securely"
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

            // 🔐 Sanitize response (NO LOGIC CHANGE)
            var safeDocuments = documents.Select(d =>
            {
                var dict = (IDictionary<string, object>)d;

                // Handle FilePath safely (if exists)
                if (dict.ContainsKey("FilePath") && dict["FilePath"] != null)
                {
                    var fullPath = dict["FilePath"].ToString();

                    if (!string.IsNullOrEmpty(fullPath))
                    {
                        // Normalize path (avoid slash issues)
                        fullPath = fullPath.Replace("\\", "/");

                        // Remove sensitive server path
                        var index = fullPath.IndexOf("/uploads", StringComparison.OrdinalIgnoreCase);

                        if (index >= 0)
                        {
                            dict["FilePath"] = fullPath.Substring(index); // ✅ "/uploads/..."
                        }
                        else
                        {
                            dict["FilePath"] = null; // fallback (safe)
                        }
                    }
                }

                return dict;
            });

            return Ok(safeDocuments);
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
