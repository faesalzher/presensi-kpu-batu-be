namespace presensi_kpu_batu_be.Modules.LeaveRequestModule.Dto;

public class QueryLeaveRequestsDto
{
    public Guid? UserId { get; set; }
    public Guid? DepartmentId { get; set; }

    // repeatable query params: ?type=SICK&type=LEAVE
    public List<string>? Type { get; set; }

    // repeatable query params: ?status=PENDING&status=APPROVED
    public List<string>? Status { get; set; }

    public DateTime? StartDateFrom { get; set; }
    public DateTime? StartDateTo { get; set; }
    public DateTime? EndDateFrom { get; set; }
    public DateTime? EndDateTo { get; set; }
    public DateTime? ReviewedDateFrom { get; set; }
    public DateTime? ReviewedDateTo { get; set; }
}
