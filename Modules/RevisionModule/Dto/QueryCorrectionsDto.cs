namespace presensi_kpu_batu_be.Modules.RevisionModule.Dto;

public class QueryCorrectionsDto
{
    public Guid? UserId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? AttendanceId { get; set; }
    public List<string>? Status { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public int? Page { get; set; }
    public int? Limit { get; set; }
}
