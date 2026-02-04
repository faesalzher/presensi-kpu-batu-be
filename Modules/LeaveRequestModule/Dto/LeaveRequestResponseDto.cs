using presensi_kpu_batu_be.Modules.FileMoudle.Dto;

namespace presensi_kpu_batu_be.Modules.LeaveRequestModule.Dto
{
    public class LeaveRequestResponseDto
    {
        public Guid Guid { get; set; }
        public Guid UserId { get; set; }
        public Guid DepartmentId { get; set; }

        public string? UserName { get; set; }
        public string? Nip { get; set; }
        public string? ProfileImageUrl { get; set; }
        public string? Type { get; set; }
        public string? Status { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public string? Reason { get; set; }
        public FileMetadataDto? Attachment { get; set; }
    }
}


