using System.ComponentModel.DataAnnotations.Schema;

namespace presensi_kpu_batu_be.Infrastucture.Models
{
    [Table("profiles")]
    public class Profile
    {
        [Column("id")]
        public Guid Id { get; set; }

        [Column("nik")]
        public string Nik { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("role")]
        public string Role { get; set; }
    }
}