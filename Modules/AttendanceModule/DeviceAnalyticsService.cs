using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using presensi_kpu_batu_be.Domain.Entities;

namespace presensi_kpu_batu_be.Modules.AttendanceModule;

public class DeviceAnalyticsService : IDeviceAnalyticsService
{
    private readonly AppDbContext _context;
    private readonly ILogger<DeviceAnalyticsService> _logger;

    public DeviceAnalyticsService(AppDbContext context, ILogger<DeviceAnalyticsService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task RecordAsync(Guid userId, Guid? attendanceId, string? deviceAnalyticsJson)
    {
        string? fingerprint = null;
        string? deviceType = null;
        string platform = "Unknown";
        string browser = "Unknown";
        bool? isMobileLike = null;
        decimal? trustScore = null;
        string? rawJson = null;

        if (!string.IsNullOrWhiteSpace(deviceAnalyticsJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(deviceAnalyticsJson);
                var root = doc.RootElement;

                fingerprint = GetString(root, "deviceFingerprint", "fingerprint");
                deviceType = GetString(root, "deviceType", "type");
                platform = NormalizePlatform(GetString(root, "platform", "os"));
                browser = NormalizeBrowser(GetString(root, "browser", "browserName"));

                var isMobileRaw = GetBool(root, "isMobileLike", "isMobile");
                isMobileLike = isMobileRaw ?? InferMobileLike(deviceType, platform);

                trustScore = GetDecimal(root, "trustScore");
                rawJson = root.GetRawText();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse device analytics JSON for user {UserId}", userId);
            }
        }

        var history = new AttendanceDeviceHistory
        {
            AttendanceId = attendanceId,
            UserId = userId,
            DeviceFingerprint = TrimTo(fingerprint, 128),
            DeviceType = TrimTo(deviceType, 32),
            Platform = TrimTo(platform, 64),
            Browser = TrimTo(browser, 64),
            IsMobileLike = isMobileLike,
            TrustScore = trustScore,
            DeviceAnalyticsJson = rawJson
        };

        _context.AttendanceDeviceHistory.Add(history);
        await _context.SaveChangesAsync();
    }

    private static string? GetString(JsonElement root, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!root.TryGetProperty(key, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.String)
                return value.GetString()?.Trim();

            if (value.ValueKind == JsonValueKind.Number || value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
                return value.ToString();
        }

        return null;
    }

    private static bool? GetBool(JsonElement root, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!root.TryGetProperty(key, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.True)
                return true;
            if (value.ValueKind == JsonValueKind.False)
                return false;

            if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed))
                return parsed;
        }

        return null;
    }

    private static decimal? GetDecimal(JsonElement root, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!root.TryGetProperty(key, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var parsedNumber))
                return parsedNumber;

            if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedString))
                return parsedString;
        }

        return null;
    }

    private static bool? InferMobileLike(string? deviceType, string? platform)
    {
        var source = $"{deviceType} {platform}".Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(source))
            return null;

        return source.Contains("mobile") || source.Contains("android") || source.Contains("ios") || source.Contains("iphone") || source.Contains("ipad");
    }

    private static string NormalizeBrowser(string? value)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(v))
            return "Unknown";

        if (v.Contains("samsung")) return "Samsung Internet";
        if (v.Contains("edg")) return "Edge";
        if (v.Contains("firefox")) return "Firefox";
        if (v.Contains("chrome") || v.Contains("chromium")) return "Chrome";
        if (v.Contains("safari")) return "Safari";

        return "Unknown";
    }

    private static string NormalizePlatform(string? value)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(v))
            return "Unknown";

        if (v.Contains("android")) return "Android";
        if (v.Contains("ios") || v.Contains("iphone") || v.Contains("ipad")) return "iOS";
        if (v.Contains("windows")) return "Windows";
        if (v.Contains("mac")) return "MacOS";
        if (v.Contains("linux")) return "Linux";

        return "Unknown";
    }

    private static string? TrimTo(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();
        return normalized.Length <= max ? normalized : normalized[..max];
    }
}
