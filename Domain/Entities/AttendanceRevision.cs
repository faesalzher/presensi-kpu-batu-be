using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace presensi_kpu_batu_be.Domain.Entities
{
    [Table("attendance_revision")]
    public class AttendanceRevision : BaseEntity
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Required]
        [Column("attendance_id")]
        public Guid AttendanceId { get; set; }

        [ForeignKey(nameof(AttendanceId))]
        public Attendance Attendance { get; set; } = null!;

        [Required]
        [Column("date")]
        public DateOnly Date { get; set; }

        [Required]
        [Column("type")]
        [MaxLength(50)]
        public string Type { get; set; } = null!;

        [Column("reason_code")]
        [MaxLength(100)]
        public string? ReasonCode { get; set; }

        [Column("reason_description")]
        public string? ReasonDescription { get; set; }

        [Column("check_in_time_old")]
        public DateTime? CheckInTimeOld { get; set; }

        [Column("check_in_time_new")]
        public DateTime? CheckInTimeNew { get; set; }

        [Column("check_out_time_old")]
        public DateTime? CheckOutTimeOld { get; set; }

        [Column("check_out_time_new")]
        public DateTime? CheckOutTimeNew { get; set; }

        [Required]
        [Column("status")]
        [MaxLength(20)]
        public string Status { get; set; } = "PENDING";

        [Required]
        [Column("requested_by")]
        public Guid RequestedBy { get; set; }

        [Column("approved_by")]
        public Guid? ApprovedBy { get; set; }

        [Column("approved_at")]
        public DateTime? ApprovedAt { get; set; }
    }
}
