using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace presensi_kpu_batu_be.Domain.Entities
{
    [Table("ref_tunjangan_kinerja")]
    [Index(nameof(KelasJabatan), IsUnique = true)]
    public class RefTunjanganKinerja : BaseEntity
    {
        [Key]
        [Column("guid")]
        public Guid Guid { get; set; }

        [Required]
        [Column("kelas_jabatan")]
        public int KelasJabatan { get; set; }

        [Required]
        [Column("tunjangan_kinerja_amount", TypeName = "numeric(18,2)")]
        public decimal TunjanganKinerjaAmount { get; set; }

        // created_at, updated_at, usr_crt, usr_upd are inherited from BaseEntity
    }
}
