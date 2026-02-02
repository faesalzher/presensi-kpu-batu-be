using Microsoft.EntityFrameworkCore;
using presensi_kpu_batu_be.Domain.Entities;
using presensi_kpu_batu_be.Modules.PushNotificationModule.Dto;

namespace presensi_kpu_batu_be.Modules.PushNotificationModule;

public class PushNotificationService : IPushNotificationService
{
    private readonly AppDbContext _context;

    public PushNotificationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task RegisterAsync(Guid userId, RegisterPushTokenDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FcmToken))
            throw new ArgumentException("FcmToken is required", nameof(dto.FcmToken));

        if (string.IsNullOrWhiteSpace(dto.DeviceId))
            throw new ArgumentException("DeviceId is required", nameof(dto.DeviceId));

        var fcmToken = dto.FcmToken.Trim();
        var deviceId = dto.DeviceId.Trim();
        var now = DateTime.UtcNow;

        // NOTE:
        // With Npgsql retrying execution strategy enabled (EnableRetryOnFailure),
        // user-initiated transactions (BeginTransactionAsync) are not supported unless
        // wrapped in Database.CreateExecutionStrategy().ExecuteAsync(...).
        // Here we avoid explicit transactions; EF Core will wrap SaveChanges in a DB transaction.

        // Deactivate any existing active records with same FcmToken (token can move across users/devices)
        var sameTokenActives = await _context.UserFcmTokens
            .Where(t => t.IsActive && t.FcmToken == fcmToken)
            .ToListAsync();

        if (sameTokenActives.Count > 0)
        {
            foreach (var t in sameTokenActives)
            {
                t.IsActive = false;
                t.UpdatedAt = now;
            }
        }

        // One active token per device: deactivate other active tokens for this device
        var sameDeviceActives = await _context.UserFcmTokens
            .Where(t => t.IsActive && t.DeviceId == deviceId)
            .ToListAsync();

        if (sameDeviceActives.Count > 0)
        {
            foreach (var t in sameDeviceActives)
            {
                t.IsActive = false;
                t.UpdatedAt = now;
            }
        }

        var newRow = new UserFcmToken
        {
            Guid = Guid.NewGuid(),
            UserId = userId,
            FcmToken = fcmToken,
            DeviceId = deviceId,
            IsActive = true,
            CreatedAt = now
        };

        _context.UserFcmTokens.Add(newRow);

        await _context.SaveChangesAsync();
    }

    public async Task UnregisterAsync(Guid userId)
    {
        var now = DateTime.UtcNow;

        var activeRows = await _context.UserFcmTokens
            .Where(t => t.UserId == userId && t.IsActive)
            .ToListAsync();

        if (activeRows.Count == 0)
            return;

        foreach (var t in activeRows)
        {
            t.IsActive = false;
            t.UpdatedAt = now;
        }

        await _context.SaveChangesAsync();
    }
}
