using System.ComponentModel.DataAnnotations;

namespace presensi_kpu_batu_be.Modules.RevisionModule.Dto
{
    public class CreateAttendanceCorrectionDto
    {
        [Required]
        public Guid AttendanceId { get; set; }

        [Required]
        public DateOnly Date { get; set; }

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = null!;

        [MaxLength(100)]
        public string? ReasonCode { get; set; }

        public string? ReasonDescription { get; set; }

        public DateTime? CheckInTimeOld { get; set; }
        public DateTime? CheckInTimeNew { get; set; }

        public DateTime? CheckOutTimeOld { get; set; }
        public DateTime? CheckOutTimeNew { get; set; }
    }
}
