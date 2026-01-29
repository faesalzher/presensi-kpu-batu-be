using System.ComponentModel.DataAnnotations;

namespace presensi_kpu_batu_be.Modules.AttendanceModule.Dto
{
    public class CheckInDto
    {
        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public double? Accuracy { get; set; }

        public string? Provider { get; set; }

        public string? Notes { get; set; }

        // Photo handled via IFormFile in controller
    }
}
