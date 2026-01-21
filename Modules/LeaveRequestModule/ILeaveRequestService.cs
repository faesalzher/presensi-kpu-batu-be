

public interface ILeaveRequestService
{
    Task<LeaveStatusResult> CheckUserLeaveStatusAsync(Guid userId, DateOnly date);
    Task<List<Guid>> GetUserIdsOnLeaveAsync(DateOnly date);

}
