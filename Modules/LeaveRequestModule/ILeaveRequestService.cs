using presensi_kpu_batu_be.Modules.LeaveRequestModule.Dto;

public interface ILeaveRequestService
{
    Task<LeaveRequest> CreateAsync(Guid userId, CreateLeaveRequestDto dto);
    Task<LeaveStatusResult> CheckUserLeaveStatusAsync(Guid userId, DateOnly date);
    Task<List<Guid>> GetUserIdsOnLeaveAsync(DateOnly date);
    Task<List<LeaveRequestResponseDto>> GetMyLeaveRequests(Guid userId);

    // Get a single leave request by its guid (scoped to requesting user)
    Task<LeaveRequestResponseDto?> GetByGuidAsync(Guid guid, Guid userId);
}
