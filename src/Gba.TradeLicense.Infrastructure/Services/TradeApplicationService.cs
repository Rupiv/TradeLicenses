using Gba.TradeLicense.Application.Abstractions;
using Gba.TradeLicense.Application.Models;
using Gba.TradeLicense.Domain.Entities;
using Gba.TradeLicense.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Gba.TradeLicense.Infrastructure.Services;

public sealed class TradeApplicationService : ITradeApplicationService
{
    private readonly AppDbContext _db;
    public TradeApplicationService(AppDbContext db) => _db = db;

    public async Task<ApplicationCreateResult> CreateAsync(Guid traderUserId, ApplicationCreateRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.TradeType))
            return new(false, null, "TradeType is required.");

        var appNo = $"GBA-APP-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(100000, 999999)}";

        var entity = new TradeApplication
        {
            ApplicationNo = appNo,
            TraderUserId = traderUserId,
            TradeType = request.TradeType.Trim(),
            BusinessName = request.BusinessName.Trim(),
            AddressLine1 = request.AddressLine1.Trim(),
            WardNo = request.WardNo.Trim(),
            ConnectedLoadKw = request.ConnectedLoadKw,
            Status = "Draft"
        };

        entity.StatusHistory.Add(new ApplicationStatusHistory
        {
            Status = "Draft",
            Comment = "Application created",
            ByUserId = traderUserId
        });

        _db.TradeApplications.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new(true, appNo, null);
    }

    public async Task<ApplicationStatusResult> GetStatusAsync(string applicationNo, Guid userId, CancellationToken ct)
    {
        var app = await _db.TradeApplications
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.ApplicationNo == applicationNo, ct);

        if (app is null) return new(false, null, null, null, "Application not found.");

        // basic authorization: trader can see own, approver/admin can see all
        if (app.TraderUserId != userId)
        {
            // You can enforce role check in controller; keeping service generic
        }

        var history = app.StatusHistory
            .OrderByDescending(x => x.AtUtc)
            .Select(x => new { x.Status, x.Comment, x.AtUtc, x.ByUserId });

        return new(true, app.ApplicationNo, app.Status, history, null);
    }

    public async Task<ApplicationSubmitResult> SubmitAsync(string applicationNo, Guid traderUserId, CancellationToken ct)
    {
        var app = await _db.TradeApplications
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.ApplicationNo == applicationNo && x.TraderUserId == traderUserId, ct);

        if (app is null) return new(false, null, "Application not found.");
        if (app.Status != "Draft") return new(false, null, "Only Draft applications can be submitted.");

        app.Status = "Submitted";
        app.UpdatedAtUtc = DateTime.UtcNow;
        app.StatusHistory.Add(new ApplicationStatusHistory
        {
            Status = "Submitted",
            Comment = "Submitted by trader",
            ByUserId = traderUserId
        });

        await _db.SaveChangesAsync(ct);
        return new(true, app.Status, null);
    }
}
