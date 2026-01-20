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


    }
}
