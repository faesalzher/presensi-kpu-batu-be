using Microsoft.EntityFrameworkCore;

public class DepartmentService : IDepartmentService
{
    private readonly AppDbContext _context;

    public DepartmentService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Guid?> GetPrimaryDepartmentIdAsync(Guid userId)
    {
        // Asumsi: primary department disimpan di user.department_id
        return await _context.Users
            .AsNoTracking()
            .Where(u => u.Guid == userId)
            .Select(u => u.DepartmentId)
            .FirstOrDefaultAsync();
    }
}
