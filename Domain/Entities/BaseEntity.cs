using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace presensi_kpu_batu_be.Domain.Entities
{
    public abstract class BaseEntity
    {
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("usr_crt")]
        public string? UsrCrt { get; set; }

        [Column("usr_upd")]
        public string? UsrUpd { get; set; }
    }
}
