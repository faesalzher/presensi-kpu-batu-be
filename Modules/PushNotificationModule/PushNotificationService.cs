using Microsoft.EntityFrameworkCore;
using presensi_kpu_batu_be.Domain.Entities;
using presensi_kpu_batu_be.Modules.LeaveRequestModule;
using presensi_kpu_batu_be.Modules.PushNotificationModule.Dto;
using presensi_kpu_batu_be.Modules.SystemSettingModule.Dto;
using presensi_kpu_batu_be.Modules.SystemSettingModule.GeneralSetting;

namespace presensi_kpu_batu_be.Modules.PushNotificationModule;

public class PushNotificationService : IPushNotificationService
{
    private readonly AppDbContext _context;
    private readonly PushSenderService _pushSenderService;
    private readonly ITimeProviderService _timeProviderService;
    private readonly IGeneralSettingService _settingService;
    private readonly ILeaveRequestService _leaveRequestService;
    private readonly ILogger<PushNotificationService> _logger;

    public PushNotificationService(
        AppDbContext context,
        PushSenderService pushSenderService,
        ITimeProviderService timeProviderService,
        IGeneralSettingService settingService,
        ILeaveRequestService leaveRequestService,
        ILogger<PushNotificationService> logger)
    {
        _context = context;
        _pushSenderService = pushSenderService;
        _timeProviderService = timeProviderService;
        _settingService = settingService;
        _leaveRequestService = leaveRequestService;
        _logger = logger;
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

    public async Task<PushRegistrationStatusResponse> GetRegistrationStatusAsync(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("DeviceId is required", nameof(deviceId));

        var trimmedDeviceId = deviceId.Trim();

        var registration = await _context.UserFcmTokens
            .Where(t => t.DeviceId == trimmedDeviceId && t.IsActive)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        if (registration == null)
        {
            return new PushRegistrationStatusResponse
            {
                IsRegistered = false,
                DeviceId = null,
                RegisteredAt = null
            };
        }

        return new PushRegistrationStatusResponse
        {
            IsRegistered = true,
            DeviceId = registration.DeviceId,
            RegisteredAt = registration.CreatedAt
        };
    }

    public async Task<BlastPushNotificationResponse> BlastAsync(BlastPushNotificationDto dto)
    {
        ValidateBlast(dto);

        var type = dto.Type.Trim().ToUpperInvariant();
        var title = GetDefaultTitle(type);
        var body = GetDefaultBody(type);

        var tz = await GetTimeZoneAsync();
        var nowUtc = await _timeProviderService.NowAsync();
        var todayLocal = TimeZoneInfo.ConvertTime(nowUtc, tz);
        var today = DateOnly.FromDateTime(todayLocal);

        // Holiday/weekend => do not send at all
        if (!await _timeProviderService.IsWorkingDayAsync(today))
        {
            _logger.LogInformation("Push blast skipped. Type={Type} Date={Date} Reason=Holiday", type, today);

            return new BlastPushNotificationResponse
            {
                Success = true,
                Skipped = true,
                Reason = "Holiday",
                Type = type,
                TotalDevices = 0,
                SuccessCount = 0,
                FailedCount = 0
            };
        }

        var userIdsOnLeave = await _leaveRequestService.GetUserIdsOnLeaveAsync(today);

        var tokens = await _context.UserFcmTokens
            .AsNoTracking()
            .Where(t =>
                t.IsActive &&
                !string.IsNullOrWhiteSpace(t.FcmToken) &&
                !userIdsOnLeave.Contains(t.UserId))
            .Select(t => t.FcmToken)
            .ToListAsync();

        _logger.LogInformation("Push blast started. Type={Type} TotalDevices={TotalDevices}", type, tokens.Count);

        var successCount = 0;
        var failedCount = 0;

        foreach (var token in tokens)
        {
            try
            {
                await _pushSenderService.SendDataMessageAsync(token!, title, body, type);
                successCount++;
            }
            catch (Exception ex)
            {
                failedCount++;
                _logger.LogWarning(ex, "Push blast send failed. Type={Type}", type);
            }
        }

        _logger.LogInformation(
            "Push blast completed. Type={Type} TotalDevices={TotalDevices} Success={SuccessCount} Failed={FailedCount}",
            type,
            tokens.Count,
            successCount,
            failedCount);

        return new BlastPushNotificationResponse
        {
            Success = true,
            Skipped = false,
            Type = type,
            TotalDevices = tokens.Count,
            SuccessCount = successCount,
            FailedCount = failedCount
        };
    }

    private static void ValidateBlast(BlastPushNotificationDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        if (string.IsNullOrWhiteSpace(dto.Type))
            throw new ArgumentException("type is required", nameof(dto.Type));

        var t = dto.Type.Trim().ToUpperInvariant();
        if (t != "MASUK" && t != "PULANG")
            throw new ArgumentException("type must be either 'MASUK' or 'PULANG'", nameof(dto.Type));

        // Title/body now optional: will be defaulted based on type
    }

    private static string GetDefaultTitle(string type) => "Reminder Presensi";

    private static string GetDefaultBody(string type) => type switch
    {
        "MASUK" => "Jangan lupa untuk melakukan presensi masuk",
        "PULANG" => "Jangan lupa untuk melakukan presensi pulang",
        _ => "Jangan lupa untuk melakukan presensi"
    };

    private async Task<TimeZoneInfo> GetTimeZoneAsync()
    {
        try
        {
            var timezoneId = await _settingService.GetAsync("TIMEZONE");
            return TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        }
        catch
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }
}
