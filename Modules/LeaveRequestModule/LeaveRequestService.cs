using Microsoft.EntityFrameworkCore;

public class LeaveRequestService : ILeaveRequestService
{
    private readonly AppDbContext _context;

    public LeaveRequestService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<LeaveStatusResult> CheckUserLeaveStatusAsync(Guid userId, DateOnly date)
    {
        var leave = await _context.LeaveRequest
            .AsNoTracking().Where(l => l.UserId == userId &&
                l.Status == LeaveRequestStatus.APPROVED &&
                l.StartDate <= date &&
                l.EndDate >= date)
            .Select(l => new
            {
                l.Type
            })
            .FirstOrDefaultAsync();

        if (leave == null)
        {
            return new LeaveStatusResult
            {
                IsOnLeave = false
            };
        }

        return new LeaveStatusResult
        {
            IsOnLeave = true,
            LeaveType = leave.Type
        };
    }

    public async Task<List<Guid>> GetUserIdsOnLeaveAsync(DateOnly date)
    {
        return await _context.LeaveRequest
            .AsNoTracking()
            .Where(l =>
                l.Status == LeaveRequestStatus.APPROVED &&
                l.StartDate <= date &&
                l.EndDate >= date)
            .Select(l => l.UserId)
            .Distinct()
            .ToListAsync();
    }

}
