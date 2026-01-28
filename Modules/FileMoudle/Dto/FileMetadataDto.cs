using presensi_kpu_batu_be.Domain.Enums;

namespace presensi_kpu_batu_be.Modules.FileMoudle.Dto
{
    public class FileMetadataDto
    {
        public Guid Guid { get; set; }
        public string FileName { get; set; } = default!;
        public string OriginalName { get; set; } = default!;
        public string MimeType { get; set; } = default!;
        public long Size { get; set; }
        public string Path { get; set; } = default!;
        public FileCategory Category { get; set; }
    }
}
