using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace presensi_kpu_batu_be.Models
{
    [Table("attendance")]
    public class Attendance
    {
        [Key]
        [Column("guid")]
        public Guid Guid { get; set; }

        // 🔗 User
        [Required]
        [Column("user_id")]
        public Guid UserId { get; set; }

        // 🔗 Department
        [Column("department_id")]
        public Guid? DepartmentId { get; set; }

        [Required]
        [Column("date")]
        public DateOnly Date { get; set; }

        // ⏰ Check In
        [Column("check_in_time")]
        public DateTime? CheckInTime { get; set; }

        [Column("check_in_location")]
        public string? CheckInLocation { get; set; }

        [Column("check_in_photo_id")]
        public Guid? CheckInPhotoId { get; set; }

        [Column("check_in_notes")]
        public string? CheckInNotes { get; set; }

        // ⏰ Check Out
        [Column("check_out_time")]
        public DateTime? CheckOutTime { get; set; }

        [Column("check_out_location")]
        public string? CheckOutLocation { get; set; }

        [Column("check_out_photo_id")]
        public Guid? CheckOutPhotoId { get; set; }

        [Column("check_out_notes")]
        public string? CheckOutNotes { get; set; }

        [Column("work_hours", TypeName = "numeric(5,2)")]
        public decimal? WorkHours { get; set; }

        [Required]
        [Column("status")]
        public string Status { get; set; } = default!;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        // =====================
        // Navigation Properties
        // =====================

        [ForeignKey(nameof(DepartmentId))]
        public Department? Department { get; set; }
    }
}
