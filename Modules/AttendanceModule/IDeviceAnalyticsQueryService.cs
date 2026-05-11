using presensi_kpu_batu_be.Modules.AttendanceModule.Dto;

namespace presensi_kpu_batu_be.Modules.AttendanceModule;

public interface IDeviceAnalyticsQueryService
{
    Task<DeviceAnalyticsSummaryDto> GetPagedAsync(DeviceAnalyticsQueryParams query);
    Task<DeviceAnalyticsDetailDto?> GetDetailAsync(Guid userId, DateTime? startDate, DateTime? endDate);
    Task<DeviceAnalyticsSummaryCardDto> GetSummaryAsync(DateTime? startDate, DateTime? endDate);
}
