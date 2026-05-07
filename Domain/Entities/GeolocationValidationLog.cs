using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace presensi_kpu_batu_be.Domain.Entities
{
    [Table("geolocation_validation_log")]
    public class GeolocationValidationLog : BaseEntity
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("latitude")]
        public double Latitude { get; set; }

        [Column("longitude")]
        public double Longitude { get; set; }

        [Column("accuracy")]
        public double Accuracy { get; set; }

        [Column("timestamp_utc")]
        public DateTime TimestampUtc { get; set; }

        [Required]
        [Column("reason")]
        public string Reason { get; set; } = default!;
    }
}
