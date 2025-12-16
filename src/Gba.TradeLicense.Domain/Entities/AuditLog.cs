namespace Gba.TradeLicense.Domain.Entities;

public sealed class AuditLog : BaseEntity
{
    public Guid? UserId { get; set; }
    public string Action { get; set; } = "";
    public string Path { get; set; } = "";
    public string Method { get; set; } = "";
    public int StatusCode { get; set; }
    public string? IpAddress { get; set; }
    public string? RequestBody { get; set; }
    public string? ResponseBody { get; set; }
}
