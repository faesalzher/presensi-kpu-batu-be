using Microsoft.EntityFrameworkCore;
using presensi_kpu_batu_be.Modules.AttendanceModule.Dto;

namespace presensi_kpu_batu_be.Modules.AttendanceModule;

public class DeviceAnalyticsQueryService : IDeviceAnalyticsQueryService
{
    private readonly AppDbContext _context;
    private readonly ILogger<DeviceAnalyticsQueryService> _logger;

    public DeviceAnalyticsQueryService(AppDbContext context, ILogger<DeviceAnalyticsQueryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<DeviceAnalyticsSummaryDto> GetPagedAsync(DeviceAnalyticsQueryParams query)
    {
        try
        {
            var normalized = NormalizeQuery(query);
            var rows = await BuildBaseQuery(normalized)
                .Select(x => new AnalyticsRow
                {
                    UserId = x.UserId,
                    UserName = x.User != null ? x.User.FullName : null,
                    DeviceFingerprint = x.DeviceFingerprint,
                    DeviceType = x.DeviceType,
                    Platform = x.Platform,
                    Browser = x.Browser,
                    IsMobileLike = x.IsMobileLike,
                    Timestamp = x.CreatedAt
                })
                .ToListAsync();

            var summaries = BuildUserSummaries(rows);

            if (!string.IsNullOrWhiteSpace(normalized.TrustStatus))
            {
                summaries = summaries
                    .Where(x => string.Equals(x.TrustStatus, normalized.TrustStatus, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            summaries = ApplySorting(summaries, normalized.SortBy, normalized.SortDirection);

            var total = summaries.Count;
            var items = summaries
                .Skip((normalized.Page - 1) * normalized.PageSize)
                .Take(normalized.PageSize)
                .ToList();

            return new DeviceAnalyticsSummaryDto
            {
                Page = normalized.Page,
                PageSize = normalized.PageSize,
                TotalRecords = total,
                Items = items
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate device analytics list");
            return new DeviceAnalyticsSummaryDto
            {
                Page = query.Page <= 0 ? 1 : query.Page,
                PageSize = query.PageSize <= 0 ? 10 : query.PageSize,
                TotalRecords = 0,
                Items = new List<DeviceAnalyticsListItemDto>()
            };
        }
    }

    public async Task<DeviceAnalyticsDetailDto?> GetDetailAsync(Guid userId, DateTime? startDate, DateTime? endDate)
    {
        try
        {
            var rows = await BuildBaseQuery(new DeviceAnalyticsQueryParams
            {
                StartDate = startDate,
                EndDate = endDate
            })
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AnalyticsRow
            {
                UserId = x.UserId,
                UserName = x.User != null ? x.User.FullName : null,
                DeviceFingerprint = x.DeviceFingerprint,
                DeviceType = x.DeviceType,
                Platform = x.Platform,
                Browser = x.Browser,
                IsMobileLike = x.IsMobileLike,
                TrustScore = x.TrustScore,
                Timestamp = x.CreatedAt
            })
            .ToListAsync();

            if (rows.Count == 0)
                return null;

            var summary = BuildUserSummary(userId, rows);

            return new DeviceAnalyticsDetailDto
            {
                UserId = userId,
                UserName = summary.UserName,
                TrustScore = summary.TrustScore,
                TrustStatus = summary.TrustStatus,
                DominantFingerprint = summary.DominantFingerprint,
                DeviceHistory = rows
                    .OrderByDescending(x => x.Timestamp)
                    .Select(x => new DeviceHistoryItemDto
                    {
                        Timestamp = x.Timestamp,
                        Fingerprint = x.DeviceFingerprint,
                        DeviceType = x.DeviceType,
                        Platform = x.Platform,
                        Browser = x.Browser,
                        IsMobileLike = x.IsMobileLike,
                        TrustScore = x.TrustScore ?? summary.TrustScore
                    })
                    .ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate device analytics detail for user {UserId}", userId);
            return null;
        }
    }

    public async Task<DeviceAnalyticsSummaryCardDto> GetSummaryAsync(DateTime? startDate, DateTime? endDate)
    {
        try
        {
            var rows = await BuildBaseQuery(new DeviceAnalyticsQueryParams
            {
                StartDate = startDate,
                EndDate = endDate
            })
            .Select(x => new AnalyticsRow
            {
                UserId = x.UserId,
                UserName = x.User != null ? x.User.FullName : null,
                DeviceFingerprint = x.DeviceFingerprint,
                DeviceType = x.DeviceType,
                Platform = x.Platform,
                Browser = x.Browser,
                IsMobileLike = x.IsMobileLike,
                Timestamp = x.CreatedAt
            })
            .ToListAsync();

            var summaries = BuildUserSummaries(rows);

            return new DeviceAnalyticsSummaryCardDto
            {
                TotalActiveDevices = rows
                    .Where(x => !string.IsNullOrWhiteSpace(x.DeviceFingerprint))
                    .Select(x => x.DeviceFingerprint!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count(),
                UsersWithMultipleDevices = summaries.Count(x => x.UniqueDeviceCount > 1),
                SuspiciousDeviceChanges = summaries.Count(x => IsSuspiciousStatus(x.TrustStatus)),
                HighTrustUsers = summaries.Count(x => string.Equals(x.TrustStatus, "HIGH", StringComparison.OrdinalIgnoreCase))
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate device analytics summary cards");
            return new DeviceAnalyticsSummaryCardDto();
        }
    }

    private IQueryable<Domain.Entities.AttendanceDeviceHistory> BuildBaseQuery(DeviceAnalyticsQueryParams query)
    {
        var q = _context.AttendanceDeviceHistory.AsNoTracking().AsQueryable();

        if (query.StartDate.HasValue)
            q = q.Where(x => x.CreatedAt >= query.StartDate.Value);

        if (query.EndDate.HasValue)
        {
            var endExclusive = query.EndDate.Value.Date.AddDays(1);
            q = q.Where(x => x.CreatedAt < endExclusive);
        }

        if (!string.IsNullOrWhiteSpace(query.Platform))
            q = q.Where(x => x.Platform != null && x.Platform.ToLower() == query.Platform.Trim().ToLower());

        if (!string.IsNullOrWhiteSpace(query.Browser))
            q = q.Where(x => x.Browser != null && x.Browser.ToLower() == query.Browser.Trim().ToLower());

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(x =>
                (x.User != null && x.User.FullName != null && x.User.FullName.ToLower().Contains(s)) ||
                (x.DeviceFingerprint != null && x.DeviceFingerprint.ToLower().Contains(s)));
        }

        return q;
    }

    private static DeviceAnalyticsQueryParams NormalizeQuery(DeviceAnalyticsQueryParams query)
    {
        return new DeviceAnalyticsQueryParams
        {
            Page = query.Page <= 0 ? 1 : query.Page,
            PageSize = query.PageSize <= 0 ? 10 : Math.Min(100, query.PageSize),
            Search = query.Search,
            Platform = query.Platform,
            Browser = query.Browser,
            TrustStatus = query.TrustStatus,
            StartDate = query.StartDate,
            EndDate = query.EndDate,
            SortBy = string.IsNullOrWhiteSpace(query.SortBy) ? "trustScore" : query.SortBy,
            SortDirection = string.IsNullOrWhiteSpace(query.SortDirection) ? "desc" : query.SortDirection
        };
    }

    private static List<DeviceAnalyticsListItemDto> BuildUserSummaries(List<AnalyticsRow> rows)
    {
        return rows
            .GroupBy(x => x.UserId)
            .Select(g => BuildUserSummary(g.Key, g.ToList()))
            .ToList();
    }

    private static DeviceAnalyticsListItemDto BuildUserSummary(Guid userId, List<AnalyticsRow> rows)
    {
        var totalAttendance = rows.Count;

        var fingerprintGroups = rows
            .GroupBy(x => NormalizeKey(x.DeviceFingerprint, "UNKNOWN"))
            .Select(g => new { Fingerprint = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Fingerprint)
            .ToList();

        var dominantFingerprint = fingerprintGroups.FirstOrDefault()?.Fingerprint;
        if (string.Equals(dominantFingerprint, "UNKNOWN", StringComparison.OrdinalIgnoreCase))
            dominantFingerprint = null;

        var dominantCount = fingerprintGroups.FirstOrDefault()?.Count ?? 0;
        var dominantRatio = totalAttendance > 0
            ? Math.Round((decimal)dominantCount * 100m / totalAttendance, 2)
            : 0m;

        var uniqueDeviceCount = fingerprintGroups.Count(x => !string.Equals(x.Fingerprint, "UNKNOWN", StringComparison.OrdinalIgnoreCase));

        var dominantFingerprintRows = rows
            .Where(x => string.Equals(NormalizeKey(x.DeviceFingerprint, "UNKNOWN"), NormalizeKey(dominantFingerprint, "UNKNOWN"), StringComparison.OrdinalIgnoreCase))
            .ToList();

        var dominantPlatform = dominantFingerprintRows
            .GroupBy(x => NormalizeKey(x.Platform, "Unknown"))
            .OrderByDescending(x => x.Count())
            .ThenBy(x => x.Key)
            .Select(x => x.Key)
            .FirstOrDefault() ?? "Unknown";

        var dominantBrowser = dominantFingerprintRows
            .GroupBy(x => NormalizeKey(x.Browser, "Unknown"))
            .OrderByDescending(x => x.Count())
            .ThenBy(x => x.Key)
            .Select(x => x.Key)
            .FirstOrDefault() ?? "Unknown";

        var dominantDeviceType = rows
            .GroupBy(x => NormalizeKey(x.DeviceType, "Unknown"))
            .OrderByDescending(x => x.Count())
            .ThenBy(x => x.Key)
            .FirstOrDefault();

        var dominantPlatformRatio = totalAttendance > 0
            ? (decimal)rows.Count(x => string.Equals(NormalizeKey(x.Platform, "Unknown"), dominantPlatform, StringComparison.OrdinalIgnoreCase)) * 100m / totalAttendance
            : 0m;

        var dominantBrowserRatio = totalAttendance > 0
            ? (decimal)rows.Count(x => string.Equals(NormalizeKey(x.Browser, "Unknown"), dominantBrowser, StringComparison.OrdinalIgnoreCase)) * 100m / totalAttendance
            : 0m;

        var dominantDeviceTypeRatio = totalAttendance > 0 && dominantDeviceType != null
            ? (decimal)dominantDeviceType.Count() * 100m / totalAttendance
            : 0m;

        var switchCount = CountFingerprintSwitches(rows);
        var platformVariety = rows.Select(x => NormalizeKey(x.Platform, "Unknown")).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var browserVariety = rows.Select(x => NormalizeKey(x.Browser, "Unknown")).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var desktopCount = rows.Count(x => IsDesktopLike(x));
        var desktopRatio = totalAttendance > 0 ? (decimal)desktopCount * 100m / totalAttendance : 0m;

        var trustScore = CalculateTrustScore(
            dominantRatio,
            dominantPlatformRatio,
            dominantBrowserRatio,
            dominantDeviceTypeRatio,
            switchCount,
            uniqueDeviceCount,
            desktopRatio,
            platformVariety,
            browserVariety);

        var trustStatus = MapTrustStatus(trustScore, switchCount, uniqueDeviceCount, desktopRatio);

        return new DeviceAnalyticsListItemDto
        {
            UserId = userId,
            UserName = rows.Select(x => x.UserName).FirstOrDefault(),
            DominantFingerprint = dominantFingerprint,
            DominantPlatform = dominantPlatform,
            DominantBrowser = dominantBrowser,
            TotalAttendance = totalAttendance,
            UniqueDeviceCount = uniqueDeviceCount,
            DominantDeviceRatio = dominantRatio,
            TrustScore = trustScore,
            TrustStatus = trustStatus,
            LastAttendanceAt = rows.Max(x => (DateTime?)x.Timestamp)
        };
    }

    private static int CountFingerprintSwitches(List<AnalyticsRow> rows)
    {
        var ordered = rows
            .OrderBy(x => x.Timestamp)
            .Select(x => NormalizeKey(x.DeviceFingerprint, "UNKNOWN"))
            .ToList();

        if (ordered.Count <= 1)
            return 0;

        var switches = 0;
        for (var i = 1; i < ordered.Count; i++)
        {
            if (!string.Equals(ordered[i], ordered[i - 1], StringComparison.OrdinalIgnoreCase))
                switches++;
        }

        return switches;
    }

    private static decimal CalculateTrustScore(
        decimal dominantRatio,
        decimal dominantPlatformRatio,
        decimal dominantBrowserRatio,
        decimal dominantDeviceTypeRatio,
        int switchCount,
        int uniqueDeviceCount,
        decimal desktopRatio,
        int platformVariety,
        int browserVariety)
    {
        decimal score = 0;

        score += dominantRatio >= 90 ? 60 : dominantRatio >= 70 ? 45 : dominantRatio >= 50 ? 30 : 10;
        score += dominantPlatformRatio >= 80 ? 20 : dominantPlatformRatio >= 60 ? 10 : 0;
        score += dominantBrowserRatio >= 80 ? 10 : dominantBrowserRatio >= 60 ? 5 : 0;
        score += dominantDeviceTypeRatio >= 80 ? 10 : dominantDeviceTypeRatio >= 60 ? 5 : 0;

        score -= Math.Min(25, switchCount * 5);
        score -= uniqueDeviceCount >= 4 ? 15 : uniqueDeviceCount >= 3 ? 8 : 0;
        score -= desktopRatio > 50 ? 10 : desktopRatio > 20 ? 5 : 0;

        if (platformVariety >= 3 && browserVariety >= 3)
            score -= 10;

        return Math.Clamp(Math.Round(score, 2), 0, 100);
    }

    private static string MapTrustStatus(decimal trustScore, int switchCount, int uniqueDeviceCount, decimal desktopRatio)
    {
        if (trustScore < 40 || uniqueDeviceCount >= 5 || switchCount >= 8 || desktopRatio >= 80)
            return "SUSPICIOUS";

        if (trustScore >= 90)
            return "HIGH";

        if (trustScore >= 70)
            return "MEDIUM";

        return "LOW";
    }

    private static bool IsDesktopLike(AnalyticsRow row)
    {
        if (row.IsMobileLike.HasValue)
            return !row.IsMobileLike.Value;

        var platform = NormalizeKey(row.Platform, "Unknown").ToLowerInvariant();
        return platform is "windows" or "macos" or "linux";
    }

    private static string NormalizeKey(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static bool IsSuspiciousStatus(string trustStatus)
    {
        return string.Equals(trustStatus, "SUSPICIOUS", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trustStatus, "LOW", StringComparison.OrdinalIgnoreCase);
    }

    private static List<DeviceAnalyticsListItemDto> ApplySorting(List<DeviceAnalyticsListItemDto> items, string? sortBy, string? sortDirection)
    {
        var desc = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "lastattendance" or "lastattendanceat" => desc
                ? items.OrderByDescending(x => x.LastAttendanceAt).ThenBy(x => x.UserName).ToList()
                : items.OrderBy(x => x.LastAttendanceAt).ThenBy(x => x.UserName).ToList(),

            "dominantratio" or "dominantdeviceratio" => desc
                ? items.OrderByDescending(x => x.DominantDeviceRatio).ThenBy(x => x.UserName).ToList()
                : items.OrderBy(x => x.DominantDeviceRatio).ThenBy(x => x.UserName).ToList(),

            "uniquedevicecount" => desc
                ? items.OrderByDescending(x => x.UniqueDeviceCount).ThenBy(x => x.UserName).ToList()
                : items.OrderBy(x => x.UniqueDeviceCount).ThenBy(x => x.UserName).ToList(),

            _ => desc
                ? items.OrderByDescending(x => x.TrustScore).ThenBy(x => x.UserName).ToList()
                : items.OrderBy(x => x.TrustScore).ThenBy(x => x.UserName).ToList()
        };
    }

    private sealed class AnalyticsRow
    {
        public Guid UserId { get; set; }
        public string? UserName { get; set; }
        public string? DeviceFingerprint { get; set; }
        public string? DeviceType { get; set; }
        public string? Platform { get; set; }
        public string? Browser { get; set; }
        public bool? IsMobileLike { get; set; }
        public decimal? TrustScore { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
