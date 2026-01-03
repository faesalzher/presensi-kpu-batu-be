using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("leave_request")]
public class LeaveRequest
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

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    [Column("attachment_id")]
    public Guid? AttachmentId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
