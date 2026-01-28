using presensi_kpu_batu_be.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace presensi_kpu_batu_be.Domain.Entities
{
    [Table("file_metadata")]
    public class FileMetadata : BaseEntity
    {
        [Key]
        [Column("guid")]
        public Guid Guid { get; set; }

        [Column("file_name")]
        public string FileName { get; set; } = default!;

        [Column("original_name")]
        public string OriginalName { get; set; } = default!;

        [Column("mime_type")]
        public string MimeType { get; set; } = default!;

        [Column("size")]
        public long Size { get; set; }

        [Column("path")]
        public string Path { get; set; } = default!;

        [Column("category")]
        public FileCategory Category { get; set; }

        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("related_id")]
        public Guid? RelatedId { get; set; }

        [Column("is_temporary")]
        public bool IsTemporary { get; set; } = false;
    }


}
