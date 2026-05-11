using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace presensi_kpu_batu_be.Domain.Entities;

[Table("attendance_device_history")]
public class AttendanceDeviceHistory : BaseEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    [Column("attendance_id")]
    public Guid? AttendanceId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("device_fingerprint")]
    [StringLength(128)]
    public string? DeviceFingerprint { get; set; }

    [Column("device_type")]
    [StringLength(32)]
    public string? DeviceType { get; set; }

    [Column("platform")]
    [StringLength(64)]
    public string? Platform { get; set; }

    [Column("browser")]
    [StringLength(64)]
    public string? Browser { get; set; }

    [Column("is_mobile_like")]
    public bool? IsMobileLike { get; set; }

    [Column("trust_score", TypeName = "numeric(5,2)")]
    public decimal? TrustScore { get; set; }

    [Column("device_analytics_json", TypeName = "jsonb")]
    public string? DeviceAnalyticsJson { get; set; }

    [ForeignKey(nameof(AttendanceId))]
    public Attendance? Attendance { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }
}
