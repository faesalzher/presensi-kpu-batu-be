namespace presensi_kpu_batu_be.Modules.AttendanceModule.Dto;

public class DeviceAnalyticsSummaryCardDto
{
    public int TotalActiveDevices { get; set; }
    public int UsersWithMultipleDevices { get; set; }
    public int SuspiciousDeviceChanges { get; set; }
    public int HighTrustUsers { get; set; }
}
