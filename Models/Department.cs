using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace presensi_kpu_batu_be.Models
{
    [Table("department")]
    public class Department
    {
        [Key]
        [Column("guid")]
        public Guid Guid { get; set; }

        [Required]
        [Column("name")]
        public string Name { get; set; } = default!;

        [Required]
        [Column("code")]
        public string Code { get; set; } = default!;

        [Column("head_id")]
        public Guid? HeadId { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
