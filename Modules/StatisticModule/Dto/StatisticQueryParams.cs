using presensi_kpu_batu_be.Domain.Enums;

namespace presensi_kpu_batu_be.Modules.StatisticModule.Dto
{
    public class StatisticQueryParams
    {
        public string? StartDate { get; set; }      // yyyy-MM-dd
        public string? EndDate { get; set; }
        public Guid? UserId { get; set; }
        public Guid? DepartmentId { get; set; }
        public ReportPeriod? Period { get; set; }
        public bool IncludeInactive { get; set; } = false;
    }

}
