using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace presensi_kpu_batu_be.Domain.Entities;

[Table("general_setting")]
public class GeneralSetting : BaseEntity
{
    [Key]
    [Column("guid")]
    public Guid Guid { get; set; }

    [Required]
    [Column("code")]
    [StringLength(100)]
    public string Code { get; set; } = default!;

    [Required]
    [Column("description")]
    [StringLength(255)]
    public string Description { get; set; } = default!;

    [Required]
    [Column("value")]
    [StringLength(500)]
    public string Value { get; set; } = default!;

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    [Required]
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
