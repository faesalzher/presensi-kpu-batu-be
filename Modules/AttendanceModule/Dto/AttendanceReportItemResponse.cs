namespace presensi_kpu_batu_be.Modules.AttendanceModule.Dto
{
    public class AttendanceReportItemResponse
    {
        public Guid UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Nip { get; set; }
        public string? ProfileImageUrl { get; set; }

        public DateOnly Date { get; set; }
        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }

        public string Status { get; set; } = string.Empty;
    }
}
