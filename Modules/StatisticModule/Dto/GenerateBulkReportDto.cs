using System.Text.Json.Serialization;
using presensi_kpu_batu_be.Domain.Enums;

namespace presensi_kpu_batu_be.Modules.StatisticModule.Dto
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BulkReportScope
    {
        ALL_USERS,
        DEPARTMENT,
        SPECIFIC_USERS,
    }

    public class GenerateBulkReportDto
    {
        [JsonPropertyName("format")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ReportFormat Format { get; set; } = ReportFormat.EXCEL;

        [JsonPropertyName("period")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ReportPeriod Period { get; set; } = ReportPeriod.MONTHLY;

        [JsonPropertyName("startDate")]
        public string? StartDate { get; set; } // yyyy-MM-dd

        [JsonPropertyName("endDate")]
        public string? EndDate { get; set; }   // yyyy-MM-dd

        [JsonPropertyName("scope")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public BulkReportScope Scope { get; set; } = BulkReportScope.DEPARTMENT;

        [JsonPropertyName("departmentName")]
        public string? DepartmentName { get; set; }

        [JsonPropertyName("userIds")]
        public List<Guid>? UserIds { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("includeInactive")]
        public bool IncludeInactive { get; set; } = false;

        [JsonPropertyName("separateSheets")]
        public bool SeparateSheets { get; set; } = true;

        [JsonPropertyName("includeSummary")]
        public bool IncludeSummary { get; set; } = true;
    }
}
