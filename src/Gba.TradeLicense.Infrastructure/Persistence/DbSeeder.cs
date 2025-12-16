using Gba.TradeLicense.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gba.TradeLicense.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct)
    {
        await db.Database.MigrateAsync(ct);

        var roles = new[] { "Trader", "Approver", "SeniorApprover", "Admin" };
        foreach (var r in roles)
        {
            if (!await db.Roles.AnyAsync(x => x.Name == r, ct))
                db.Roles.Add(new Role { Name = r });
        }
        await db.SaveChangesAsync(ct);

        // Demo users (change immediately in production)
        await EnsureUserAsync(db, ct,
            fullName: "Demo Trader",
            email: "trader@gba.local",
            phone: "9999999999",
            password: "Trader@123",
            role: "Trader");

        await EnsureUserAsync(db, ct,
            fullName: "Demo Approver",
            email: "approver@gba.local",
            phone: "9999999998",
            password: "Approver@123",
            role: "Approver");

        await EnsureUserAsync(db, ct,
            fullName: "Demo Senior",
            email: "senior@gba.local",
            phone: "9999999997",
            password: "Senior@123",
            role: "SeniorApprover");

        await EnsureUserAsync(db, ct,
            fullName: "Demo Admin",
            email: "admin@gba.local",
            phone: "9999999996",
            password: "Admin@123",
            role: "Admin");
    }

    private static async Task EnsureUserAsync(AppDbContext db, CancellationToken ct,
        string fullName, string email, string phone, string password, string role)
    {
        var existing = await db.Users
            .Include(x => x.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(x => x.Phone == phone, ct);

        if (existing is null)
        {
            var user = new User
            {
                FullName = fullName,
                Email = email,
                Phone = phone,
                IsActive = true
            };

            var hasher = new PasswordHasher<User>();
            user.PasswordHash = hasher.HashPassword(user, password);

            var r = await db.Roles.FirstAsync(x => x.Name == role, ct);
            user.UserRoles.Add(new UserRole { User = user, Role = r });

            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
            return;
        }

        if (!existing.UserRoles.Any(x => x.Role.Name == role))
        {
            var r = await db.Roles.FirstAsync(x => x.Name == role, ct);
            existing.UserRoles.Add(new UserRole { UserId = existing.Id, RoleId = r.Id });
            await db.SaveChangesAsync(ct);
        }
    }
}
