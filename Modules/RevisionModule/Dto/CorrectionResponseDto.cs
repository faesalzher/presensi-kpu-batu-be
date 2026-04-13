namespace presensi_kpu_batu_be.Modules.RevisionModule.Dto
{
    public class CorrectionResponseDto
    {
        public Guid Id { get; set; }
        public Guid AttendanceId { get; set; }
        public DateOnly Date { get; set; }
        public string Type { get; set; } = null!;
        public string? ReasonCode { get; set; }
        public string? ReasonDescription { get; set; }
        public DateTime? CheckInTimeOld { get; set; }
        public DateTime? CheckInTimeNew { get; set; }
        public DateTime? CheckOutTimeOld { get; set; }
        public DateTime? CheckOutTimeNew { get; set; }
        public string Status { get; set; } = null!;
        public Guid RequestedBy { get; set; }
        public string? ProfileImageUrl { get; set; }
        public string? Username { get; set; }
        public string? Nip { get; set; }
        public Guid? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
