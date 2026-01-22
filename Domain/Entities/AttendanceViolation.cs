namespace presensi_kpu_batu_be.Domain.Entities
{
    using presensi_kpu_batu_be.Domain.Enums;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    [Table("attendance_violation")]
    public class AttendanceViolation : BaseEntity
    {
        [Key]
        [Column("guid")]
        public Guid Guid { get; set; }

        [Required]
        [Column("attendance_id")]
        public Guid AttendanceId { get; set; }

        [ForeignKey(nameof(AttendanceId))]
        public Attendance Attendance { get; set; } = null!;

        [Required]
        [Column("type")]
        public AttendanceViolationType Type { get; set; }

        [Required]
        [Column("source")]
        public ViolationSource Source { get; set; }

        [Required]
        [Column("penalty_percent", TypeName = "numeric(5,2)")]
        public decimal PenaltyPercent { get; set; }

        [Required]
        [Column("occurred_at")]
        public DateTime OccurredAt { get; set; } // UTC

        [Column("notes")]
        [MaxLength(255)]
        public string? Notes { get; set; }
    }

}
