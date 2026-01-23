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
}
