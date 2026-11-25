namespace Gba.TradeLicense.Api.Services
{
    /// <summary>
    /// Physical server paths (drive letters, IIS folders) must not reach the browser. Documents are
    /// downloaded by ApplicationDocumentID; the FilePath field is kept in responses for
    /// compatibility, reduced to the application-level part from "/Uploads/..." (the same form
    /// licence-documents/by-application has always returned), or null.
    /// </summary>
    public static class SafePaths
    {
        public static string? ToPublic(object? storedPath)
        {
            var path = storedPath?.ToString();
            if (string.IsNullOrWhiteSpace(path)) return null;

            path = path.Replace('\\', '/');
            var i = path.IndexOf("/uploads", StringComparison.OrdinalIgnoreCase);
            return i >= 0 ? path[i..] : null;
        }

        /// <summary>Rewrites the FilePath column of Dapper rows (dynamic / dictionary) in place.</summary>
        public static void SanitizeRows(IEnumerable<object?> rows)
        {
            foreach (var row in rows)
            {
                if (row is not IDictionary<string, object> dict) continue;
                foreach (var key in dict.Keys.Where(k => string.Equals(k, "FilePath", StringComparison.OrdinalIgnoreCase)).ToList())
                    dict[key] = ToPublic(dict[key])!;
            }
        }
    }
}
