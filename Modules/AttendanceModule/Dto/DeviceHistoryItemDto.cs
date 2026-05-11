namespace presensi_kpu_batu_be.Modules.AttendanceModule.Dto;

public class DeviceHistoryItemDto
{
    public DateTime Timestamp { get; set; }
    public string? Fingerprint { get; set; }
    public string? DeviceType { get; set; }
    public string? Platform { get; set; }
    public string? Browser { get; set; }
    public bool? IsMobileLike { get; set; }
    public decimal? TrustScore { get; set; }
}
