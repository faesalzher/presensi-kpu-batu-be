namespace presensi_kpu_batu_be.Modules.AttendanceModule;

public interface IDeviceAnalyticsService
{
    Task RecordAsync(Guid userId, Guid? attendanceId, string? deviceAnalyticsJson);
}
