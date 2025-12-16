using Gba.TradeLicense.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gba.TradeLicense.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<TradeApplication> TradeApplications => Set<TradeApplication>();
    public DbSet<ApplicationStatusHistory> ApplicationStatusHistory => Set<ApplicationStatusHistory>();
    public DbSet<ApplicationDocument> ApplicationDocuments => Set<ApplicationDocument>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Inspection> Inspections => Set<Inspection>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(b =>
        {
            b.HasIndex(x => x.Email).IsUnique(false);
            b.HasIndex(x => x.Phone).IsUnique(true);
            b.Property(x => x.FullName).HasMaxLength(200);
            b.Property(x => x.Phone).HasMaxLength(20);
            b.Property(x => x.Email).HasMaxLength(200);
        });

        modelBuilder.Entity<Role>(b =>
        {
            b.HasIndex(x => x.Name).IsUnique(true);
            b.Property(x => x.Name).HasMaxLength(64);
        });

        modelBuilder.Entity<UserRole>(b =>
        {
            b.HasKey(x => new { x.UserId, x.RoleId });
            b.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId);
            b.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId);
        });

        modelBuilder.Entity<TradeApplication>(b =>
        {
            b.HasIndex(x => x.ApplicationNo).IsUnique(true);
            b.Property(x => x.ApplicationNo).HasMaxLength(40);
            b.Property(x => x.Status).HasMaxLength(30);
            b.HasOne(x => x.TraderUser).WithMany().HasForeignKey(x => x.TraderUserId);
        });

        modelBuilder.Entity<ApplicationStatusHistory>(b =>
        {
            b.Property(x => x.Status).HasMaxLength(30);
            b.HasOne(x => x.TradeApplication).WithMany(x => x.StatusHistory).HasForeignKey(x => x.TradeApplicationId);
        });

        modelBuilder.Entity<ApplicationDocument>(b =>
        {
            b.Property(x => x.DocType).HasMaxLength(100);
            b.HasOne(x => x.TradeApplication).WithMany(x => x.Documents).HasForeignKey(x => x.TradeApplicationId);
        });

        modelBuilder.Entity<Payment>(b =>
        {
            b.Property(x => x.Status).HasMaxLength(30);
            b.HasOne(x => x.TradeApplication).WithMany(x => x.Payments).HasForeignKey(x => x.TradeApplicationId);
        });

        modelBuilder.Entity<Inspection>(b =>
        {
            b.Property(x => x.Result).HasMaxLength(30);
            b.HasOne(x => x.TradeApplication).WithMany(x => x.Inspections).HasForeignKey(x => x.TradeApplicationId);
        });
    }
}
