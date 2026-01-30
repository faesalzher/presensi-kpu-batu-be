using Microsoft.EntityFrameworkCore;
using presensi_kpu_batu_be.Domain.Entities;

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

    public async Task<Department?> GetByNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        // PostgreSQL-friendly & case-insensitive
        return await _context.Department
            .AsNoTracking()
            .FirstOrDefaultAsync(d =>
                EF.Functions.ILike(d.Name, name));
    }

    public async Task<List<Department>> GetByHeadAsync(Guid headId)
    {
        return await _context.Department
            .AsNoTracking()
            .Where(d => d.HeadId == headId && d.IsActive)
            .OrderBy(d => d.Name)
            .ToListAsync();
    }

    public async Task<List<Department>> GetByMemberAsync(Guid memberId)
    {
        // Current data model: user has a single DepartmentId.
        // Return that department (if any) in a list.
        var deptId = await _context.Users
            .AsNoTracking()
            .Where(u => u.Guid == memberId && u.IsActive)
            .Select(u => u.DepartmentId)
            .FirstOrDefaultAsync();

        if (!deptId.HasValue)
            return new List<Department>();

        var dept = await _context.Department
            .AsNoTracking()
            .Where(d => d.Guid == deptId.Value && d.IsActive)
            .ToListAsync();

        return dept;
    }
}
