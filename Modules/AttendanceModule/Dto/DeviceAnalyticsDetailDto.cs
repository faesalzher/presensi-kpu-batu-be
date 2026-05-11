namespace presensi_kpu_batu_be.Modules.AttendanceModule.Dto;

public class DeviceAnalyticsDetailDto
{
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public decimal TrustScore { get; set; }
    public string TrustStatus { get; set; } = "LOW";
    public string? DominantFingerprint { get; set; }
    public List<DeviceHistoryItemDto> DeviceHistory { get; set; } = new();
}
