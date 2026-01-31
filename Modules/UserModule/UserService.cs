using Microsoft.EntityFrameworkCore;
using presensi_kpu_batu_be.Modules.UserModule.Dto;


namespace presensi_kpu_batu_be.Modules.UserModule
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _context;

        public UserService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UserResponse?> GetUserByGuid(Guid guid)
        {
            return await _context.Users
                .AsNoTracking()
                .Where(u => u.Guid == guid)
                .Join(
                    _context.Department.AsNoTracking(),
                    u => u.DepartmentId,
                    d => d.Guid,
                    (u, d) => new UserResponse
                    {
                        Guid = u.Guid,
                        FullName = u.FullName!,
                        Email = u.Email!,
                        Nip = u.Nip,
                        PhoneNumber = u.PhoneNumber,
                        ProfileImageUrl = u.ProfileImageUrl,
                        Role = u.Role,
                        DepartmentId = u.DepartmentId,
                        Department = d.Name,
                        Position = u.Position,
                        IsActive = u.IsActive
                    }
                )
                .FirstOrDefaultAsync();
        }

        public async Task<List<User>> GetActiveUsersAsync()
        {
            return await _context.Users
                .Where(u => u.IsActive)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<UserResponse>> GetAllActiveUsersAsync()
        {
            return await _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive)
                .GroupJoin(
                    _context.Department.AsNoTracking(),
                    u => u.DepartmentId,
                    d => d.Guid,
                    (u, deps) => new { u, deps }
                )
                .SelectMany(
                    x => x.deps.DefaultIfEmpty(),
                    (x, d) => new UserResponse
                    {
                        Guid = x.u.Guid,
                        FullName = x.u.FullName ?? string.Empty,
                        Email = x.u.Email ?? string.Empty,
                        Nip = x.u.Nip,
                        PhoneNumber = x.u.PhoneNumber,
                        ProfileImageUrl = x.u.ProfileImageUrl,
                        Role = x.u.Role,
                        DepartmentId = x.u.DepartmentId,
                        Department = d != null ? d.Name : null,
                        Position = x.u.Position,
                        IsActive = x.u.IsActive
                    }
                )
                .ToListAsync();
        }

        public async Task<List<UserResponse>> GetUsersByDepartmentAsync(Guid departmentId)
        {
            return await _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive && u.DepartmentId == departmentId)
                .GroupJoin(
                    _context.Department.AsNoTracking(),
                    u => u.DepartmentId,
                    d => d.Guid,
                    (u, deps) => new { u, deps }
                )
                .SelectMany(
                    x => x.deps.DefaultIfEmpty(),
                    (x, d) => new UserResponse
                    {
                        Guid = x.u.Guid,
                        FullName = x.u.FullName ?? string.Empty,
                        Email = x.u.Email ?? string.Empty,
                        Nip = x.u.Nip,
                        PhoneNumber = x.u.PhoneNumber,
                        ProfileImageUrl = x.u.ProfileImageUrl,
                        Role = x.u.Role,
                        DepartmentId = x.u.DepartmentId,
                        Department = d != null ? d.Name : null,
                        Position = x.u.Position,
                        IsActive = x.u.IsActive
                    }
                )
                .OrderBy(u => u.FullName)
                .ToListAsync();
        }

        public async Task<List<UserResponse>> GetUsersByDepartmentNameAsync(string departmentName)
        {
            if (string.IsNullOrWhiteSpace(departmentName))
                return new List<UserResponse>();

            var normalized = departmentName.Trim();

            // resolve department id first (safer than joining on name)
            var departmentId = await _context.Department
                .AsNoTracking()
                .Where(d => d.Name != null && d.Name.ToLower() == normalized.ToLower())
                .Select(d => (Guid?)d.Guid)
                .FirstOrDefaultAsync();

            if (departmentId == null)
                return new List<UserResponse>();

            return await GetUsersByDepartmentAsync(departmentId.Value);
        }
    }
}
