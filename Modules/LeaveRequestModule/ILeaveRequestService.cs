

public interface ILeaveRequestService
{
    Task<LeaveStatusResult> CheckUserLeaveStatusAsync(Guid userId, DateOnly date);
}
