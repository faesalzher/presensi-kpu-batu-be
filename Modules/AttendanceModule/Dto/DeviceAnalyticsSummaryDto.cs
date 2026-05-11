namespace presensi_kpu_batu_be.Modules.AttendanceModule.Dto;

public class DeviceAnalyticsSummaryDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalRecords { get; set; }
    public List<DeviceAnalyticsListItemDto> Items { get; set; } = new();
}
