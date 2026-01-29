namespace presensi_kpu_batu_be.Modules.SystemSettingModule.Dto
{
    public class SchedulerLogDto
    {
        public long Id { get; set; }
        public string JobName { get; set; } = default!;
        public DateTime? ScheduledAt { get; set; }
        public DateTime? ExecutedAt { get; set; }
        public string Status { get; set; } = default!; // SUCCESS, FAILED, SKIPPED, NOT_RUN
        public string? Message { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SchedulerLogQueryParams
    {
        public string? JobName { get; set; }
        public string? Status { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class RunSchedulerJobDto
    {
        public string JobName { get; set; } = default!;
        public DateTime? ScheduledAt { get; set; }
    }
}