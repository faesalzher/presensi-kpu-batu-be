using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace presensi_kpu_batu_be.Domain.Entities
{
    [Table("scheduler_execution_log")]
    public class SchedulerLog : BaseEntity
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("job_name")]
        public string JobName { get; set; } = default!;

        [Column("scheduled_at")]
        public DateTime? ScheduledAt { get; set; }

        [Column("executed_at")]
        public DateTime? ExecutedAt { get; set; }

        [Required]
        [Column("status")]
        public string Status { get; set; } = default!; // SUCCESS, FAILED, SKIPPED, NOT_RUN

        [Column("message")]
        public string? Message { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}