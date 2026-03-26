using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Gba.TradeLicense.Application.Models;
using Gba.TradeLicense.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gba.TradeLicense.Api.Controllers.Licence
{
    [ApiController]
    [Route("api/licence/certificate/generated")]
    public class GeneratedLicenceCertificateController : ControllerBase
    {
        private const int MaxFileBytes = 15 * 1024 * 1024; // 15 MB decoded
        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf"
        };

        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<GeneratedLicenceCertificateController> _logger;

        public GeneratedLicenceCertificateController(
            AppDbContext db,
            IWebHostEnvironment env,
            ILogger<GeneratedLicenceCertificateController> logger)
        {
            _db = db;
            _env = env;
            _logger = logger;
        }

        [HttpPost("save")]
        [RequestSizeLimit(25 * 1024 * 1024)] // request body limit (base64 is bigger than binary)
        public async Task<IActionResult> Save([FromBody] SaveGeneratedLicenceRequest request, CancellationToken ct)
        {
            if (request is null) return BadRequest("Payload is required.");
            if (request.LicenceApplicationID <= 0) return BadRequest("Invalid licenceApplicationID.");
            if (string.IsNullOrWhiteSpace(request.FileContentBase64)) return BadRequest("fileContentBase64 is required.");
            if (!AllowedContentTypes.Contains(request.ContentType ?? string.Empty)) return BadRequest("Unsupported contentType.");

            byte[] fileBytes;
            try
            {
                fileBytes = Convert.FromBase64String(request.FileContentBase64.Trim());
            }
            catch
            {
                return BadRequest("Invalid base64 content.");
            }

            if (fileBytes.Length == 0) return BadRequest("Empty file.");
            if (fileBytes.Length > MaxFileBytes) return BadRequest($"File too large. Max {MaxFileBytes / (1024 * 1024)} MB.");

            // PDF magic header check: %PDF
            if (!(fileBytes.Length >= 4 &&
                  fileBytes[0] == 0x25 && fileBytes[1] == 0x50 &&
                  fileBytes[2] == 0x44 && fileBytes[3] == 0x46))
            {
                return BadRequest("Uploaded file is not a valid PDF.");
            }

            var webRoot = _env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
            var folder = Path.Combine(webRoot, "uploads", "licence-certificates", request.LicenceApplicationID.ToString());
            Directory.CreateDirectory(folder);

            var safeAppNo = SanitizeFileToken(request.ApplicationNumber, 60);
            var fileName = BuildSafeFileName(request.FileName, safeAppNo);
            var physicalPath = Path.Combine(folder, fileName);

            await System.IO.File.WriteAllBytesAsync(physicalPath, fileBytes, ct);

            var relativePath = Path.GetRelativePath(webRoot, physicalPath).Replace("\\", "/");
            var sha256 = Convert.ToHexString(SHA256.HashData(fileBytes));

            var existing = await _db.GeneratedLicenceCertificate
                .FirstOrDefaultAsync(x => x.LicenceApplicationID == request.LicenceApplicationID, ct);

            if (existing is null)
            {
                existing = new GeneratedLicenceCertificate
                {
                    LicenceApplicationID = request.LicenceApplicationID,
                    ApplicationNumber = request.ApplicationNumber?.Trim() ?? string.Empty,
                    FileName = fileName,
                    FilePath = relativePath,
                    ContentType = "application/pdf",
                    FileSizeBytes = fileBytes.Length,
                    FileHash = sha256,
                    CreatedOn = DateTime.UtcNow
                };
                _db.GeneratedLicenceCertificate.Add(existing);
            }
            else
            {
                existing.ApplicationNumber = request.ApplicationNumber?.Trim() ?? existing.ApplicationNumber;
                existing.FileName = fileName;
                existing.FilePath = relativePath;
                existing.ContentType = "application/pdf";
                existing.FileSizeBytes = fileBytes.Length;
                existing.FileHash = sha256;
                existing.ModifiedOn = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(ct);

            return Ok(new
            {
                existing.LicenceApplicationID,
                existing.ApplicationNumber,
                existing.FileName,
                existing.FilePath,
                existing.ContentType,
                existing.FileSizeBytes
            });
        }

        [HttpGet("{licenceApplicationID:int}/download")]
        public async Task<IActionResult> Download(int licenceApplicationID, CancellationToken ct)
        {
            try
            {
                var cert = await _db.GeneratedLicenceCertificate
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.LicenceApplicationID == licenceApplicationID, ct);

                if (cert is null)
                    return NotFound("Generated licence not found.");

                var webRoot = _env.WebRootPath;
                if (string.IsNullOrWhiteSpace(webRoot))
                    webRoot = @"C:\inetpub\vhosts\pickitover.com\httpdocs\api\wwwroot"; // production fallback

                var normalizedDbPath = (cert.FilePath ?? string.Empty)
                    .Replace("/", Path.DirectorySeparatorChar.ToString())
                    .TrimStart(Path.DirectorySeparatorChar);

                var appBase = AppContext.BaseDirectory;

                var candidatePaths = new List<string>
        {
            Path.GetFullPath(Path.Combine(webRoot, normalizedDbPath)),
            Path.GetFullPath(Path.Combine(webRoot, "uploads", "licence-certificates", licenceApplicationID.ToString(), cert.FileName ?? string.Empty)),
            Path.GetFullPath(Path.Combine(appBase, "wwwroot", normalizedDbPath)),
            Path.GetFullPath(Path.Combine(appBase, "wwwroot", "uploads", "licence-certificates", licenceApplicationID.ToString(), cert.FileName ?? string.Empty))
        };

                // path traversal guard
                var safeRoots = new[]
                {
            Path.GetFullPath(webRoot),
            Path.GetFullPath(Path.Combine(appBase, "wwwroot"))
        };

                var existingPath = candidatePaths
                    .Where(p => safeRoots.Any(root => p.StartsWith(root, StringComparison.OrdinalIgnoreCase)))
                    .FirstOrDefault(System.IO.File.Exists);

                _logger.LogInformation(
                    "Download cert AppId={AppId}, WebRoot={WebRoot}, DBPath={DBPath}, Resolved={Resolved}",
                    licenceApplicationID, webRoot, cert.FilePath, existingPath ?? "NOT_FOUND");

                if (existingPath is null)
                    return NotFound("File not found on server.");

                var contentType = string.IsNullOrWhiteSpace(cert.ContentType)
                    ? "application/pdf"
                    : cert.ContentType;

                var downloadName = string.IsNullOrWhiteSpace(cert.FileName)
                    ? $"Licence_{licenceApplicationID}.pdf"
                    : cert.FileName;

                var stream = new FileStream(
                    existingPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 64 * 1024,
                    useAsync: true);

                // DO NOT wrap stream in using/await using
                return File(stream, contentType, downloadName, enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Download failed for LicenceApplicationID={AppId}", licenceApplicationID);
                return StatusCode(500, "Failed to download certificate.");
            }
        }



        private static string BuildSafeFileName(string? requestedName, string safeAppNo)
        {
            var baseName = Path.GetFileNameWithoutExtension(requestedName ?? string.Empty);
            baseName = string.IsNullOrWhiteSpace(baseName) ? $"Licence_{safeAppNo}" : SanitizeFileToken(baseName, 120);
            return $"{baseName}.pdf";
        }

        private static string SanitizeFileToken(string? value, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(value)) return "NA";
            var cleaned = new string(value.Trim().Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_').ToArray());
            return cleaned.Length <= maxLen ? cleaned : cleaned[..maxLen];
        }
    }

    public sealed class SaveGeneratedLicenceRequest
    {
        [Required]
        public int LicenceApplicationID { get; set; }

        [Required, MaxLength(100)]
        public string ApplicationNumber { get; set; } = string.Empty;

        [MaxLength(260)]
        public string FileName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string ContentType { get; set; } = "application/pdf";

        [Required]
        public string FileContentBase64 { get; set; } = string.Empty;
    }
}
