using presensi_kpu_batu_be.Domain.Enums;

namespace presensi_kpu_batu_be.Modules.LeaveRequestModule.Dto;

public class ReviewLeaveRequestDto
{
    public LeaveRequestStatus Status { get; set; }
    public string? Comments { get; set; }
}
