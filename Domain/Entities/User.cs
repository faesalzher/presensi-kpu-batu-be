using presensi_kpu_batu_be.Domain.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

[Table("users")]
public class User : BaseEntity
{
    [Key]
    [Required]
    [Column("guid", TypeName = "uuid")]
    public Guid Guid { get; set; } 

    [Column("full_name")]
    [Required]
    public string? FullName { get; set; }

    [Column("email")]
    [Required]
    public string? Email { get; set; }

    [Column("nip")]
    public string? Nip { get; set; }

    [Column("phone_number")]
    public string? PhoneNumber { get; set; }

    [Column("profile_image")]
    public string? ProfileImage { get; set; }

    [Column("profile_image_url")]
    public string? ProfileImageUrl { get; set; }

    [Column("role")]
    public string? Role { get; set; }

    [Column("department_id")]
    public Guid? DepartmentId { get; set; }

    [Column("position")]
    public string? Position { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

