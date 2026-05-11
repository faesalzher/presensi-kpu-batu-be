namespace presensi_kpu_batu_be.Modules.AttendanceModule.Dto;

public class DeviceAnalyticsQueryParams
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Search { get; set; }
    public string? Platform { get; set; }
    public string? Browser { get; set; }
    public string? TrustStatus { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? SortBy { get; set; } = "trustScore";
    public string? SortDirection { get; set; } = "desc";
}
