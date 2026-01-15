namespace presensi_kpu_batu_be.Modules.SystemSettingModule.Dto
{
    public class WorkingDayResponseDto
    {
        public string Date { get; set; } = default!;
        public bool IsWorkAllowed { get; set; }
        public bool IsHoliday { get; set; }
        public string Type { get; set; } = default!;
        public string? WorkStart { get; set; }
        public string? WorkEnd { get; set; }
        public string? WorkOpened { get; set; }
        public string? WorkClosed { get; set; }
        public string Message { get; set; } = default!;
        public DateTime? NextChangeAt { get; set; }
    }

}
