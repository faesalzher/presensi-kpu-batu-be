namespace presensi_kpu_batu_be.Modules.SystemSettingModule.Dto
{
    public class WorkingDayResponseDto
    {
        public string Date { get; set; } = default!;
        public bool IsHoliday { get; set; }
        public string Type { get; set; } = default!;
        public string? WorkStart { get; set; }
        public string? WorkEnd { get; set; }
        public string Message { get; set; } = default!;
    }

}
