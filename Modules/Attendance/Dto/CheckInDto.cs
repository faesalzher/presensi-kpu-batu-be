using System.ComponentModel.DataAnnotations;

namespace presensi_kpu_batu_be.Modules.Attendance.Dto
{
    public class CheckInDto
    {
        [Required]
        [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90")]
        public double Latitude { get; set; }

        [Required]
        [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180")]
        public double Longitude { get; set; }

        public double? Accuracy { get; set; }

        public string? Provider { get; set; }

        public string? Notes { get; set; }

        // Photo handled via IFormFile in controller
    }
}
