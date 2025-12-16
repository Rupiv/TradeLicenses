namespace Gba.TradeLicense.Domain.Entities;

public sealed class Role : BaseEntity
{
    public string Name { get; set; } = "";
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
