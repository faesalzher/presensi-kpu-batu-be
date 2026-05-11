namespace presensi_kpu_batu_be.Modules.AttendanceModule.Dto;

public class DeviceAnalyticsListItemDto
{
    public Guid UserId { get; set; }
    public string? UserName { get; set; }

    public string? DominantFingerprint { get; set; }
    public string? DominantPlatform { get; set; }
    public string? DominantBrowser { get; set; }

    public int TotalAttendance { get; set; }
    public int UniqueDeviceCount { get; set; }
    public decimal DominantDeviceRatio { get; set; }

    public decimal TrustScore { get; set; }
    public string TrustStatus { get; set; } = "LOW";

    public DateTime? LastAttendanceAt { get; set; }
}
