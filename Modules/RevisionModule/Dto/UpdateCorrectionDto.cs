using System.ComponentModel.DataAnnotations;

namespace presensi_kpu_batu_be.Modules.RevisionModule.Dto
{
    public class UpdateCorrectionDto
    {
        [Required]
        public string Status { get; set; } = null!; // APPROVE / REJECT
    }
}
