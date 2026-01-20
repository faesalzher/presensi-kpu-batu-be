
using presensi_kpu_batu_be.Modules.UserModule.Dto;

namespace presensi_kpu_batu_be.Modules.UserModule
{
    public interface IUserService
    {
        Task<UserResponse?> GetUserByGuid(Guid guid);
        Task<List<User>> GetActiveUsersAsync();
    }
}
