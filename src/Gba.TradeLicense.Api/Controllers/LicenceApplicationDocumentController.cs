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

            // Inspection photos (DocumentID 10) are images taken on site; every other document is a PDF.
            bool isInspectionPhoto = model.DocumentID == InspectionPhotoDocumentId;

            long maxBytes = isInspectionPhoto ? 5 * 1024 * 1024 : 2 * 1024 * 1024;
            if (model.File.Length > maxBytes)
                return BadRequest(isInspectionPhoto ? "Photo size must be less than 5 MB" : "File size must be less than 2 MB");

            // ✅ 1. Validate Extension
            var extension = Path.GetExtension(model.File.FileName).ToLower();
            if (extension == ".jpeg") extension = ".jpg";

            var allowedExtensions = isInspectionPhoto ? PhotoTypes.Keys.ToArray() : new[] { ".pdf" };
            if (!allowedExtensions.Contains(extension))
                return BadRequest(isInspectionPhoto ? "Only JPG, PNG or WEBP photos are allowed" : "Only PDF files are allowed");

            // ❌ Block dangerous extensions explicitly
            var blockedExtensions = new[] { ".aspx", ".php", ".exe", ".js", ".bat" };
            if (blockedExtensions.Contains(extension))
                return BadRequest("Invalid file type");

            // ✅ 2. Validate MIME Type
            var expectedMime = isInspectionPhoto ? PhotoTypes[extension] : "application/pdf";
            var actualMime = (model.File.ContentType ?? string.Empty).ToLowerInvariant();
            if (actualMime != expectedMime && !(extension == ".jpg" && actualMime == "image/jpg"))
                return BadRequest("Invalid file type (MIME mismatch)");

            // ✅ 3. Validate File Signature (MAGIC NUMBER)
            using (var stream = model.File.OpenReadStream())
            {
                byte[] buffer = new byte[12];
                int read = await stream.ReadAsync(buffer, 0, buffer.Length);

                if (!HasValidSignature(extension, buffer, read))
                    return BadRequest(isInspectionPhoto ? "Invalid image file content" : "Invalid PDF file content");
            }

            // ✅ 4. Secure File Name (NO original name)
            string storedName = Guid.NewGuid().ToString() + extension;

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

                    // Validated type (.pdf, or .jpg/.png/.webp for inspection photos)
                    FileExtension = extension,

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

            // Real type so photos show as images and PDFs open in the viewer
            var storedExt = Path.GetExtension((string)doc.FilePath).ToLowerInvariant();
            var contentType = storedExt == ".pdf" ? "application/pdf"
                : PhotoTypes.TryGetValue(storedExt, out var mime) ? mime
                : "application/octet-stream";

            return File(
                bytes,
                contentType,
                doc.FileName
            );
        }

        /* =====================================================
           FILE TYPE HELPERS
        ===================================================== */
        private const int InspectionPhotoDocumentId = 10;

        private static readonly Dictionary<string, string> PhotoTypes = new()
        {
            [".jpg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp",
        };

        private static bool HasValidSignature(string extension, byte[] b, int read) => extension switch
        {
            ".pdf"  => read >= 4 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46,            // %PDF
            ".jpg"  => read >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF,
            ".png"  => read >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47
                                 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A,
            ".webp" => read >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F'
                                  && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P',
            _ => false
        };

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
