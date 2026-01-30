using presensi_kpu_batu_be.Modules.UserModule.Dto;

namespace presensi_kpu_batu_be.Modules.UserModule
{
    public interface IUserService
    {
        Task<UserResponse?> GetUserByGuid(Guid guid);
        Task<List<User>> GetActiveUsersAsync();

        // Returns all active users projected to UserResponse DTO
        Task<List<UserResponse>> GetAllActiveUsersAsync();

        // Returns active users belonging to a specific department (projected to UserResponse)
        Task<List<UserResponse>> GetUsersByDepartmentAsync(Guid departmentId);
    }
}
