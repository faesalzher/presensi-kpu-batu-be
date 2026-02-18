using System.Text.Json.Serialization;
using presensi_kpu_batu_be.Modules.StatisticModule.Dto;

namespace presensi_kpu_batu_be.Modules.AttendanceModule.Dto
{
    public class AttendanceReportQueryParams
    {
        public string? StartDate { get; set; } // yyyy-MM-dd
        public string? EndDate { get; set; }
        public Guid? UserId { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public BulkReportScope? Scope { get; set; }

        public string? DepartmentName { get; set; }
        public string? Status { get; set; }
    }
}
