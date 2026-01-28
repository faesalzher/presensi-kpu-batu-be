using presensi_kpu_batu_be.Domain.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("leave_request")]
public class LeaveRequest : BaseEntity
{
    [Key]
    [Column("guid")]
    public Guid Guid { get; set; }

    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Required]
    [Column("department_id")]
    public Guid DepartmentId { get; set; }

    [Required]
    [Column("type")]
    public LeaveRequestType Type { get; set; }

    [Required]
    [Column("status")]
    public LeaveRequestStatus Status { get; set; }

    [Required]
    [Column("start_date")]
    public DateOnly StartDate { get; set; }

    [Required]
    [Column("end_date")]
    public DateOnly EndDate { get; set; }

    [Required]
    [Column("reason")]
    public string? Reason { get; set; }

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    // ❗ tidak simpan attachment_id di sini
    // relasi via file_metadata.related_id
}
