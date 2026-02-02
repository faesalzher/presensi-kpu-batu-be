using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace presensi_kpu_batu_be.Domain.Entities;

[Table("user_fcm_tokens")]
public class UserFcmToken : BaseEntity
{
    [Key]
    [Column("guid")]
    public Guid Guid { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("fcm_token")]
    public string FcmToken { get; set; } = default!;

    [Column("device_id")]
    public string DeviceId { get; set; } = default!;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;
}
